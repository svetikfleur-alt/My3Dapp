# Spawn-Claude.ps1 - Claude Code CLI runner
# Single-agent spawner: claude.
# Loops through unchecked tasks in AgentOps/TASK_QUEUE.md, runs the configured agent
# on each, refreshes main build, moves to the next. Sequential, no parallelism.

$ErrorActionPreference = 'Continue'
$root      = 'C:\Users\Lena\My3DApp'
$queueFile = Join-Path $root 'AgentOps\TASK_QUEUE.md'
$doneFile  = Join-Path $root 'AgentOps\DONE.md'
$maxIter   = 30

$agentName = 'claude'
$agentCmd  = 'claude'
# Args use __PROMPT__ as the placeholder
$agentArgs = @('--dangerously-skip-permissions', '-p', '__PROMPT__')

function Show-Banner($text, $char = '=') {
  $bar = $char * 60
  Write-Host ""
  Write-Host $bar -ForegroundColor Cyan
  Write-Host $text -ForegroundColor Cyan
  Write-Host $bar -ForegroundColor Cyan
  Write-Host ""
}

function Show-QueueStatus {
  $lines = Get-Content $queueFile -Encoding UTF8
  $done  = ($lines | Where-Object { $_ -match '^- \[x\]' }).Count
  $open  = ($lines | Where-Object { $_ -match '^- \[ \]' }).Count
  Write-Host ("  Queue: {0} done, {1} open" -f $done, $open) -ForegroundColor Yellow
}

function Refresh-MainBuild {
  Write-Host "  Refreshing main-checkout build..." -ForegroundColor Yellow
  Push-Location $root
  try {
    & dotnet build --nologo -v minimal 2>&1 | Select-Object -Last 5 | ForEach-Object { Write-Host "    $_" }
    if ($LASTEXITCODE -eq 0) {
      Write-Host "  Main build: OK" -ForegroundColor Green
    } else {
      Write-Host "  Main build: FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    }
  } finally { Pop-Location }
}

function Run-Agent($prompt) {
  $resolvedArgs = @()
  foreach ($a in $agentArgs) {
    if ($a -eq '__PROMPT__') { $resolvedArgs += $prompt } else { $resolvedArgs += $a }
  }

  $cliInfo = Get-Command $agentCmd -ErrorAction SilentlyContinue
  if (-not $cliInfo) {
    Write-Host ("  ERROR: CLI '{0}' not on PATH" -f $agentCmd) -ForegroundColor Red
    return @{ Status = 'missing'; Exit = -1 }
  }

  $tmpOut = [IO.Path]::GetTempFileName()
  $tmpErr = [IO.Path]::GetTempFileName()
  Push-Location $root
  $exit = -1
  try {
    $cmdLine = '"{0}"' -f $agentCmd
    foreach ($a in $resolvedArgs) { $cmdLine += ' "{0}"' -f ($a -replace '"', '\"') }
    $cmdLine = "$cmdLine > `"$tmpOut`" 2> `"$tmpErr`""
    & cmd /c $cmdLine 2>&1 | Out-Null
    $exit = $LASTEXITCODE
  } finally { Pop-Location }

  $stdoutText = if (Test-Path $tmpOut) { Get-Content $tmpOut -Raw -ErrorAction SilentlyContinue } else { '' }
  $stderrText = if (Test-Path $tmpErr) { Get-Content $tmpErr -Raw -ErrorAction SilentlyContinue } else { '' }
  Remove-Item $tmpOut, $tmpErr -ErrorAction SilentlyContinue

  if ($stdoutText) { Write-Host $stdoutText }
  if ($stderrText) { Write-Host $stderrText -ForegroundColor DarkGray }

  if ($exit -eq 0) { return @{ Status = 'ok'; Exit = 0 } }
  return @{ Status = 'error'; Exit = $exit }
}

Show-Banner ('AgentOps claude-only spawner - start')
Show-QueueStatus
$iter = 0

while ($iter -lt $maxIter) {
  $iter++
  $lines = Get-Content $queueFile -Encoding UTF8
  $nextLine = $null
  foreach ($line in $lines) {
    if ($line -match '^- \[ \] (\d+)') { $nextLine = $line; break }
  }
  if (-not $nextLine) { Show-Banner 'Queue clean.'; break }

  $taskNum = ''
  if ($nextLine -match '^- \[ \] (\d+)') { $taskNum = $matches[1] }
  $specPath = ''
  if ($nextLine -match '(TASKS/[A-Za-z0-9_\-/.]+\.md)') { $specPath = $matches[1] }
  $title = $nextLine -replace '^- \[ \] \d+\s*\P{L}+\s*', '' -replace '\s+\P{L}+\s*TASKS/.*$', ''
  $title = $title.Trim(); if (-not $title) { $title = "task $taskNum" }
  if (-not $specPath) { $specPath = "TASKS/{0:D2}-*.md" -f [int]$taskNum }

  Show-Banner ("START iter {0}: task {1} - {2}" -f $iter, $taskNum, $title) '-'
  $startedAt = Get-Date
  Write-Host ("  Started: {0:yyyy-MM-dd HH:mm:ss}" -f $startedAt) -ForegroundColor Yellow
  Write-Host ("  Spec   : {0}" -f $specPath) -ForegroundColor Yellow
  Write-Host ("  Agent  : {0}" -f $agentName) -ForegroundColor Yellow

  $prompt = @"
Read AgentOps/$specPath and implement it.

Codebase: .NET / Avalonia + WebView2. UI in AvaloniaApp/, geometry in Engine/. Do not add NuGet packages or WinForms references. Do not touch temp_verify_* folders.

Read AgentOps/MERGE_PROTOCOL.md and AgentOps/DIALOG_STANDARD.md before touching code.

Process: read spec; implement; build via runner (write AgentOps/build_request.txt, poll build_result.json) until 0 errors; commit `agentops: $taskNum - $title`; mark - [ ] $taskNum as - [x] in TASK_QUEUE.md; append timestamp lines (YYYY-MM-DD HH:MM) to DONE.md and LOG.md; final report <=15 lines.
"@

  $result = Run-Agent $prompt
  if ($result.Status -ne 'ok') {
    Show-Banner ("FAILED iter {0}: agent exit={1} - stopping" -f $iter, $result.Exit) '!'
    break
  }

  $afterLines = Get-Content $queueFile -Encoding UTF8
  $stillOpen = $false
  foreach ($line in $afterLines) { if ($line -match "^- \[ \] $taskNum\b") { $stillOpen = $true; break } }
  if ($stillOpen) {
    Show-Banner ("FAILED iter {0}: task {1} still unchecked - stopping" -f $iter, $taskNum) '!'
    break
  }

  $duration = (Get-Date) - $startedAt
  Show-Banner ("DONE iter {0}: task {1} - took {2:hh\\:mm\\:ss}" -f $iter, $taskNum, $duration) '+'
  Show-QueueStatus
  if (Test-Path $doneFile) {
    $lastDone = Get-Content $doneFile -Encoding UTF8 | Select-Object -Last 4
    Write-Host "  Recent DONE entries:" -ForegroundColor Green
    foreach ($d in $lastDone) { Write-Host "    $d" }
  }
  Refresh-MainBuild
  Start-Sleep -Seconds 2
}

Show-Banner ("AgentOps - loop ended after {0} iter(s)." -f $iter)
Show-QueueStatus
