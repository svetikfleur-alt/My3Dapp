# AgentOps Next-Task Spawner (multi-agent autonomous looping mode)
#
# Tries multiple coding agents in priority order. When one hits its usage limit,
# falls back to the next. When all are at their limit, sleeps until the earliest
# reset time and resumes automatically.
#
# Configure the agents at the top of the script. Each agent has:
#   Name             - friendly name shown in the terminal
#   Cmd              - executable on PATH
#   Args             - argument array; '__PROMPT__' is the placeholder for the prompt
#   LimitRegex       - regex matched against stdout/stderr to detect 'usage limit hit'
#   Enabled          - $true to use, $false to skip

$ErrorActionPreference = 'Stop'
$root      = 'C:\Users\Lena\My3DApp'
$queueFile = Join-Path $root 'AgentOps\TASK_QUEUE.md'
$doneFile  = Join-Path $root 'AgentOps\DONE.md'
$stateFile = Join-Path $root 'AgentOps\spawner.state.json'
$failureLogFile = Join-Path $root 'AgentOps\last_agent_failure.log'
$agentPromptFile = Join-Path $root 'AgentOps\current_task_prompt.md'
$maxIter   = 30

# ====================================================================
# AGENT CONFIG
# ====================================================================
$agents = @(
  [pscustomobject]@{
    Name       = 'codex'
    Cmd        = 'codex'
    Args       = @('exec', '--skip-git-repo-check')   # prompt is sent via stdin
    StdinMode  = $true
    TimeoutSec = 75
    LimitRegex = "rate.?limit|quota.?exceeded|usage.?limit|out.?of.?credits"
    Env        = @{ CODEX_HOME = (Join-Path $root 'AgentOps\codex-home') }
    Enabled    = $true                # priority 1: primary autonomous agent
  },
  [pscustomobject]@{
    Name       = 'claude-code'
    Cmd        = 'claude'
    Args       = @('--dangerously-skip-permissions', '-p')
    StdinMode  = $true
    TimeoutSec = 75
    LimitRegex = "You've hit your limit|usage limit|monthly limit|rate.?limit"
    Enabled    = $true                # priority 2: fallback after codex
  },
  [pscustomobject]@{
    Name       = 'gh-copilot'
    Cmd        = 'gh'
    Args       = @('copilot', 'suggest', '-t', 'shell', '__PROMPT__')
    StdinMode  = $false
    TimeoutSec = 45
    LimitRegex = "limit|quota"
    Enabled    = $true                # priority 3: final fallback
  }
)
# ====================================================================

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

function Load-CooldownState {
  if (Test-Path $stateFile) {
    try { return Get-Content $stateFile -Raw | ConvertFrom-Json } catch { return @{} }
  }
  return @{}
}
function Save-CooldownState($state) {
  $state | ConvertTo-Json -Depth 4 | Set-Content -Path $stateFile -Encoding UTF8
}

function Is-AgentCooldown($state, $name) {
  if (-not $state.PSObject.Properties[$name]) { return $false }
  $until = [datetime]::Parse($state.$name)
  return (Get-Date) -lt $until
}
function Set-AgentCooldown($state, $name, $resetAt) {
  if ($state -is [hashtable]) { $state[$name] = $resetAt.ToString('o') }
  else { $state | Add-Member -NotePropertyName $name -NotePropertyValue $resetAt.ToString('o') -Force }
  Save-CooldownState $state
}
function Earliest-Cooldown($state) {
  $earliest = $null
  foreach ($p in $state.PSObject.Properties) {
    $t = [datetime]::Parse($p.Value)
    if (($t -gt (Get-Date)) -and ($null -eq $earliest -or $t -lt $earliest)) { $earliest = $t }
  }
  return $earliest
}

function Get-AgentFailureSummary($output, $exitCode) {
  if ([string]::IsNullOrWhiteSpace($output)) {
    return "exit=$exitCode"
  }

  $lines = $output -split "\r?\n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
  foreach ($line in $lines) {
    if ($line -match '^(error|ERROR|fatal|Fatal|failed|Failed)\b') {
      return $line
    }
  }

  foreach ($line in $lines) {
    if ($line -match 'Unable to connect to API|ConnectionRefused|ECONNREFUSED|authentication|not authenticated|login required|API key|invalid api key|unauthorized|forbidden') {
      return $line
    }
  }

  foreach ($line in $lines) {
    if ($line -match 'permission|readonly|read.?only|websocket|stream disconnected|unexpected argument|not on PATH|limit') {
      return $line
    }
  }

  return "exit=$exitCode"
}

function Write-AgentFailureLog($agentName, $exitCode, $output) {
  $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
  $summary = Get-AgentFailureSummary $output $exitCode
  $content = @(
    "[$timestamp] agent=$agentName exit=$exitCode"
    "summary: $summary"
    ""
    $output
  ) -join [Environment]::NewLine
  Set-Content -Path $failureLogFile -Value $content -Encoding UTF8
}

function Resolve-AgentLaunchPath($cliInfo) {
  if (-not $cliInfo) { return $null }

  $source = $cliInfo.Source
  if (-not $source) { return $null }

  $preferred = @(
    [IO.Path]::ChangeExtension($source, '.cmd'),
    [IO.Path]::ChangeExtension($source, '.exe'),
    [IO.Path]::ChangeExtension($source, '.bat'),
    $source
  )

  foreach ($candidate in $preferred) {
    if ($candidate -and (Test-Path $candidate)) {
      return $candidate
    }
  }

  return $source
}

function Format-CmdArg($text) {
  if ($null -eq $text) { return '""' }
  if ($text -match '[\s"&<>|^]') {
    return '"' + ($text -replace '"', '\"') + '"'
  }
  return $text
}

function Resolve-SpecPath($specPath, $taskNum) {
  $candidates = New-Object System.Collections.Generic.List[string]

  if (-not [string]::IsNullOrWhiteSpace($specPath)) {
    $normalized = $specPath -replace '/', '\'
    if ([IO.Path]::IsPathRooted($normalized)) {
      $candidates.Add($normalized)
    } else {
      $candidates.Add((Join-Path $root $normalized))
      $candidates.Add((Join-Path $root ("AgentOps\" + $normalized)))
    }
  }

  if ($taskNum) {
    $taskPrefix = "{0:D2}" -f [int]$taskNum
    $candidates.Add((Join-Path $root ("AgentOps\TASKS\{0}-*.md" -f $taskPrefix)))
    $candidates.Add((Join-Path $root ("TASKS\{0}-*.md" -f $taskPrefix)))
  }

  foreach ($candidate in $candidates) {
    if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
    if ($candidate.Contains('*') -or $candidate.Contains('?')) {
      $match = Get-ChildItem -Path $candidate -ErrorAction SilentlyContinue | Select-Object -First 1
      if ($match) { return $match.FullName }
    } elseif (Test-Path $candidate) {
      return (Resolve-Path $candidate).Path
    }
  }

  return $null
}

function Write-AgentPromptFile($taskNum, $title, $resolvedSpecPath) {
  $relativeSpec = $resolvedSpecPath
  if ($resolvedSpecPath -like "$root*") {
    $relativeSpec = $resolvedSpecPath.Substring($root.Length).TrimStart('\') -replace '\\', '/'
  }

  $content = @"
Task: $taskNum - $title

Read the task spec at:
$relativeSpec

Read these project rules before changing code:
- Agent rules: Agent rules.md
- Merge protocol: AgentOps/MERGE_PROTOCOL.md
- Dialog standard: AgentOps/DIALOG_STANDARD.md

Constraints:
- Codebase: .NET / Avalonia + WebView2
- UI in AvaloniaApp/
- Geometry/backend in Engine/
- Do not add NuGet packages
- Do not add WinForms references
- Do not touch temp_verify_* folders

Required process:
1. Read the spec and inspect existing code first
2. Implement the task
3. Build via the runner flow (write AgentOps/build_request.txt, poll build_result.json) until 0 errors
4. Commit with message: agentops: $taskNum - $title
5. Mark the task from [ ] to [x] in AgentOps/TASK_QUEUE.md
6. Append timestamp lines (YYYY-MM-DD HH:MM) to AgentOps/DONE.md and AgentOps/LOG.md
7. Return a final report in 15 lines or less
"@

  Set-Content -Path $agentPromptFile -Value $content -Encoding UTF8
  return "Read AgentOps/current_task_prompt.md and follow it exactly."
}

function Run-Agent($agent, $prompt) {
  Write-Host ("  Trying agent: {0}" -f $agent.Name) -ForegroundColor Yellow

  # First check the CLI exists at all
  $cliInfo = Get-Command $agent.Cmd -ErrorAction SilentlyContinue
  if (-not $cliInfo) {
    Write-Host ("  Agent {0}: CLI '{1}' not on PATH" -f $agent.Name, $agent.Cmd) -ForegroundColor Red
    return @{ Status = 'missing'; Output = ''; Exit = -1 }
  }
  $cliPath = Resolve-AgentLaunchPath $cliInfo
  if (-not $cliPath) {
    Write-Host ("  Agent {0}: could not resolve launch path for '{1}'" -f $agent.Name, $agent.Cmd) -ForegroundColor Red
    return @{ Status = 'missing'; Output = ''; Exit = -1 }
  }
  Write-Host ("  Launch : {0}" -f $cliPath) -ForegroundColor DarkGray

  $resolvedArgs = @()
  foreach ($a in $agent.Args) {
    if ($a -eq '__PROMPT__') { $resolvedArgs += $prompt } else { $resolvedArgs += $a }
  }

  $stdinMode = $false
  if ($agent.PSObject.Properties['StdinMode']) { $stdinMode = [bool]$agent.StdinMode }
  $timeoutSec = 90
  if ($agent.PSObject.Properties['TimeoutSec'] -and $agent.TimeoutSec) { $timeoutSec = [int]$agent.TimeoutSec }
  $agentEnv = @{}
  if ($agent.PSObject.Properties['Env'] -and $agent.Env) {
    $agentEnv = $agent.Env
  }

  # Suppress global Stop behavior so external CLI stderr does not turn into a thrown exception
  $prevEAP = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'

  $tmpOut = [IO.Path]::GetTempFileName()
  $tmpErr = [IO.Path]::GetTempFileName()
  $tmpIn  = $null
  $priorEnv = @{}
  $process = $null

  Push-Location $root
  $exit = -1
  try {
    foreach ($entry in $agentEnv.GetEnumerator()) {
      $priorEnv[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
      if ($entry.Value) {
        if ($entry.Key -eq 'CODEX_HOME' -and -not (Test-Path $entry.Value)) {
          New-Item -ItemType Directory -Path $entry.Value -Force | Out-Null
        }
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
      }
    }

      if ($stdinMode) {
        $tmpIn = [IO.Path]::GetTempFileName()
        Set-Content -Path $tmpIn -Value $prompt -Encoding UTF8 -NoNewline
        $cmdLine = '""{0}"' -f $cliPath
        foreach ($a in $resolvedArgs) { $cmdLine += ' {0}' -f (Format-CmdArg $a) }
        $cmdLine += ' < {0} > {1} 2> {2}"' -f (Format-CmdArg $tmpIn), (Format-CmdArg $tmpOut), (Format-CmdArg $tmpErr)
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'cmd.exe'
        $psi.Arguments = "/d /s /c $cmdLine"
        $psi.WorkingDirectory = $root
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $process = [System.Diagnostics.Process]::Start($psi)
      } else {
        $cmdLine = '""{0}"' -f $cliPath
        foreach ($a in $resolvedArgs) { $cmdLine += ' {0}' -f (Format-CmdArg $a) }
        $cmdLine += ' > {0} 2> {1}"' -f (Format-CmdArg $tmpOut), (Format-CmdArg $tmpErr)
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = 'cmd.exe'
        $psi.Arguments = "/d /s /c $cmdLine"
        $psi.WorkingDirectory = $root
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
      $process = [System.Diagnostics.Process]::Start($psi)
    }

      if ($null -eq $process) {
        throw "Failed to launch $($agent.Name)."
      }

      Write-Host ("  Launched {0} (timeout {1}s)" -f $agent.Name, $timeoutSec) -ForegroundColor DarkCyan
      Write-Host ("  Waiting for {0} to respond..." -f $agent.Name) -ForegroundColor DarkGray

      $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
      $heartbeatSec = 5
      $lastHeartbeatSec = -1
      $reportedOutput = $false
      $timedOut = $false

      while (-not $process.HasExited) {
        Start-Sleep -Milliseconds 1000
        if ($process.HasExited) { break }

        $elapsedSec = [int][Math]::Floor($stopwatch.Elapsed.TotalSeconds)

        if (-not $reportedOutput) {
          $stdoutLength = 0
          $stderrLength = 0
          if (Test-Path $tmpOut) {
            $stdoutLength = (Get-Item $tmpOut -ErrorAction SilentlyContinue).Length
          }
          if (Test-Path $tmpErr) {
            $stderrLength = (Get-Item $tmpErr -ErrorAction SilentlyContinue).Length
          }

          if (($stdoutLength + $stderrLength) -gt 0) {
            Write-Host ("  {0} produced output after {1}s; waiting for completion..." -f $agent.Name, $elapsedSec) -ForegroundColor DarkCyan
            $reportedOutput = $true
          }
        }

        if (($elapsedSec -gt 0) -and (($elapsedSec % $heartbeatSec) -eq 0) -and ($elapsedSec -ne $lastHeartbeatSec)) {
          Write-Host ("  Running {0}... {1}s / {2}s" -f $agent.Name, $elapsedSec, $timeoutSec) -ForegroundColor DarkGray
          $lastHeartbeatSec = $elapsedSec
        }

        if ($elapsedSec -ge $timeoutSec) {
          $timedOut = $true
          break
        }
      }

      $stopwatch.Stop()

      if ($timedOut -and -not $process.HasExited) {
        try {
          $process.Kill()
        } catch {
        }

        $timeoutMessage = "Timed out after $timeoutSec seconds."
        Add-Content -Path $tmpErr -Value $timeoutMessage
        Write-AgentFailureLog $agent.Name -1 $timeoutMessage
        Write-Host ("  Agent {0}: timeout after {1}s" -f $agent.Name, $timeoutSec) -ForegroundColor Yellow
        Write-Host ("  Logged : {0}" -f $failureLogFile) -ForegroundColor DarkGray
        return @{ Status = 'timeout'; Output = $timeoutMessage; Exit = -1 }
      }

      $process.WaitForExit()
      $exit = $process.ExitCode
  } catch {
    $ErrorActionPreference = $prevEAP
    if ($process -and -not $process.HasExited) {
      try {
        $process.Kill()
      } catch {
      }
    }
    Remove-Item $tmpOut, $tmpErr -ErrorAction SilentlyContinue
    if ($tmpIn) { Remove-Item $tmpIn -ErrorAction SilentlyContinue }
    Write-Host ("  Agent {0} launch error: {1}" -f $agent.Name, $_.Exception.Message) -ForegroundColor Red
    return @{ Status = 'error'; Output = $_.Exception.Message; Exit = -1 }
  } finally {
    foreach ($entry in $priorEnv.GetEnumerator()) {
      [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    Pop-Location
    $ErrorActionPreference = $prevEAP
  }

  $stdoutText = if (Test-Path $tmpOut) { Get-Content $tmpOut -Raw -ErrorAction SilentlyContinue } else { '' }
  $stderrText = if (Test-Path $tmpErr) { Get-Content $tmpErr -Raw -ErrorAction SilentlyContinue } else { '' }
  Remove-Item $tmpOut, $tmpErr -ErrorAction SilentlyContinue
  if ($tmpIn) { Remove-Item $tmpIn -ErrorAction SilentlyContinue }

  $output = ($stdoutText + "`n" + $stderrText)
  if ($stdoutText) { Write-Host $stdoutText }
  if ($stderrText) { Write-Host $stderrText -ForegroundColor DarkGray }

  if ($output -match $agent.LimitRegex) {
    Write-Host ("  Agent {0}: matched LimitRegex - treating as usage limit" -f $agent.Name) -ForegroundColor Red
    Write-AgentFailureLog $agent.Name $exit $output
    return @{ Status = 'limit'; Output = $output; Exit = $exit }
  }
  if ($exit -eq 0) { return @{ Status = 'ok'; Output = $output; Exit = $exit } }
  $summary = Get-AgentFailureSummary $output $exit
  Write-AgentFailureLog $agent.Name $exit $output
  Write-Host ("  Agent {0}: exit={1}" -f $agent.Name, $exit) -ForegroundColor Yellow
  Write-Host ("  Reason: {0}" -f $summary) -ForegroundColor DarkYellow
  Write-Host ("  Logged : {0}" -f $failureLogFile) -ForegroundColor DarkGray
  return @{ Status = 'error'; Output = $output; Exit = $exit }
}

# ====================================================================
# MAIN LOOP
# ====================================================================
if (-not (Test-Path $queueFile)) { Write-Host 'TASK_QUEUE.md not found'; exit 1 }

Show-Banner 'AgentOps multi-agent spawner - start'
Show-QueueStatus
$state = Load-CooldownState
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
    $resolvedSpecPath = Resolve-SpecPath $specPath $taskNum

    Show-Banner ("START iter {0}: task {1} - {2}" -f $iter, $taskNum, $title) '-'
    $startedAt = Get-Date
    Write-Host ("  Started: {0:yyyy-MM-dd HH:mm:ss}" -f $startedAt) -ForegroundColor Yellow
    Write-Host ("  Spec   : {0}" -f $specPath) -ForegroundColor Yellow

    if (-not $resolvedSpecPath) {
      Write-Host ("  ERROR  : could not resolve task spec for task {0}" -f $taskNum) -ForegroundColor Red
      Write-Host ("  Tried  : {0}" -f $specPath) -ForegroundColor DarkRed
      break
    }

    $relativeResolvedSpec = if ($resolvedSpecPath -like "$root*") { $resolvedSpecPath.Substring($root.Length).TrimStart('\') -replace '\\', '/' } else { $resolvedSpecPath }
    Write-Host ("  Resolved spec: {0}" -f $relativeResolvedSpec) -ForegroundColor DarkCyan

    $prompt = Write-AgentPromptFile $taskNum $title $resolvedSpecPath
    Write-Host ("  Prompt : AgentOps/current_task_prompt.md") -ForegroundColor DarkCyan

    $taskDone = $false
    $hadImmediateFailure = $false
    foreach ($agent in $agents) {
      if (-not $agent.Enabled) { continue }
      if (Is-AgentCooldown $state $agent.Name) {
        Write-Host ("  Skipping {0} - on cooldown until {1}" -f $agent.Name, $state.$($agent.Name)) -ForegroundColor DarkYellow
        continue
    }
    $result = Run-Agent $agent $prompt
    if ($result.Status -eq 'ok') {
      $taskDone = $true; break
    } elseif ($result.Status -eq 'limit') {
      # 1-hour cooldown when output really matched a "limit" pattern
      $resetAt = (Get-Date).AddHours(1)
      Set-AgentCooldown $state $agent.Name $resetAt
      Write-Host ("  {0} cooldown 1h (limit detected)" -f $agent.Name) -ForegroundColor Red
    } elseif ($result.Status -eq 'missing') {
      # CLI not on PATH - long cooldown, no point retrying every iteration
      Set-AgentCooldown $state $agent.Name ((Get-Date).AddHours(2))
      Write-Host ("  {0} CLI missing - cooldown 2h" -f $agent.Name) -ForegroundColor DarkYellow
      } elseif ($result.Status -eq 'timeout') {
        $hadImmediateFailure = $true
        Write-Host ("  {0} timed out and was skipped for this task" -f $agent.Name) -ForegroundColor DarkYellow
      } elseif ($result.Status -eq 'error') {
        $hadImmediateFailure = $true
        # Generic non-zero exit. NO cooldown - just try the next agent on this task.
        Write-Host ("  {0} returned non-zero (no cooldown set, falling through)" -f $agent.Name) -ForegroundColor DarkYellow
      }
    }

    if (-not $taskDone) {
      if ($hadImmediateFailure) {
        Show-Banner ("FAILED iter {0}: runnable agents failed - stopping" -f $iter) '!'
        Write-Host "  At least one configured agent launched but failed." -ForegroundColor Red
        Write-Host ("  Inspect: {0}" -f $failureLogFile) -ForegroundColor DarkYellow
        break
      }
      $earliest = Earliest-Cooldown $state
      if ($earliest) {
        $waitMin = [Math]::Max(1, [Math]::Ceiling(($earliest - (Get-Date)).TotalMinutes))
        Write-Host ("  All agents on cooldown. Sleeping {0} minutes until {1:HH:mm}." -f $waitMin, $earliest) -ForegroundColor Yellow
      Start-Sleep -Seconds ($waitMin * 60)
      $state = Load-CooldownState
      continue  # retry this same task on next iter
    } else {
      Show-Banner ("FAILED iter {0}: no agent succeeded - stopping" -f $iter) '!'
      break
    }
  }

  $afterLines = Get-Content $queueFile -Encoding UTF8
  $stillOpen = $false
  foreach ($line in $afterLines) { if ($line -match "^- \[ \] $taskNum\b") { $stillOpen = $true; break } }
  if ($stillOpen) {
    Show-Banner ("FAILED iter {0}: task {1} still unchecked" -f $iter, $taskNum) '!'
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
