[CmdletBinding()]
param(
  [int]$MaxIterations = 0,
  [switch]$PrepareOnly
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$agentOpsRoot = Join-Path $root 'AgentOps'
$tasksRoot = Join-Path $agentOpsRoot 'TASKS'
$queueFile = Join-Path $agentOpsRoot 'TASK_QUEUE.md'
$doneFile = Join-Path $agentOpsRoot 'DONE.md'
$logFile = Join-Path $agentOpsRoot 'LOG.md'
$taskSourceFile = Join-Path $agentOpsRoot 'TASK_SOURCE.md'
$uiPolishPolicyFile = Join-Path $agentOpsRoot 'UI_POLISH_POLICY.md'
$currentTaskFile = Join-Path $agentOpsRoot 'CURRENT_TASK.md'
$handoffFile = Join-Path $agentOpsRoot 'HANDOFF.md'
$statusFile = Join-Path $agentOpsRoot 'STATUS.md'
$progressFile = Join-Path $agentOpsRoot 'PROGRESS.md'
$verificationFile = Join-Path $agentOpsRoot 'VERIFICATION.md'
$queueAutowriteFile = Join-Path $agentOpsRoot 'LAST_QUEUE_AUTOWRITE.md'
$appBehaviorFile = Join-Path $agentOpsRoot 'APP_BEHAVIOR.md'
$lastAppBehaviorFile = Join-Path $agentOpsRoot 'LAST_APP_BEHAVIOR.json'
$healthFile = Join-Path $agentOpsRoot 'HEALTH.md'
$lastHealthResultFile = Join-Path $agentOpsRoot 'LAST_HEALTH_RESULT.json'
$runnerLockFile = Join-Path $agentOpsRoot 'RUNNER.lock'
$lastPromptFile = Join-Path $agentOpsRoot 'LAST_CODEX_PROMPT.md'
$codexStdoutLog = Join-Path $agentOpsRoot 'LAST_CODEX_STDOUT.log'
$codexStderrLog = Join-Path $agentOpsRoot 'LAST_CODEX_STDERR.log'
$lastTestResultFile = Join-Path $agentOpsRoot 'LAST_TEST_RESULT.json'
$lastTestBuildLog = Join-Path $agentOpsRoot 'LAST_TEST_BUILD.log'
$lastVerifyResultFile = Join-Path $agentOpsRoot 'LAST_VERIFY_RESULT.json'
$runSummaryFile = Join-Path $agentOpsRoot 'RUN_SUMMARY.md'
$workerLogFile = Join-Path $agentOpsRoot 'worker.log'
$testerScript = Join-Path $agentOpsRoot 'Test-CodexTask.ps1'
$verifierScript = Join-Path $agentOpsRoot 'Verify-CodexTask.ps1'
$runnerScriptPath = $MyInvocation.MyCommand.Path
$runnerScriptStamp = if ($runnerScriptPath -and (Test-Path $runnerScriptPath)) { (Get-Item $runnerScriptPath).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') } else { '(unknown)' }

function Write-WorkerLog {
  param([string]$Message)

  try {
    if (-not (Test-Path $agentOpsRoot)) {
      New-Item -ItemType Directory -Path $agentOpsRoot -Force | Out-Null
    }

    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Add-Content -Path $workerLogFile -Value ("[{0}] {1}" -f $stamp, $Message) -Encoding UTF8
  } catch {
    # Logging must never break the worker.
  }
}

function Show-Banner($text, $char = '=') {
  $bar = $char * 72
  Write-Host ''
  Write-Host $bar -ForegroundColor Cyan
  Write-Host $text -ForegroundColor Cyan
  Write-Host $bar -ForegroundColor Cyan
  Write-Host ''
}

function Show-QueueStatus {
  if (-not (Test-Path $queueFile)) {
    Write-Host '  Queue: missing TASK_QUEUE.md' -ForegroundColor Yellow
    return
  }

  $stats = Get-QueueStats
  Write-Host ("  Queue: {0} done, {1} open" -f $stats.Done, $stats.Open) -ForegroundColor Yellow
}

function Get-QueueStats {
  if (-not (Test-Path $queueFile)) {
    return [pscustomobject]@{ Done = 0; Open = 0 }
  }

  $lines = Get-Content $queueFile -Encoding UTF8
  return [pscustomobject]@{
    Done = @($lines | Where-Object { $_ -match '^- \[x\]' }).Count
    Open = @($lines | Where-Object { $_ -match '^- \[ \]' }).Count
  }
}

function Get-OpenTaskCount {
  return (Get-QueueStats).Open
}

function Test-IsFinalUiPolishText([string]$text) {
  if ([string]::IsNullOrWhiteSpace($text)) { return $false }
  return ($text.ToLowerInvariant() -match 'final\s+ui\s+polish|final-ui-polish|end-of-cycle\s+polish')
}

function Get-UiPolishStageStatus {
  if (-not (Test-Path $queueFile)) {
    return [pscustomobject]@{
      Exists = $false
      Pending = $false
      Completed = $false
      PendingCount = 0
      CompletedCount = 0
      Line = ''
      Message = 'missing queue'
    }
  }

  $lines = @(Get-Content $queueFile -Encoding UTF8)
  $polishLines = @($lines | Where-Object { Test-IsFinalUiPolishText $_ })
  $pending = @($polishLines | Where-Object { $_ -match '^\s*-\s*\[ \]' })
  $completed = @($polishLines | Where-Object { $_ -match '^\s*-\s*\[x\]' })
  $line = if ($pending.Count -gt 0) { [string]$pending[0] } elseif ($completed.Count -gt 0) { [string]$completed[-1] } else { '' }
  $message = if ($pending.Count -gt 1) {
    "duplicate pending final UI polish tasks ($($pending.Count)); keep only one"
  } elseif ($pending.Count -gt 0) {
    'pending rare end-of-cycle polish'
  } elseif ($completed.Count -gt 0) {
    'already completed in this queue cycle'
  } else {
    'not present'
  }

  return [pscustomobject]@{
    Exists = $polishLines.Count -gt 0
    Pending = $pending.Count -gt 0
    Completed = $completed.Count -gt 0
    PendingCount = $pending.Count
    CompletedCount = $completed.Count
    Line = $line
    Message = $message
  }
}

function Show-UiPolishStageStatus {
  $status = Get-UiPolishStageStatus
  Write-Host ("  UI polish stage: {0}" -f $status.Message) -ForegroundColor Yellow
  if (-not [string]::IsNullOrWhiteSpace($status.Line)) {
    Write-Host ("    {0}" -f $status.Line) -ForegroundColor DarkGray
  }
}

function Show-Stage($index, $total, $name, $detail = $null) {
  $percent = if ($total -gt 0) { [int][Math]::Round(($index / [double]$total) * 100) } else { 0 }
  Write-Host ''
  Write-Host ("  [{0}/{1}] {2} ({3}%)" -f $index, $total, $name, $percent) -ForegroundColor Magenta
  if (-not [string]::IsNullOrWhiteSpace($detail)) {
    Write-Host ("        {0}" -f $detail) -ForegroundColor DarkMagenta
  }
  Write-Host ("        Progress: stage {0} of {1}" -f $index, $total) -ForegroundColor DarkGray
}

function Write-Status($task, $phase, $message, $state = 'running') {
  $taskLabel = if ($null -ne $task) { "{0} - {1}" -f $task.Number, $task.Title } else { '(none)' }
  $content = @(
    "# AgentOps Status - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ''
    "State: $state"
    "Phase: $phase"
    "Task: $taskLabel"
    "Message: $message"
    "RunnerScript: $runnerScriptPath"
    "RunnerScriptModified: $runnerScriptStamp"
    ''
    "Prompt: $lastPromptFile"
    "StdoutLog: $codexStdoutLog"
    "StderrLog: $codexStderrLog"
    "TestResult: $lastTestResultFile"
    "VerifyResult: $lastVerifyResultFile"
    "RunSummary: $runSummaryFile"
    "Progress: $progressFile"
    "BehaviorReport: $appBehaviorFile"
    "QueueAutowrite: $queueAutowriteFile"
    "HealthReport: $healthFile"
  ) -join [Environment]::NewLine

  Set-Content -Path $statusFile -Value $content -Encoding UTF8
  Set-Content -Path $runSummaryFile -Value $content -Encoding UTF8
}

function Write-ProgressSnapshot($task, [int]$stageIndex, [int]$stageTotal, [string]$stageName, [string]$message, [int]$elapsedSeconds = 0, [int]$timeoutSeconds = 0, [int]$stdoutLines = 0, [int]$stderrLines = 0) {
  $taskLabel = if ($null -ne $task) { "{0} - {1}" -f $task.Number, $task.Title } else { '(none)' }
  $stagePercent = if ($stageTotal -gt 0) { [int][Math]::Round(($stageIndex / [double]$stageTotal) * 100) } else { 0 }
  $timeLine = if ($timeoutSeconds -gt 0) { "{0}s / {1}s" -f $elapsedSeconds, $timeoutSeconds } else { "{0}s" -f $elapsedSeconds }
  $stats = Get-RelevantChangeStats

  $content = @(
    "# AgentOps Progress - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ''
    "Task: $taskLabel"
    "Stage: $stageIndex/$stageTotal - $stageName"
    "StagePercent: $stagePercent"
    "Message: $message"
    "Elapsed: $timeLine"
    "CodexStdoutLines: $stdoutLines"
    "CodexStderrLines: $stderrLines"
    "RelevantFiles: $($stats.FileCount)"
    "RelevantLinesAdded: $($stats.Added)"
    "RelevantLinesRemoved: $($stats.Removed)"
    "IgnoredRuntimePaths: $($stats.IgnoredRuntimeCount)"
  )

  $content -join [Environment]::NewLine | Set-Content -Path $progressFile -Encoding UTF8
}

function Acquire-RunnerLock {
  $lockInfo = [pscustomobject]@{
    pid = $PID
    started_at = (Get-Date).ToString('o')
    machine = $env:COMPUTERNAME
    root = $root
  }

  if (Test-Path $runnerLockFile) {
    $existing = $null
    try {
      $existing = Get-Content $runnerLockFile -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
    }

    if ($null -ne $existing -and $existing.pid) {
      $active = Get-Process -Id ([int]$existing.pid) -ErrorAction SilentlyContinue
      if ($active) {
        throw ("Another AgentOps runner is already active (PID {0}). Lock: {1}" -f $existing.pid, $runnerLockFile)
      }
    }

    Remove-Item -LiteralPath $runnerLockFile -Force -ErrorAction SilentlyContinue
    Write-Host ("  Removed stale runner lock: {0}" -f $runnerLockFile) -ForegroundColor DarkGray
  }

  $lockInfo | ConvertTo-Json -Depth 3 | Set-Content -Path $runnerLockFile -Encoding UTF8
  $script:RunnerLockHeld = $true
  Write-Host ("  Runner lock: {0}" -f $runnerLockFile) -ForegroundColor DarkGray
}

function Release-RunnerLock {
  if ($script:RunnerLockHeld -and (Test-Path $runnerLockFile)) {
    Remove-Item -LiteralPath $runnerLockFile -Force -ErrorAction SilentlyContinue
  }
  $script:RunnerLockHeld = $false
}

function Ensure-AgentOpsFiles {
  Write-Host '  Ensuring AgentOps files...' -ForegroundColor Yellow

  foreach ($dir in @($agentOpsRoot, $tasksRoot)) {
    if (-not (Test-Path $dir)) {
      New-Item -ItemType Directory -Path $dir -Force | Out-Null
      Write-Host ("    Created directory: {0}" -f $dir) -ForegroundColor Green
    }
  }

  if (-not (Test-Path $queueFile)) {
    @(
      '# Active queue (in priority order)'
      ''
    ) | Set-Content -Path $queueFile -Encoding UTF8
    Write-Host ("    Created file: {0}" -f $queueFile) -ForegroundColor Green
  }

  if (-not (Test-Path $doneFile)) {
    @(
      '# Done'
      ''
    ) | Set-Content -Path $doneFile -Encoding UTF8
    Write-Host ("    Created file: {0}" -f $doneFile) -ForegroundColor Green
  }

  if (-not (Test-Path $logFile)) {
    @(
      '# Log'
      ''
    ) | Set-Content -Path $logFile -Encoding UTF8
      Write-Host ("    Created file: {0}" -f $logFile) -ForegroundColor Green
  }

  if (-not (Test-Path $workerLogFile)) {
    @(
      '# AgentOps worker log'
      ''
    ) | Set-Content -Path $workerLogFile -Encoding UTF8
    Write-Host ("    Created file: {0}" -f $workerLogFile) -ForegroundColor Green
  }

  if (-not (Test-Path $taskSourceFile)) {
    @(
      '# Task source'
      ''
      '## Add new task seeds below as bullet or numbered lines.'
      '## Use unchecked bullets for real seeds you want expanded.'
      ''
      '- [x] Example only: tighten viewport toolbar grouping without redesigning the shell'
      '- [x] Example only: add a clearer app behavior log for UI-heavy tasks'
    ) | Set-Content -Path $taskSourceFile -Encoding UTF8
    Write-Host ("    Created file: {0}" -f $taskSourceFile) -ForegroundColor Green
  }

  if (-not (Test-Path $uiPolishPolicyFile)) {
    @(
      '# UI polish policy'
      ''
      'Final UI polish is a rare end-of-cycle stage, not a normal feature-task cleanup step.'
      ''
      'Rules:'
      '- Keep at most one pending final UI polish task in TASK_QUEUE.md.'
      '- Run it after functional/structural tasks, not after every feature.'
      '- Polish only existing visible CAD surfaces; do not add new CAD features.'
      '- Preserve viewport behavior, primitives, sketch flow, feature flow, and assistant fallback behavior.'
      '- Build must pass before the task can be marked complete.'
      ''
      'Default queue task: Final UI polish pass.'
    ) | Set-Content -Path $uiPolishPolicyFile -Encoding UTF8
    Write-Host ("    Created file: {0}" -f $uiPolishPolicyFile) -ForegroundColor Green
  }

  if (-not (Test-Path $handoffFile)) {
    Set-Content -Path $handoffFile -Value '' -Encoding UTF8
    Write-Host ("    Created file: {0}" -f $handoffFile) -ForegroundColor Green
  }
}

function Ensure-CodexFolders {
  Write-Host '  Ensuring Codex session folders...' -ForegroundColor Yellow

  $codexRoot = Join-Path $env:USERPROFILE '.codex'
  $sessionsRoot = Join-Path $codexRoot 'sessions'
  $todayRoot = Join-Path $sessionsRoot ((Get-Date).ToString('yyyy-MM-dd'))

  foreach ($dir in @($codexRoot, $sessionsRoot, $todayRoot)) {
    if (-not (Test-Path $dir)) {
      try {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host ("    Created directory: {0}" -f $dir) -ForegroundColor Green
      } catch {
        throw ("Failed to create Codex folder '{0}': {1}" -f $dir, $_.Exception.Message)
      }
    }
  }
}

function Ensure-GitRepo {
  Write-Host '  Verifying git repository...' -ForegroundColor Yellow
  Push-Location $root
  try {
    $inside = (& git rev-parse --is-inside-work-tree 2>$null)
    if ($LASTEXITCODE -ne 0 -or ($inside | Select-Object -Last 1) -ne 'true') {
      throw 'Project root is not inside a git repository.'
    }
  } finally {
    Pop-Location
  }
}

function Ensure-GitIdentity {
  Write-Host '  Ensuring local git identity...' -ForegroundColor Yellow
  Push-Location $root
  try {
    $userName = (& git config --local --get user.name 2>$null)
    $userEmail = (& git config --local --get user.email 2>$null)

    if ([string]::IsNullOrWhiteSpace(($userName | Select-Object -First 1))) {
      & git config --local user.name 'agentops'
      if ($LASTEXITCODE -ne 0) { throw 'Failed to set local git user.name.' }
      Write-Host '    Set local git user.name = agentops' -ForegroundColor Green
    }

    if ([string]::IsNullOrWhiteSpace(($userEmail | Select-Object -First 1))) {
      & git config --local user.email 'agent@my3dapp.local'
      if ($LASTEXITCODE -ne 0) { throw 'Failed to set local git user.email.' }
      Write-Host '    Set local git user.email = agent@my3dapp.local' -ForegroundColor Green
    }
  } finally {
    Pop-Location
  }
}

function Get-QueueLineInfo($line) {
  $pattern = '^- \[(?<state>[ x])\] (?<number>\d+)\s+.+?TASKS/(?<spec>[A-Za-z0-9_\-/.]+\.md)\s*$'
  $match = [regex]::Match($line, $pattern)
  if (-not $match.Success) {
    return $null
  }

  return [pscustomobject]@{
    State = $match.Groups['state'].Value
    Number = [int]$match.Groups['number'].Value
    SpecRelative = ('TASKS/' + $match.Groups['spec'].Value)
  }
}

function Invoke-AgentOpsHealthCheck {
  $errors = New-Object System.Collections.Generic.List[string]
  $warnings = New-Object System.Collections.Generic.List[string]
  $goods = New-Object System.Collections.Generic.List[string]

  foreach ($path in @(
    $queueFile, $doneFile, $logFile, $taskSourceFile,
    $testerScript, $verifierScript
  )) {
    if (-not (Test-Path $path)) {
      $errors.Add(("Missing required file: {0}" -f $path)) | Out-Null
    } else {
      $goods.Add(("Present: {0}" -f $path)) | Out-Null
    }
  }

  $queueInfos = New-Object System.Collections.Generic.List[object]
  if (Test-Path $queueFile) {
    $queueLines = @(Get-Content $queueFile -Encoding UTF8)
    for ($i = 0; $i -lt $queueLines.Count; $i++) {
      $line = $queueLines[$i]
      if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) { continue }
      if ($line -match '^\s*-\s+\(empty') { continue }

      $info = Get-QueueLineInfo $line
      if ($null -eq $info) {
        $warnings.Add(("Unrecognized queue line format at line {0}: {1}" -f ($i + 1), $line.Trim())) | Out-Null
        continue
      }

      $queueInfos.Add($info) | Out-Null

      $specPath = Join-Path $agentOpsRoot ($info.SpecRelative -replace '/', [IO.Path]::DirectorySeparatorChar)
      if (-not (Test-Path $specPath)) {
        $errors.Add(("Queue task {0} points to missing spec: {1}" -f $info.Number, $info.SpecRelative)) | Out-Null
      }
    }

    $duplicateNumbers = @($queueInfos | Group-Object Number | Where-Object Count -gt 1)
    foreach ($dup in $duplicateNumbers) {
      $errors.Add(("Duplicate task number in queue: {0}" -f $dup.Name)) | Out-Null
    }

    $duplicateSpecs = @($queueInfos | Group-Object SpecRelative | Where-Object Count -gt 1)
    foreach ($dup in $duplicateSpecs) {
      $errors.Add(("Duplicate task spec in queue: {0}" -f $dup.Name)) | Out-Null
    }

    $goods.Add(("Queue tasks parsed: {0}" -f $queueInfos.Count)) | Out-Null
  }

  if (Test-Path $tasksRoot) {
    foreach ($file in (Get-ChildItem -Path $tasksRoot -File -Filter '*.md' -ErrorAction SilentlyContinue)) {
      if ($file.BaseName -notmatch '^\d{2,}-') {
        $warnings.Add(("Task spec filename does not start with a numeric prefix: {0}" -f $file.Name)) | Out-Null
      }
    }
  }

  $openSeeds = @()
  if (Test-Path $taskSourceFile) {
    $openSeeds = @(Get-TaskSourceEntries | Where-Object { -not $_.Done })
    if ($openSeeds.Count -gt 0) {
      $warnings.Add(("TASK_SOURCE.md still has {0} unchecked seed(s) waiting for auto-write." -f $openSeeds.Count)) | Out-Null
    } else {
      $goods.Add('TASK_SOURCE.md has no pending unchecked seeds') | Out-Null
    }
  }

  if (Test-Path (Join-Path $root '.git\index.lock')) {
    $errors.Add('Git index lock is present. Another git process may be active or stuck.') | Out-Null
  } else {
    $goods.Add('Git index lock not present') | Out-Null
  }

  $result = if ($errors.Count -gt 0) { 'fail' } elseif ($warnings.Count -gt 0) { 'warn' } else { 'pass' }

  $reportLines = @(
    "# AgentOps health - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ''
    "Result: $result"
    ''
    'Good:'
  )
  $reportLines += @($goods | ForEach-Object { "- $_" })
  $reportLines += ''
  $reportLines += 'Warnings:'
  $reportLines += @($(if ($warnings.Count -gt 0) { $warnings } else { @('none') }) | ForEach-Object { "- $_" })
  $reportLines += ''
  $reportLines += 'Errors:'
  $reportLines += @($(if ($errors.Count -gt 0) { $errors } else { @('none') }) | ForEach-Object { "- $_" })

  $reportLines -join [Environment]::NewLine | Set-Content -Path $healthFile -Encoding UTF8

  [pscustomobject]@{
    result = $result
    errors = @($errors)
    warnings = @($warnings)
    goods = @($goods)
    checked_at = (Get-Date).ToString('o')
  } | ConvertTo-Json -Depth 6 | Set-Content -Path $lastHealthResultFile -Encoding UTF8

  Write-Host ("  Health check: {0}" -f $result) -ForegroundColor $(switch ($result) { 'pass' { 'Green' } 'warn' { 'Yellow' } default { 'Red' } })
  Write-Host ("  Health report: {0}" -f $healthFile) -ForegroundColor DarkGray

  if ($result -eq 'fail') {
    throw ("AgentOps health check failed. Inspect {0}" -f $healthFile)
  }
}

function Get-GitStatusPaths {
  Push-Location $root
  try {
    $status = @(& git status --porcelain=1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }

    $items = @()
    foreach ($line in $status) {
      if ([string]::IsNullOrWhiteSpace($line)) { continue }
      if ($line.Length -lt 4) { continue }

      $pathPart = $line.Substring(3).Trim()
      if ($pathPart -match ' -> ') {
        $pathPart = ($pathPart -split ' -> ')[-1]
      }

      if (-not [string]::IsNullOrWhiteSpace($pathPart)) {
        $items += $pathPart.Replace('\', '/')
      }
    }

    return $items
  } finally {
    Pop-Location
  }
}

function Get-GitStatusEntries {
  Push-Location $root
  try {
    $status = @(& git status --porcelain=1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }

    $items = @()
    foreach ($line in $status) {
      if ([string]::IsNullOrWhiteSpace($line)) { continue }
      if ($line.Length -lt 4) { continue }

      $statusCode = $line.Substring(0, 2)
      $pathPart = $line.Substring(3).Trim()
      if ($pathPart -match ' -> ') {
        $pathPart = ($pathPart -split ' -> ')[-1]
      }

      if (-not [string]::IsNullOrWhiteSpace($pathPart)) {
        $items += [pscustomobject]@{
          Status = $statusCode
          Path = $pathPart.Replace('\', '/')
        }
      }
    }

    return @($items)
  } finally {
    Pop-Location
  }
}

function Test-IsGeneratedPath($path) {
  $normalized = ($path -replace '\\', '/').Trim()

  $generatedPatterns = @(
    '^bin/',
    '^obj/',
    '^AgentOps/CURRENT_TASK\.md$',
    '^AgentOps/STATUS\.md$',
    '^AgentOps/PROGRESS\.md$',
    '^AgentOps/RUN_SUMMARY\.md$',
    '^AgentOps/HEARTBEAT\.md$',
    '^AgentOps/HEARTBEAT_STATE\.json$',
    '^AgentOps/HEARTBEAT\.log$',
    '^AgentOps/HEARTBEAT\.lock$',
    '^AgentOps/HEARTBEAT_LAST_WORKER\.log$',
    '^AgentOps/HEARTBEAT_LAST_WORKER\.err$',
    '^AgentOps/APP_BEHAVIOR\.md$',
    '^AgentOps/HEALTH\.md$',
    '^AgentOps/RUNNER\.lock$',
    '^AgentOps/LAST_CODEX_PROMPT\.md$',
    '^AgentOps/LAST_CODEX_STDOUT\.log$',
    '^AgentOps/LAST_CODEX_STDERR\.log$',
    '^AgentOps/LAST_APP_BEHAVIOR\.json$',
    '^AgentOps/LAST_HEALTH_RESULT\.json$',
    '^AgentOps/LAST_QUEUE_AUTOWRITE\.md$',
    '^AgentOps/build_result\.json$',
    '^AgentOps/current_task_prompt\.md$',
    '^AgentOps/last_agent_failure\.log$',
    '^AgentOps/spawner\.state\.json$',
    '^AgentOps/codex-home/',
    '^AgentOps/runner/.*\.log(\.err)?$',
    '^bin/.*/logs/runtime-errors\.log$',
    '^dist/.*/logs/runtime-errors\.log$',
    '^bin/.*/webview2-data/'
  )

  foreach ($pattern in $generatedPatterns) {
    if ($normalized -match $pattern) {
      return $true
    }
  }

  return $false
}

function Get-RelevantGitPaths {
  $paths = @(Get-GitStatusPaths)
  return @($paths | Where-Object { -not (Test-IsGeneratedPath $_) })
}

function Get-RelevantGitStatusEntries {
  return @(Get-GitStatusEntries | Where-Object { -not (Test-IsGeneratedPath $_.Path) })
}

function Get-TextLineCount($path) {
  if (-not (Test-Path $path)) { return 0 }

  try {
    return @((Get-Content -LiteralPath $path -Encoding UTF8 -ErrorAction Stop)).Count
  } catch {
    return 0
  }
}

function Get-RelevantChangeStats {
  $entries = @(Get-RelevantGitStatusEntries)
  $paths = @($entries | ForEach-Object { $_.Path } | Select-Object -Unique)
  $files = New-Object System.Collections.Generic.List[object]
  $added = 0
  $removed = 0
  $binary = 0

  Push-Location $root
  try {
    if ($paths.Count -gt 0) {
      $numstat = @(& git diff HEAD --numstat -- $paths)
      foreach ($line in $numstat) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`t"
        if ($parts.Count -lt 3) { continue }

        $path = $parts[2].Replace('\', '/')
        if ($parts[0] -eq '-' -or $parts[1] -eq '-') {
          $binary++
          $files.Add([pscustomobject]@{ Path = $path; Added = '-'; Removed = '-'; Kind = 'binary' }) | Out-Null
          continue
        }

        $a = [int]$parts[0]
        $r = [int]$parts[1]
        $added += $a
        $removed += $r
        $files.Add([pscustomobject]@{ Path = $path; Added = $a; Removed = $r; Kind = 'tracked' }) | Out-Null
      }

      $diffPaths = @($files | ForEach-Object { $_.Path })
      $untrackedEntries = @($entries | Where-Object { $_.Status -eq '??' -and $diffPaths -notcontains $_.Path })
      foreach ($entry in $untrackedEntries) {
        $fullPath = Join-Path $root $entry.Path
        if (Test-Path $fullPath -PathType Leaf) {
          $lineCount = Get-TextLineCount $fullPath
          $added += $lineCount
          $files.Add([pscustomobject]@{ Path = $entry.Path; Added = $lineCount; Removed = 0; Kind = 'untracked' }) | Out-Null
        }
      }
    }
  } finally {
    Pop-Location
  }

  return [pscustomobject]@{
    Files = $files.ToArray()
    FileCount = $paths.Count
    Added = $added
    Removed = $removed
    Binary = $binary
    IgnoredRuntimeCount = (@(Get-GitStatusEntries).Count - $entries.Count)
  }
}

function Show-ChangeSummary($label) {
  $stats = Get-RelevantChangeStats
  Write-Host ("  {0}: {1} relevant file(s), +{2} -{3}" -f $label, $stats.FileCount, $stats.Added, $stats.Removed) -ForegroundColor Cyan
  if ($stats.Binary -gt 0) {
    Write-Host ("    Binary changed files: {0}" -f $stats.Binary) -ForegroundColor DarkGray
  }
  if ($stats.IgnoredRuntimeCount -gt 0) {
    Write-Host ("    Ignored generated/build/runtime paths: {0}" -f $stats.IgnoredRuntimeCount) -ForegroundColor DarkGray
  }

  foreach ($file in @($stats.Files | Select-Object -First 12)) {
    Write-Host ("    +{0} -{1} {2}" -f $file.Added, $file.Removed, $file.Path) -ForegroundColor DarkGray
  }
  if ($stats.Files.Count -gt 12) {
    Write-Host ("    ... {0} more file(s)" -f ($stats.Files.Count - 12)) -ForegroundColor DarkGray
  }

  return $stats
}

function Show-FilePreview($label, $path, [int]$lineCount = 12) {
  if (-not (Test-Path $path)) {
    Write-Host ("  {0}: missing ({1})" -f $label, $path) -ForegroundColor Yellow
    return
  }

  $lines = @(Get-Content $path -Encoding UTF8 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First $lineCount)
  if ($lines.Count -eq 0) {
    Write-Host ("  {0}: empty ({1})" -f $label, $path) -ForegroundColor Yellow
    return
  }

  Write-Host ("  {0}: {1}" -f $label, $path) -ForegroundColor DarkGray
  foreach ($line in $lines) {
    Write-Host ("    {0}" -f $line) -ForegroundColor DarkGray
  }
}

function Read-JsonFile($path) {
  if (-not (Test-Path $path)) { return $null }

  try {
    return Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
  } catch {
    return $null
  }
}

function Ensure-StrictDoneEntry($task) {
  $testResult = Read-JsonFile $lastTestResultFile
  $verifyResult = Read-JsonFile $lastVerifyResultFile
  $stats = Get-RelevantChangeStats
  $today = Get-Date -Format 'yyyy-MM-dd'

  $buildLine = 'build evidence unavailable'
  if ($null -ne $testResult) {
    if ([bool]$testResult.build_success) {
      $buildLine = "build 0 errors, $($testResult.warnings) warnings"
    } else {
      $buildLine = "build failed in tester"
    }
  }

  $profile = if ($null -ne $testResult -and $testResult.profile) { [string]$testResult.profile } else { 'unknown profile' }
  $verifier = if ($null -ne $verifyResult -and $verifyResult.result) { [string]$verifyResult.result } else { 'unknown' }
  $entry = "- $($task.Number) - $($task.Title) ($today) - runner verified; queue marked [x]; tester profile '$profile'; verifier '$verifier'; $buildLine; relevant files $($stats.FileCount), lines +$($stats.Added) -$($stats.Removed); commit created by runner with this entry."

  $lines = if (Test-Path $doneFile) { @(Get-Content $doneFile -Encoding UTF8) } else { @('# Done', '') }
  if ($lines.Count -eq 0) {
    $lines = @('# Done', '')
  }
  if ($lines[0] -notmatch '^# Done') {
    $lines = @('# Done', '') + $lines
  }

  $taskPattern = "^\s*(-|done:)\s*$([regex]::Escape([string]$task.Number))\b"
  $filtered = @($lines | Where-Object { $_ -notmatch $taskPattern })
  $filtered += $entry
  $filtered += ''
  Set-Content -Path $doneFile -Value $filtered -Encoding UTF8
  Write-Host ("  DONE.md updated with verified task entry: {0}" -f $task.Number) -ForegroundColor Green
}

function Git-HasChanges {
  return @(Get-RelevantGitPaths).Count -gt 0
}

function Git-CommitIfDirty($message) {
  Push-Location $root
  try {
    $paths = @(Get-RelevantGitPaths)
    if ($paths.Count -eq 0) {
      Write-WorkerLog "SKIP git commit: no relevant changes for '$message'"
      return $false
    }

    [void](Show-ChangeSummary 'Committing changes')
    Write-WorkerLog ("CMD git add -A -- {0}" -f ($paths -join ' '))
    & git add -A -- $paths
    if ($LASTEXITCODE -ne 0) {
      Write-WorkerLog ("EXIT git add = {0}" -f $LASTEXITCODE)
      $indexLock = Join-Path $root '.git\index.lock'
      if (Test-Path $indexLock) {
        throw ("git add failed because .git/index.lock is present: {0}" -f $indexLock)
      }

      throw 'git add failed. Another git process or a permissions policy is blocking writes to .git.'
    }
    Write-WorkerLog 'EXIT git add = 0'

    Write-WorkerLog ("CMD git commit -m ""{0}""" -f $message)
    & git commit -m $message
    Write-WorkerLog ("EXIT git commit = {0}" -f $LASTEXITCODE)
    if ($LASTEXITCODE -ne 0) { throw ("git commit failed: {0}" -f $message) }

    Write-Host ("    Commit created: {0}" -f $message) -ForegroundColor Green
    return $true
  } finally {
    Pop-Location
  }
}

function Git-CheckpointBeforeTask($task) {
  if (-not (Git-HasChanges)) {
    Show-ChangeSummary 'Pre-task working tree'
    return
  }

  $checkpointMessage = "agentops: checkpoint before task {0} - {1}" -f $task.Number, $task.Title
  Write-Host '  Working tree is dirty. Creating checkpoint commit before task...' -ForegroundColor Yellow
  [void](Git-CommitIfDirty $checkpointMessage)
}

function Normalize-TaskKey($text) {
  if ([string]::IsNullOrWhiteSpace($text)) {
    return ''
  }

  return (($text.ToLowerInvariant() -replace '[^a-z0-9]+', ' ').Trim())
}

function Convert-ToSlug($text) {
  if ([string]::IsNullOrWhiteSpace($text)) {
    return 'task'
  }

  $normalized = $text.Normalize([Text.NormalizationForm]::FormD)
  $builder = New-Object System.Text.StringBuilder
  foreach ($char in $normalized.ToCharArray()) {
    if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($char) -eq [Globalization.UnicodeCategory]::NonSpacingMark) {
      continue
    }
    [void]$builder.Append($char)
  }

  $slug = $builder.ToString().ToLowerInvariant()
  $slug = $slug -replace '[^a-z0-9]+', '-'
  $slug = $slug.Trim('-')
  if ([string]::IsNullOrWhiteSpace($slug)) {
    return 'task'
  }

  return $slug
}

function Get-ExistingQueueKeys {
  if (-not (Test-Path $queueFile)) {
    return @()
  }

  $keys = New-Object System.Collections.Generic.List[string]
  foreach ($line in (Get-Content $queueFile -Encoding UTF8)) {
    if ($line -notmatch '^- \[[ x]\] (?<number>\d+)\s+') {
      continue
    }

    $title = $line -replace '^- \[[ x]\] \d+\s*', ''
    $specMatch = [regex]::Match($title, 'TASKS/[A-Za-z0-9_\-/.]+\.md')
    if ($specMatch.Success) {
      $specIndex = $title.IndexOf($specMatch.Value)
      if ($specIndex -ge 0) {
        $title = $title.Substring(0, $specIndex)
      }
    }

    $title = $title.Trim()
    $title = $title -replace '^[^\p{L}\p{N}]+' , ''
    $title = $title -replace '[\s\p{Pd}\p{Po}]+$', ''
    $key = Normalize-TaskKey $title
    if (-not [string]::IsNullOrWhiteSpace($key)) {
      $keys.Add($key) | Out-Null
    }
  }

  return @($keys | Select-Object -Unique)
}

function Get-NextAvailableTaskNumber {
  $numbers = New-Object System.Collections.Generic.List[int]

  if (Test-Path $queueFile) {
    foreach ($line in (Get-Content $queueFile -Encoding UTF8)) {
      if ($line -match '^- \[[ x]\] (?<number>\d+)\s+') {
        $numbers.Add([int]$matches['number']) | Out-Null
      }
    }
  }

  foreach ($file in (Get-ChildItem -Path $tasksRoot -File -Filter '*.md' -ErrorAction SilentlyContinue)) {
    if ($file.BaseName -match '^(?<number>\d+)-') {
      $numbers.Add([int]$matches['number']) | Out-Null
    }
  }

  if ($numbers.Count -eq 0) {
    return 1
  }

  return ((@($numbers | Measure-Object -Maximum).Maximum) + 1)
}

function Get-TaskSourceEntries {
  if (-not (Test-Path $taskSourceFile)) {
    return @()
  }

  $lines = @(Get-Content $taskSourceFile -Encoding UTF8)
  $items = New-Object System.Collections.Generic.List[object]

  for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line.TrimStart().StartsWith('#')) { continue }

    $isDone = $false
    $text = $null

    $checkboxMatch = [regex]::Match($line, '^\s*-\s*\[(?<state>[ xX])\]\s*(?<text>.+)$')
    $bulletMatch = [regex]::Match($line, '^\s*[-*]\s+(?<text>.+)$')
    $numberedMatch = [regex]::Match($line, '^\s*\d+\.\s+(?<text>.+)$')

    if ($checkboxMatch.Success) {
      $isDone = ($checkboxMatch.Groups['state'].Value -match '[xX]')
      $text = $checkboxMatch.Groups['text'].Value.Trim()
    } elseif ($bulletMatch.Success) {
      $text = $bulletMatch.Groups['text'].Value.Trim()
    } elseif ($numberedMatch.Success) {
      $text = $numberedMatch.Groups['text'].Value.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($text)) { continue }

    $items.Add([pscustomobject]@{
      Index = $i
      Text = $text
      Done = $isDone
    }) | Out-Null
  }

  return $items.ToArray()
}

function Get-SeedGoalSummary($text) {
  $trimmed = $text.Trim()
  if ($trimmed.Length -le 140) {
    return $trimmed
  }

  return ($trimmed.Substring(0, 137).TrimEnd() + '...')
}

function Build-AutoTaskSpec($number, $title, $seedText) {
  $pseudoTask = [pscustomobject]@{ Number = $number; Title = $title }
  $profile = @(Get-TaskProfileFromSpec $pseudoTask $seedText)
  $paths = @(Get-InferredPathHints $pseudoTask $seedText $profile)
  $acceptance = @(Get-TitleDerivedAcceptance $pseudoTask)
  $plan = @(Get-ImplementationPlan $pseudoTask $profile $true)
  $verify = @(Get-VerificationFocus $pseudoTask $profile $seedText)
  $antiRegression = @(Get-AntiRegressionTargets $seedText)

  $profileBlock = ($profile | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  $pathsBlock = if ($paths.Count -gt 0) {
    ($paths | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  } else {
    '- Inspect the existing code and keep the edit scope small.'
  }
  $acceptanceBlock = ($acceptance | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  $planBlock = ($plan | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  $verifyBlock = ($verify | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  $antiRegressionBlock = ($antiRegression | ForEach-Object { "- $_" }) -join [Environment]::NewLine

  return @"
# Task $number - $title

## Seed brief
$seedText

## Goal
$(Get-SeedGoalSummary $seedText)

## Profile
$profileBlock

## Likely change areas
$pathsBlock

## Suggested execution order
$planBlock

## Acceptance targets
$acceptanceBlock

## Verification focus
$verifyBlock

## Anti-regression
$antiRegressionBlock

## Notes
- This spec was auto-generated from AgentOps/TASK_SOURCE.md.
- Keep the change set local and stable.
- Update AgentOps/TASK_QUEUE.md, LOG.md, and HANDOFF.md when the task is truly complete. The runner owns DONE.md after verification.
"@
}

function Add-QueueEntries([string[]]$newEntries) {
  if ($newEntries.Count -eq 0) {
    return
  }

  $lines = New-Object System.Collections.Generic.List[string]
  if (Test-Path $queueFile) {
    foreach ($line in (Get-Content $queueFile -Encoding UTF8)) {
      $lines.Add($line) | Out-Null
    }
  }

  $insertIndex = $lines.Count
  for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^# Backlog') {
      $insertIndex = $i
      break
    }
  }

  if ($insertIndex -gt 0 -and -not [string]::IsNullOrWhiteSpace($lines[$insertIndex - 1])) {
    $lines.Insert($insertIndex, '')
    $insertIndex++
  }

  foreach ($entry in $newEntries) {
    $lines.Insert($insertIndex, $entry)
    $insertIndex++
  }

  Set-Content -Path $queueFile -Value $lines -Encoding UTF8
}

function Build-FinalUiPolishSpec($number) {
  return @"
# Task $number - Final UI polish pass

## Goal
Bring the studio to a presentable CAD/engineering-tool baseline after the current functional cycle is otherwise in place.

This is a rare final polish stage. It should not run after every feature task.

## Scope
- Tighten existing UI alignment, spacing, hierarchy, icon consistency, tooltips, theme parity, and CAD-shell readability.
- Walk the visible studio shell: top toolbar, left feature tree, viewport chrome, right assistant/properties panel, status bar, and currently wired dialogs.
- Keep the viewport dominant and CAD-like.
- Preserve existing behavior and controls.

## Out of scope
- Do not add new CAD features.
- Do not redesign the product shell.
- Do not rewrite architecture.
- Do not remove working primitives, sketch tools, feature commands, or assistant fallback behavior.
- Do not touch backend logic unless a UI binding requires a tiny safe fix.

## Acceptance
- The app still builds with 0 errors.
- Primary controls remain icon-led and compact.
- Light and dark themes both look intentionally styled.
- Feature tree, toolbar, viewport, dialogs, and side panels look consistent.
- No placeholder/debug labels are visible in the main UI.
- No viewport, primitive, sketch, or feature workflow regressions.

## Verification
- Run dotnet build --nologo -v minimal.
- If possible, launch the app and record APP_BEHAVIOR.md notes for the visible surfaces checked.
- Mark the queue item complete only when the UI polish is real and the app still behaves correctly.
"@
}

function Ensure-FinalUiPolishStage {
  $status = Get-UiPolishStageStatus
  if ($status.Exists) {
    return 0
  }

  $nextNumber = Get-NextAvailableTaskNumber
  $specFileName = ('{0:D2}-final-ui-polish-pass.md' -f ([int]$nextNumber))
  $specFilePath = Join-Path $tasksRoot $specFileName
  if (-not (Test-Path $specFilePath)) {
    Set-Content -Path $specFilePath -Value (Build-FinalUiPolishSpec $nextNumber) -Encoding UTF8
  }

  Add-Content -Path $queueFile -Value ('- [ ] {0:D2} - Final UI polish pass -> TASKS/{1}' -f ([int]$nextNumber), $specFileName) -Encoding UTF8
  Write-Host ("  Added rare final UI polish stage: task {0:D2}" -f ([int]$nextNumber)) -ForegroundColor Green
  return 1
}

function AutoWrite-QueueFromSource {
  if (-not (Test-Path $taskSourceFile)) {
    return 0
  }

  $sourceLines = @((Get-Content $taskSourceFile -Encoding UTF8))
  $entries = @(Get-TaskSourceEntries)
  $existingKeys = @(Get-ExistingQueueKeys)
  $nextNumber = Get-NextAvailableTaskNumber
  $queueAdds = New-Object System.Collections.Generic.List[string]
  $report = New-Object System.Collections.Generic.List[string]
  $createdCount = 0

  foreach ($entry in $entries) {
    if ($entry.Done) {
      continue
    }

    $title = $entry.Text.Trim()
    $title = $title -replace '^[^\p{L}\p{N}]+' , ''
    $title = $title -replace '[\s\p{Pd}\p{Po}]+$', ''
    if ([string]::IsNullOrWhiteSpace($title)) {
      continue
    }

    $key = Normalize-TaskKey $title
    $sourceLines[$entry.Index] = "- [x] $($entry.Text)"

    if ($existingKeys -contains $key) {
      $report.Add(("- skipped duplicate seed: {0}" -f $title)) | Out-Null
      continue
    }

    $slug = Convert-ToSlug $title
    $specFileName = ('{0:D2}-{1}.md' -f ([int]$nextNumber), $slug)
    $specFilePath = Join-Path $tasksRoot $specFileName

    while (Test-Path $specFilePath) {
      $nextNumber++
      $specFileName = ('{0:D2}-{1}.md' -f ([int]$nextNumber), $slug)
      $specFilePath = Join-Path $tasksRoot $specFileName
    }

    $specContent = Build-AutoTaskSpec $nextNumber $title $entry.Text
    Set-Content -Path $specFilePath -Value $specContent -Encoding UTF8
    $queueAdds.Add(('- [ ] {0:D2} - {1} -> TASKS/{2}' -f ([int]$nextNumber), $title, $specFileName)) | Out-Null
    $report.Add(("- created task {0:D2}: {1}" -f ([int]$nextNumber), $title)) | Out-Null
    $existingKeys += $key
    $createdCount++
    $nextNumber++
  }

  if ($createdCount -gt 0) {
    Add-QueueEntries @($queueAdds)
    Set-Content -Path $taskSourceFile -Value $sourceLines -Encoding UTF8
    Write-Host ("  Auto-writer created {0} queue item(s) from TASK_SOURCE.md." -f $createdCount) -ForegroundColor Green
  } else {
    Write-Host '  Auto-writer found no new queue seeds.' -ForegroundColor DarkGray
  }

  $summary = @(
    "# Queue autowriter - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ''
    ("Created: {0}" -f $createdCount)
    ''
  ) + @($report)
  $summary -join [Environment]::NewLine | Set-Content -Path $queueAutowriteFile -Encoding UTF8
  return $createdCount
}

function Get-NextTask {
  $lines = Get-Content $queueFile -Encoding UTF8

  foreach ($line in $lines) {
    if ($line -notmatch '^- \[ \] (?<number>\d+)\s+') {
      continue
    }

    $taskNumber = $matches['number']
    $specMatch = [regex]::Match($line, 'TASKS/[A-Za-z0-9_\-/.]+\.md')
    $specRelative = if ($specMatch.Success) { $specMatch.Value } else { $null }

    $title = $line -replace '^- \[ \] \d+\s*', ''
    if ($specRelative) {
      $specIndex = $title.IndexOf($specRelative)
      if ($specIndex -ge 0) {
        $title = $title.Substring(0, $specIndex)
      }
    }

    $title = $title.Trim()
    $title = $title -replace '^[^\p{L}\p{N}]+' , ''
    $title = $title -replace '[\s\p{Pd}\p{Po}]+$', ''
    if ([string]::IsNullOrWhiteSpace($title)) {
      $title = "task $taskNumber"
    }

    return [pscustomobject]@{
      Number = $taskNumber
      Title = $title
      SpecRelative = $specRelative
    }
  }

  return $null
}

function Resolve-SpecFile($task) {
  $candidates = New-Object System.Collections.Generic.List[string]

  if (-not [string]::IsNullOrWhiteSpace($task.SpecRelative)) {
    $relative = $task.SpecRelative -replace '/', [string][IO.Path]::DirectorySeparatorChar
    $candidates.Add((Join-Path $agentOpsRoot $relative))
    $candidates.Add((Join-Path $root $relative))
  }

  $taskPrefix = "{0:D2}" -f [int]$task.Number
  $candidates.Add((Join-Path $tasksRoot ("{0}-*.md" -f $taskPrefix)))

  foreach ($candidate in $candidates) {
    if ([string]::IsNullOrWhiteSpace($candidate)) {
      continue
    }

    if ($candidate.Contains('*') -or $candidate.Contains('?')) {
      $match = Get-ChildItem -Path $candidate -ErrorAction SilentlyContinue | Select-Object -First 1
      if ($match) { return $match.FullName }
      continue
    }

    if (Test-Path $candidate) {
      return (Resolve-Path $candidate).Path
    }
  }

  throw ("Spec file could not be resolved for task {0} ({1})." -f $task.Number, $task.Title)
}

function Get-TaskProfileFromSpec($task, $specContent) {
  $text = ("{0}`n{1}" -f $task.Title, $specContent).ToLowerInvariant()
  $areas = New-Object System.Collections.Generic.List[string]

  if ($text -match 'agentops|spawn-codex|spawn-nexttask|orchestrator|automation|runner|\.bat|\.ps1|task_queue|done\.md|log\.md') {
    $areas.Add('AgentOps automation') | Out-Null
  }
  if ($text -match 'avaloniaapp|toolbar|dialog|tree|assistant|panel|theme|button|axaml|viewport|ui|layout|tooltip') {
    $areas.Add('Avalonia UI shell') | Out-Null
  }
  if ($text -match 'engine|geometry|backend|cadmodel|cadprojectstore|solid|profile|extrude|revolve|csg|mesh') {
    $areas.Add('CAD backend / geometry') | Out-Null
  }
  if ($text -match 'test|verify|build|smoke|status|commit|git') {
    $areas.Add('Validation / safety flow') | Out-Null
  }

  if ($areas.Count -eq 0) {
    $areas.Add('General implementation') | Out-Null
  }

  return @($areas | Select-Object -Unique)
}

function Get-SpecMentionedPaths($specContent) {
  $matches = [regex]::Matches($specContent, '(?im)(AgentOps|AvaloniaApp|Engine|Core|Backends|Assets)[/\\][A-Za-z0-9_.\-/\\]+')
  $items = New-Object System.Collections.Generic.List[string]

  foreach ($match in $matches) {
    $value = $match.Value -replace '\\', '/'
    if (-not [string]::IsNullOrWhiteSpace($value)) {
      $items.Add($value) | Out-Null
    }
  }

  return @($items | Select-Object -Unique | Select-Object -First 10)
}

function Get-ChecklistLines($specContent, [string[]]$headingPatterns) {
  $lines = $specContent -split "\r?\n"
  $capture = $false
  $items = New-Object System.Collections.Generic.List[string]

  foreach ($rawLine in $lines) {
    $line = $rawLine.TrimEnd()

    if ($line -match '^\s*#{1,6}\s+') {
      $capture = $false
      foreach ($pattern in $headingPatterns) {
        if ($line -match $pattern) {
          $capture = $true
          break
        }
      }
      continue
    }

    if (-not $capture) { continue }

    if ($line -match '^\s*[-*]\s+\[[ xX]\]\s*(.+)$') {
      $items.Add($matches[1].Trim()) | Out-Null
      continue
    }
    if ($line -match '^\s*[-*]\s+(.+)$') {
      $items.Add($matches[1].Trim()) | Out-Null
      continue
    }
    if ($line -match '^\s*\d+\.\s+(.+)$') {
      $items.Add($matches[1].Trim()) | Out-Null
      continue
    }
  }

  return @($items | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
}

function Get-AcceptanceTargets($specContent) {
  $patterns = @(
    '^(##\s*)?(Expected|Expected behavior|Expected result|Acceptance|Verifier checklist|Done when|Requirements)\b',
    '^(##\s*)?How .* should work\b'
  )
  $items = @(Get-ChecklistLines $specContent $patterns)
  if ($items.Count -gt 0) {
    return @($items | Select-Object -First 12)
  }

  $fallback = New-Object System.Collections.Generic.List[string]
  foreach ($line in ($specContent -split "\r?\n")) {
    $trimmed = $line.Trim()
    if ($trimmed -match '^(Goal|Task|Required|Important|Target):\s*(.+)$') {
      $fallback.Add($matches[2].Trim()) | Out-Null
    }
  }
  return @($fallback | Select-Object -Unique | Select-Object -First 8)
}

function Get-AntiRegressionTargets($specContent) {
  $lines = $specContent -split "\r?\n"
  $items = New-Object System.Collections.Generic.List[string]

  foreach ($line in $lines) {
    $trimmed = $line.Trim()
    if ($trimmed -match '^(Do not|Do NOT|DON''T|Never|Preserve|Keep)\b(.+)$') {
      $items.Add($trimmed) | Out-Null
    }
  }

  $defaults = @(
    'Do not start the next task.',
    'Do not redesign the whole app.',
    'Do not remove working features.',
    'Do not break viewport behavior.',
    'Do not reintroduce grid.',
    'Do not add unsupported Avalonia properties.',
    'Do not touch temp_verify_* folders.',
    'Do not add NuGet packages unless explicitly required.'
  )

  foreach ($item in $defaults) {
    $items.Add($item) | Out-Null
  }

  return @($items | Select-Object -Unique | Select-Object -First 14)
}

function Get-SpecStats($specContent) {
  $lines = @($specContent -split "\r?\n")
  $nonEmptyLines = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
  $wordCount = 0

  foreach ($line in $nonEmptyLines) {
    $parts = @($line.Trim() -split '\s+' | Where-Object { $_ })
    $wordCount += $parts.Count
  }

  return [pscustomobject]@{
    NonEmptyLineCount = $nonEmptyLines.Count
    WordCount = $wordCount
    CharacterCount = $specContent.Length
  }
}

function Test-IsTerseSpec($specContent) {
  $stats = Get-SpecStats $specContent
  return ($stats.NonEmptyLineCount -lt 12 -or $stats.WordCount -lt 160)
}

function Get-TitleDerivedAcceptance($task) {
  $title = $task.Title.ToLowerInvariant()
  $items = New-Object System.Collections.Generic.List[string]

  if ($title -match '\bfix\b|\brestore\b|\brepair\b') {
    $items.Add('The named broken behavior is corrected without widening scope.') | Out-Null
  }
  if ($title -match '\badd\b|\bimplement\b|\bcreate\b|\bsupport\b') {
    $items.Add('The requested capability exists in the intended workflow, not as placeholder UI.') | Out-Null
  }
  if ($title -match '\bclean\b|\btighten\b|\bpolish\b') {
    $items.Add('The targeted surface is improved without redesigning unrelated areas.') | Out-Null
  }
  if ($title -match '\bverify\b|\btest\b|\bcheck\b') {
    $items.Add('The relevant verification step is added or passes with clear output.') | Out-Null
  }

  if ($items.Count -eq 0) {
    $items.Add('The task title is satisfied with the smallest correct change set.') | Out-Null
  }

  return @($items | Select-Object -Unique | Select-Object -First 4)
}

function Get-InferredPathHints($task, $specContent, [string[]]$profile) {
  $text = ("{0}`n{1}" -f $task.Title, $specContent).ToLowerInvariant()
  $items = New-Object System.Collections.Generic.List[string]

  $hintRules = @(
    [pscustomobject]@{
      Pattern = 'agentops|spawn-codex|spawn-nexttask|orchestrator|runner|task_queue|done\.md|log\.md|handoff|status\.md'
      Paths = @(
        'AgentOps/Spawn-Codex.ps1',
        'AgentOps/Spawn-Codex.bat',
        'AgentOps/Test-CodexTask.ps1',
        'AgentOps/Verify-CodexTask.ps1',
        'AgentOps/TASK_QUEUE.md',
        'AgentOps/HANDOFF.md'
      )
    },
    [pscustomobject]@{
      Pattern = 'toolbar|dialog|assistant|tree|panel|axaml|theme|tooltip|button|layout|viewport|sketch mode|3d mode'
      Paths = @(
        'AvaloniaApp/MainWindow.axaml',
        'AvaloniaApp/MainWindow.axaml.cs',
        'AvaloniaApp/ViewModels/StudioShellViewModel.cs',
        'AvaloniaApp/Dialogs/',
        'AvaloniaApp/Themes/'
      )
    },
    [pscustomobject]@{
      Pattern = 'sketch|profile|extrude|revolve|fillet|constraint|geometry|solid|cadmodel|cadprojectstore|body|plane'
      Paths = @(
        'Engine/CadModel.cs',
        'Engine/CadProjectStore.cs',
        'Engine/ProfileBuilder.cs',
        'Engine/SolidMesher.cs',
        'Core/Solids.cs'
      )
    },
    [pscustomobject]@{
      Pattern = 'icon|svg|asset'
      Paths = @(
        'Assets/Icons/',
        'AvaloniaApp/MainWindow.axaml',
        'AvaloniaApp/Themes/'
      )
    }
  )

  foreach ($rule in $hintRules) {
    if ($text -match $rule.Pattern) {
      foreach ($path in $rule.Paths) {
        $items.Add($path) | Out-Null
      }
    }
  }

  foreach ($area in $profile) {
    switch ($area) {
      'AgentOps automation' {
        foreach ($path in @(
          'AgentOps/Spawn-Codex.ps1',
          'AgentOps/Test-CodexTask.ps1',
          'AgentOps/Verify-CodexTask.ps1'
        )) { $items.Add($path) | Out-Null }
      }
      'Avalonia UI shell' {
        foreach ($path in @(
          'AvaloniaApp/MainWindow.axaml',
          'AvaloniaApp/MainWindow.axaml.cs',
          'AvaloniaApp/ViewModels/'
        )) { $items.Add($path) | Out-Null }
      }
      'CAD backend / geometry' {
        foreach ($path in @(
          'Engine/CadModel.cs',
          'Engine/CadProjectStore.cs',
          'Engine/ProfileBuilder.cs'
        )) { $items.Add($path) | Out-Null }
      }
    }
  }

  return @($items | Select-Object -Unique | Select-Object -First 12)
}

function Get-ImplementationPlan($task, [string[]]$profile, [bool]$isTerse) {
  $items = New-Object System.Collections.Generic.List[string]

  $items.Add('Inspect the current implementation in the likely target files before editing.') | Out-Null
  $items.Add('Prefer repairing and reconnecting existing code over inventing a new subsystem.') | Out-Null
  $items.Add('Keep the change set local to this task and preserve existing working behavior.') | Out-Null

  if ($profile -contains 'AgentOps automation') {
    $items.Add('Stay inside AgentOps scripts/support files unless the task explicitly requires broader changes.') | Out-Null
  }
  if ($profile -contains 'Avalonia UI shell') {
    $items.Add('Keep UI work in AvaloniaApp/ and avoid backend drift unless the wiring truly requires it.') | Out-Null
  }
  if ($profile -contains 'CAD backend / geometry') {
    $items.Add('Keep geometry logic in Engine/ or Core/ and do not move model truth into viewport code.') | Out-Null
  }
  if ($isTerse) {
    $items.Add('The source task text is terse, so infer intent conservatively from the title and existing code rather than broadening scope.') | Out-Null
  }

  $items.Add('Before finishing, update TASK_QUEUE.md, HANDOFF.md, and LOG.md exactly as requested. The runner writes DONE.md after verification.') | Out-Null
  return @($items | Select-Object -Unique | Select-Object -First 8)
}

function Get-VerificationFocus($task, [string[]]$profile, $specContent) {
  $items = New-Object System.Collections.Generic.List[string]
  $items.Add('Run dotnet build --nologo -v minimal and leave the build clean.') | Out-Null

  if ($profile -contains 'AgentOps automation') {
    $items.Add('Syntax-check any changed PowerShell scripts and keep the runner/log/status flow readable.') | Out-Null
    $items.Add('Do not touch CAD application code unless the task explicitly requires it.') | Out-Null
  }
  if ($profile -contains 'Avalonia UI shell') {
    $items.Add('Keep Avalonia bindings and AXAML valid; do not add unsupported Avalonia properties.') | Out-Null
  }
  if ($profile -contains 'CAD backend / geometry') {
    $items.Add('Keep geometry solid-first and preserve working viewport behavior.') | Out-Null
  }
  if ($specContent.ToLowerInvariant() -match 'tooltip|icon') {
    $items.Add('Primary tool controls should stay icon-led, with text reserved for tooltips or secondary labels.') | Out-Null
  }

  $items.Add('If the task cannot be completed safely, stop at the failing boundary and say exactly why.') | Out-Null
  return @($items | Select-Object -Unique | Select-Object -First 8)
}

function Get-TaskSynopsis($task, $specContent, [bool]$isTerse, [string[]]$profile) {
  $goals = New-Object System.Collections.Generic.List[string]

  foreach ($line in ($specContent -split "\r?\n")) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
    if ($trimmed -match '^#') { continue }
    if ($trimmed -match '^(Do not|Do NOT|DON''T|Never|Rules|Done when|Verification|Requirements)\b') { continue }
    $goals.Add($trimmed) | Out-Null
    if ($goals.Count -ge 3) { break }
  }

  if ($goals.Count -gt 0) {
    return @($goals)
  }

  $profileLabel = if ($profile.Count -gt 0) { ($profile -join ', ') } else { 'the current task area' }
  $summary = if ($isTerse) {
    "Implement task $($task.Number) from a terse spec by making the smallest correct change in $profileLabel."
  } else {
    "Implement task $($task.Number) exactly as titled, keeping the work local to $profileLabel."
  }

  return @($summary)
}

function Build-ExecutionBrief($task, $specContent, $specFile) {
  $profile = @(Get-TaskProfileFromSpec $task $specContent)
  $explicitPaths = @(Get-SpecMentionedPaths $specContent)
  $hintPaths = @(Get-InferredPathHints $task $specContent $profile)
  $paths = @($explicitPaths + $hintPaths | Select-Object -Unique)
  $acceptance = @(Get-AcceptanceTargets $specContent)
  if ($acceptance.Count -eq 0) {
    $acceptance = @(Get-TitleDerivedAcceptance $task)
  }
  $antiRegression = @(Get-AntiRegressionTargets $specContent)
  $isTerse = Test-IsTerseSpec $specContent

  $goalLines = @(Get-TaskSynopsis $task $specContent $isTerse $profile)

  return [pscustomobject]@{
    Profile = $profile
    MentionedPaths = $paths
    Acceptance = $acceptance
    AntiRegression = $antiRegression
    GoalSummary = @($goalLines)
    ImplementationPlan = @(Get-ImplementationPlan $task $profile $isTerse)
    VerificationFocus = @(Get-VerificationFocus $task $profile $specContent)
    IsTerse = $isTerse
    RelativeSpec = $specFile.Substring($root.Length).TrimStart([IO.Path]::DirectorySeparatorChar) -replace '\\', '/'
  }
}

function Get-RecommendedIterationLimit($requestedMaxIterations) {
  $openTasks = Get-OpenTaskCount
  if ($requestedMaxIterations -gt 0) {
    return [pscustomobject]@{
      Limit = $requestedMaxIterations
      OpenTasks = $openTasks
      Source = 'manual'
    }
  }

  $limit = if ($openTasks -gt 0) { [Math]::Max(1, $openTasks) } else { 1 }
  return [pscustomobject]@{
    Limit = $limit
    OpenTasks = $openTasks
    Source = 'auto'
  }
}

function Get-CodexTimeoutBudget($task, $specContent) {
  $profile = @(Get-TaskProfileFromSpec $task $specContent)
  $stats = Get-SpecStats $specContent
  $score = 1

  if ($stats.WordCount -ge 180) { $score++ }
  if ($stats.WordCount -ge 420) { $score++ }
  if ($profile.Count -ge 2) { $score++ }
  if (($task.Title + "`n" + $specContent).ToLowerInvariant() -match 'ui|behavior|viewport|toolbar|assistant|layout|visual|dialog') { $score++ }
  if (($task.Title + "`n" + $specContent).ToLowerInvariant() -match 'agentops|runner|queue|prompt|codex') { $score++ }

  $seconds = switch ($score) {
    { $_ -le 1 } { 900; break }
    2 { 1200; break }
    3 { 1500; break }
    4 { 1800; break }
    default { 2400; break }
  }

  return [pscustomobject]@{
    Seconds = $seconds
    Minutes = [Math]::Round($seconds / 60.0, 1)
    Score = $score
    Profile = $profile
    WordCount = $stats.WordCount
  }
}

function Build-Prompt($task, $specFile) {
  $specContent = Get-Content $specFile -Raw -Encoding UTF8
  $brief = Build-ExecutionBrief $task $specContent $specFile
  $relativeSpec = $brief.RelativeSpec

  $profileBlock = if ($brief.Profile.Count -gt 0) {
    ($brief.Profile | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  } else {
    '- General implementation'
  }

  $pathsBlock = if ($brief.MentionedPaths.Count -gt 0) {
    ($brief.MentionedPaths | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  } else {
    '- No explicit file paths named in the spec; inspect existing code and pick the smallest correct change set.'
  }

  $goalBlock = if ($brief.GoalSummary.Count -gt 0) {
    ($brief.GoalSummary | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  } else {
    "- Complete task $($task.Number) exactly as described in the spec."
  }

  $implementationBlock = if ($brief.ImplementationPlan.Count -gt 0) {
    ($brief.ImplementationPlan | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  } else {
    '- Inspect the current code, make the smallest correct change, and keep the work local to the task.'
  }

  $acceptanceBlock = if ($brief.Acceptance.Count -gt 0) {
    ($brief.Acceptance | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  } else {
    '- Build must pass.'
  }

  $verificationBlock = if ($brief.VerificationFocus.Count -gt 0) {
    ($brief.VerificationFocus | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  } else {
    '- Run the local verification steps that are possible for this task.'
  }

  $antiRegressionBlock = ($brief.AntiRegression | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  $uiPolishBlock = if (Test-IsFinalUiPolishText ($task.Title + "`n" + $specContent)) {
@"
- This task is the rare final UI polish stage for the current cycle.
- Improve existing visible UI quality, consistency, spacing, hierarchy, icon/tool states, theme parity, and CAD-shell readability.
- Do not add new CAD features, redesign the whole product, or hide/remove working controls to make the UI simpler.
- Preserve viewport behavior, primitives, sketch tools, feature commands, body/tree behavior, and assistant fallback behavior.
- Record which visible surfaces were checked in AgentOps/HANDOFF.md and AgentOps/LOG.md.
"@
  } else {
    '- Not active for this task. Do not perform incidental UI polish or redesign outside the requested task.'
  }

  $prompt = @"
You are working in the My3DApp project at:
$root

Implement only this task:
- Task number: $($task.Number)
- Task title: $($task.Title)
- Task spec file: $relativeSpec

Read these files first:
- AgentOps/CURRENT_TASK.md
- AgentOps/EXECUTOR_GUIDE.md
- AgentOps/HANDOFF.md

Execution brief distilled from the task:
Profile:
$profileBlock

Task density:
- Spec is $(if ($brief.IsTerse) { 'terse' } else { 'detailed enough to follow directly' }).
- If the task text leaves gaps, infer the smallest correct intent from the title, current code, and guardrails below.

Likely focus areas / files:
$pathsBlock

Primary outcome:
$goalBlock

Implementation plan:
$implementationBlock

Acceptance targets:
$acceptanceBlock

Verification focus:
$verificationBlock

Anti-regression guardrails:
$antiRegressionBlock

Final UI polish stage:
$uiPolishBlock

Task spec:
$specContent

Project rules:
- Work only on this task
- Do not start the next task
- Do not redesign the whole app
- Do not remove working features
- Do not break viewport behavior
- Do not reintroduce grid
- Do not add unsupported Avalonia properties
- Do not touch temp_verify_* folders
- Do not add NuGet packages unless the task explicitly requires it
- Preserve existing working functionality
- Build/test if possible before finishing

AgentOps completion rules:
- Mark this task as done in AgentOps/TASK_QUEUE.md by changing [ ] to [x] for task $($task.Number)
- Update AgentOps/HANDOFF.md using the executor guide format
- Append a short timestamped entry to AgentOps/LOG.md
- Do not append to AgentOps/DONE.md; the runner writes DONE.md only after tester and verifier acceptance
- Do not create a git commit
- Final report must be 15 lines or less

Important:
- UI files are in AvaloniaApp/
- Geometry/backend files are in Engine/
- Keep changes local and stable
"@

  Set-Content -Path $lastPromptFile -Value $prompt -Encoding UTF8
  return $prompt
}

function Write-CurrentTaskFile($task, $specFile) {
  $relativeSpec = $specFile.Substring($root.Length).TrimStart([IO.Path]::DirectorySeparatorChar) -replace '\\', '/'
  $specContent = Get-Content $specFile -Raw -Encoding UTF8
  $brief = Build-ExecutionBrief $task $specContent $specFile

  $briefBlock = @(
    'Execution brief:'
    'Profile:'
  )
  foreach ($item in $brief.Profile) { $briefBlock += "- $item" }
  $briefBlock += ''
  $briefBlock += ("Task density: {0}" -f $(if ($brief.IsTerse) { 'terse - use the derived brief conservatively' } else { 'detailed spec available' }))
  $briefBlock += ''
  $briefBlock += 'Likely focus areas / files:'
  foreach ($item in $brief.MentionedPaths) { $briefBlock += "- $item" }
  $briefBlock += ''
  $briefBlock += 'Implementation plan:'
  foreach ($item in $brief.ImplementationPlan) { $briefBlock += "- $item" }
  $briefBlock += ''
  $briefBlock += 'Acceptance targets:'
  foreach ($item in $brief.Acceptance) { $briefBlock += "- $item" }
  $briefBlock += ''
  $briefBlock += 'Verification focus:'
  foreach ($item in $brief.VerificationFocus) { $briefBlock += "- $item" }
  $briefBlock += ''
  $briefBlock += 'Anti-regression:'
  foreach ($item in $brief.AntiRegression) { $briefBlock += "- $item" }

  $content = @"
Task: $($task.Number) - $($task.Title)
Spec: $relativeSpec

Goal:
$specContent

$($briefBlock -join [Environment]::NewLine)

Rules:
- Implement only this task
- Do not start the next task
- Do not redesign the whole app
- Do not remove working features
- Do not break viewport
- Do not reintroduce grid
- Do not add unsupported Avalonia properties
- Do not touch temp_verify_* folders
- Do not add NuGet packages unless explicitly required
- Build/test if possible
- Do not create a git commit
"@

  Set-Content -Path $currentTaskFile -Value $content -Encoding UTF8
}

function Resolve-CodexCommand {
  foreach ($candidate in @('codex.cmd', 'codex')) {
    $command = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($command) {
      return $command.Source
    }
  }

  throw "Codex CLI not found on PATH. Expected 'codex.cmd' or 'codex'."
}

function Get-CodexFailureSummary($stdoutText, $stderrText, $exitCode) {
  $combined = @($stdoutText, $stderrText) -join [Environment]::NewLine
  if ([string]::IsNullOrWhiteSpace($combined)) {
    return "Codex exited with code $exitCode and produced no output."
  }

  $lines = $combined -split "\r?\n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }

  foreach ($line in $lines) {
    if ($line -match 'authentication required|not authenticated|login required') {
      return 'Codex is not authenticated. Run codex login first.'
    }
  }

  foreach ($line in $lines) {
    if ($line -match 'ConnectionRefused|Unable to connect to API|stream disconnected|websocket|api\.openai\.com') {
      return 'Codex could not reach its API endpoint. Check network/proxy/firewall.'
    }
  }

  foreach ($line in $lines) {
    if ($line -match 'permission denied|access is denied|unauthorized') {
      return $line
    }
  }

  foreach ($line in $lines) {
    if ($line -match 'ERROR|Error|error:|fatal|failed') {
      return $line
    }
  }

  return "Codex exited with code $exitCode."
}

function Run-Codex($prompt, [int]$TimeoutSeconds = 1800, $task = $null, [int]$StageIndex = 2, [int]$StageTotal = 6) {
  $codexCommand = Resolve-CodexCommand
  Write-Host ("  Codex command: {0}" -f $codexCommand) -ForegroundColor DarkGray
  Write-Host ("  Prompt file  : {0}" -f $lastPromptFile) -ForegroundColor DarkGray
  Write-Host ("  Stdout log   : {0}" -f $codexStdoutLog) -ForegroundColor DarkGray
  Write-Host ("  Stderr log   : {0}" -f $codexStderrLog) -ForegroundColor DarkGray
  Write-WorkerLog ("CMD codex exec < LAST_CODEX_PROMPT.md (cwd={0}, timeout={1}s, exe={2})" -f $root, $TimeoutSeconds, $codexCommand)

  Set-Content -Path $codexStdoutLog -Value '' -Encoding UTF8
  Set-Content -Path $codexStderrLog -Value '' -Encoding UTF8

  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $codexCommand
  $psi.Arguments = 'exec'
  $psi.WorkingDirectory = $root
  $psi.UseShellExecute = $false
  $psi.RedirectStandardInput = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.CreateNoWindow = $true

  $stdoutBuilder = New-Object System.Text.StringBuilder
  $stderrBuilder = New-Object System.Text.StringBuilder
  $script:AgentOpsCodexStdoutBuilder = $stdoutBuilder
  $script:AgentOpsCodexStderrBuilder = $stderrBuilder
  $script:AgentOpsCodexStdoutLogPath = $codexStdoutLog
  $script:AgentOpsCodexStderrLogPath = $codexStderrLog
  $script:AgentOpsCodexStdoutLines = 0
  $script:AgentOpsCodexStderrLines = 0

  $process = New-Object System.Diagnostics.Process
  $process.StartInfo = $psi
  $process.EnableRaisingEvents = $true

  $outputHandler = [System.Diagnostics.DataReceivedEventHandler]{
    param($sender, $eventArgs)
    if ($null -ne $eventArgs.Data) {
      [void]$script:AgentOpsCodexStdoutBuilder.AppendLine($eventArgs.Data)
      $script:AgentOpsCodexStdoutLines++
      Add-Content -Path $script:AgentOpsCodexStdoutLogPath -Value $eventArgs.Data -Encoding UTF8
      $display = [string]$eventArgs.Data
      if ($display.Length -gt 220) { $display = $display.Substring(0, 220) + '...' }
      Write-Host ("    codex> {0}" -f $display) -ForegroundColor DarkGray
    }
  }

  $errorHandler = [System.Diagnostics.DataReceivedEventHandler]{
    param($sender, $eventArgs)
    if ($null -ne $eventArgs.Data) {
      [void]$script:AgentOpsCodexStderrBuilder.AppendLine($eventArgs.Data)
      $script:AgentOpsCodexStderrLines++
      Add-Content -Path $script:AgentOpsCodexStderrLogPath -Value $eventArgs.Data -Encoding UTF8
      $display = [string]$eventArgs.Data
      if ($display.Length -gt 220) { $display = $display.Substring(0, 220) + '...' }
      Write-Host ("    codex! {0}" -f $display) -ForegroundColor DarkYellow
    }
  }

  $process.add_OutputDataReceived($outputHandler)
  $process.add_ErrorDataReceived($errorHandler)

  $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
  $heartbeatSeconds = 5
  $nextHeartbeat = $heartbeatSeconds
  $reportedOutput = $false

  try {
    if (-not $process.Start()) {
      throw 'Codex process failed to start.'
    }

    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
    $process.StandardInput.Write($prompt)
    $process.StandardInput.Close()

    Write-Host ("  Codex started (PID {0})" -f $process.Id) -ForegroundColor DarkCyan
    Write-Host '  Prompt sent through stdin for CLI stability.' -ForegroundColor DarkCyan
    Write-Host ("  Progress: stage {0}/{1}, Codex running, 0s / {2}s" -f $StageIndex, $StageTotal, $TimeoutSeconds) -ForegroundColor Cyan
    Write-ProgressSnapshot $task $StageIndex $StageTotal 'Codex executor' 'Codex process started.' 0 $TimeoutSeconds 0 0

    while (-not $process.WaitForExit(1000)) {
      $elapsed = [int][Math]::Floor($stopwatch.Elapsed.TotalSeconds)

      if (-not $reportedOutput -and (($stdoutBuilder.Length + $stderrBuilder.Length) -gt 0)) {
        Write-Host ("  Codex produced output after {0}s..." -f $elapsed) -ForegroundColor DarkGray
        $reportedOutput = $true
      }

      if ($elapsed -ge $nextHeartbeat) {
        Write-Host ("  Codex still running... {0}s / {1}s (stdout {2}, stderr {3})" -f $elapsed, $TimeoutSeconds, $script:AgentOpsCodexStdoutLines, $script:AgentOpsCodexStderrLines) -ForegroundColor DarkGray
        Write-ProgressSnapshot $task $StageIndex $StageTotal 'Codex executor' 'Codex is still running.' $elapsed $TimeoutSeconds $script:AgentOpsCodexStdoutLines $script:AgentOpsCodexStderrLines
        $nextHeartbeat += $heartbeatSeconds
      }

      if ($elapsed -ge $TimeoutSeconds) {
        try {
          $process.Kill()
        } catch {
        }
        throw ("Codex timed out after {0} seconds." -f $TimeoutSeconds)
      }
    }

    $process.WaitForExit()
    $stopwatch.Stop()

    $stdoutText = $stdoutBuilder.ToString().Trim()
    $stderrText = $stderrBuilder.ToString().Trim()
    $duration = [int][Math]::Round($stopwatch.Elapsed.TotalSeconds)

    Write-Host ("  Codex exit code: {0}" -f $process.ExitCode) -ForegroundColor $(if ($process.ExitCode -eq 0) { 'Green' } else { 'Red' })
    Write-Host ("  Codex duration : {0}s" -f $duration) -ForegroundColor DarkGray
    Write-Host ("  Codex output   : stdout {0} line(s), stderr {1} line(s)" -f $script:AgentOpsCodexStdoutLines, $script:AgentOpsCodexStderrLines) -ForegroundColor DarkGray
    Write-WorkerLog ("EXIT codex exec = {0} (duration={1}s, stdout={2}, stderr={3})" -f $process.ExitCode, $duration, $script:AgentOpsCodexStdoutLines, $script:AgentOpsCodexStderrLines)
    if ($script:AgentOpsCodexStdoutLines -eq 0 -and $script:AgentOpsCodexStderrLines -eq 0) {
      Write-Host '  Codex produced no output. Treating later queue/build/verifier checks as the source of truth.' -ForegroundColor Yellow
    }

    if ($process.ExitCode -ne 0) {
      $summary = Get-CodexFailureSummary $stdoutText $stderrText $process.ExitCode
      Write-Host ("  Codex failure: {0}" -f $summary) -ForegroundColor Red
      Write-ProgressSnapshot $task $StageIndex $StageTotal 'Codex executor' ("Codex failed: {0}" -f $summary) $duration $TimeoutSeconds $script:AgentOpsCodexStdoutLines $script:AgentOpsCodexStderrLines
      return $process.ExitCode
    }

    Write-Host ("  Codex finished in {0:n0}s" -f $stopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
    Write-ProgressSnapshot $task $StageIndex $StageTotal 'Codex executor' 'Codex completed successfully.' $duration $TimeoutSeconds $script:AgentOpsCodexStdoutLines $script:AgentOpsCodexStderrLines
    return 0
  } finally {
    try { $process.remove_OutputDataReceived($outputHandler) } catch {}
    try { $process.remove_ErrorDataReceived($errorHandler) } catch {}
    if ($process) { $process.Dispose() }
    Remove-Variable AgentOpsCodexStdoutBuilder -Scope Script -ErrorAction SilentlyContinue
    Remove-Variable AgentOpsCodexStderrBuilder -Scope Script -ErrorAction SilentlyContinue
    Remove-Variable AgentOpsCodexStdoutLogPath -Scope Script -ErrorAction SilentlyContinue
    Remove-Variable AgentOpsCodexStderrLogPath -Scope Script -ErrorAction SilentlyContinue
    Remove-Variable AgentOpsCodexStdoutLines -Scope Script -ErrorAction SilentlyContinue
    Remove-Variable AgentOpsCodexStderrLines -Scope Script -ErrorAction SilentlyContinue
  }
}

function Refresh-MainBuild {
  Write-Host '  Running build check...' -ForegroundColor Yellow
  Push-Location $root
  try {
    Write-WorkerLog 'CMD dotnet build --nologo -v minimal'
    & dotnet build --nologo -v minimal
    Write-WorkerLog ("EXIT dotnet build = {0}" -f $LASTEXITCODE)
    if ($LASTEXITCODE -ne 0) {
      Write-Host ("  Build failed with exit code {0}" -f $LASTEXITCODE) -ForegroundColor Red
      return $false
    }

    Write-Host '  Build passed.' -ForegroundColor Green
    return $true
  } finally {
    Pop-Location
  }
}

function Join-ProcessArguments {
  param([string[]]$Arguments)

  return (($Arguments | ForEach-Object {
    $arg = [string]$_
    if ($arg -match '[\s"]') {
      '"' + ($arg -replace '"', '\"') + '"'
    } else {
      $arg
    }
  }) -join ' ')
}

function Invoke-LoggedNoWindowProcess {
  param(
    [string]$Label,
    [string]$FileName,
    [string[]]$Arguments
  )

  $argumentText = Join-ProcessArguments $Arguments
  Write-Host ("  Running {0}..." -f $Label) -ForegroundColor Yellow
  Write-WorkerLog ("CMD {0} {1}" -f $FileName, $argumentText)

  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $FileName
  $psi.Arguments = $argumentText
  $psi.WorkingDirectory = $root
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.CreateNoWindow = $true

  $script:AgentOpsNoWindowLabel = $Label
  $script:AgentOpsNoWindowStdoutLines = 0
  $script:AgentOpsNoWindowStderrLines = 0

  $process = New-Object System.Diagnostics.Process
  $process.StartInfo = $psi
  $process.EnableRaisingEvents = $true

  $outputHandler = [System.Diagnostics.DataReceivedEventHandler]{
    param($sender, $eventArgs)
    if ($null -eq $eventArgs.Data) { return }
    $script:AgentOpsNoWindowStdoutLines++
    Write-Host ("    {0}> {1}" -f $script:AgentOpsNoWindowLabel, $eventArgs.Data) -ForegroundColor DarkGray
    Write-WorkerLog ("{0}> {1}" -f $script:AgentOpsNoWindowLabel, $eventArgs.Data)
  }

  $errorHandler = [System.Diagnostics.DataReceivedEventHandler]{
    param($sender, $eventArgs)
    if ($null -eq $eventArgs.Data) { return }
    $script:AgentOpsNoWindowStderrLines++
    Write-Host ("    {0}! {1}" -f $script:AgentOpsNoWindowLabel, $eventArgs.Data) -ForegroundColor Yellow
    Write-WorkerLog ("{0}! {1}" -f $script:AgentOpsNoWindowLabel, $eventArgs.Data)
  }

  [void]$process.add_OutputDataReceived($outputHandler)
  [void]$process.add_ErrorDataReceived($errorHandler)

  [void]$process.Start()
  $process.BeginOutputReadLine()
  $process.BeginErrorReadLine()

  while (-not $process.WaitForExit(1000)) {
    Write-Host ("    {0}: still running, stdout {1}, stderr {2}" -f $Label, $script:AgentOpsNoWindowStdoutLines, $script:AgentOpsNoWindowStderrLines) -ForegroundColor DarkGray
  }

  $process.WaitForExit()
  $exitCode = [int]$process.ExitCode
  Write-WorkerLog ("EXIT {0} = {1}" -f $Label, $exitCode)
  Write-Host ("  {0} exit code: {1}" -f $Label, $exitCode) -ForegroundColor $(if ($exitCode -eq 0) { 'Green' } else { 'Yellow' })
  return $exitCode
}

function Run-Tester($task) {
  if (-not (Test-Path $testerScript)) {
    throw "Tester script not found: $testerScript"
  }

  $powershellExe = (Get-Command powershell.exe -ErrorAction Stop).Source
  return Invoke-LoggedNoWindowProcess 'tester' $powershellExe @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $testerScript,
    '-TaskNumber',
    [string]$task.Number,
    '-TaskTitle',
    [string]$task.Title)
}

function Run-Verifier($task) {
  if (-not (Test-Path $verifierScript)) {
    throw "Verifier script not found: $verifierScript"
  }

  $powershellExe = (Get-Command powershell.exe -ErrorAction Stop).Source
  return Invoke-LoggedNoWindowProcess 'verifier' $powershellExe @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $verifierScript,
    '-TaskNumber',
    [string]$task.Number,
    '-TaskTitle',
    [string]$task.Title)
}

Show-Banner 'AgentOps Codex sequential runner - start'
Write-WorkerLog 'START Spawn-Codex.ps1'
Write-Host ("  Runner script: {0}" -f $runnerScriptPath) -ForegroundColor DarkGray
Write-Host ("  Runner script modified: {0}" -f $runnerScriptStamp) -ForegroundColor DarkGray
Ensure-AgentOpsFiles
Acquire-RunnerLock
Ensure-CodexFolders
Ensure-GitRepo
Ensure-GitIdentity
$autoAddedTasks = AutoWrite-QueueFromSource
$polishAddedTasks = Ensure-FinalUiPolishStage
Invoke-AgentOpsHealthCheck
Show-QueueStatus
Show-UiPolishStageStatus
if ($autoAddedTasks -gt 0) {
  Write-Host ("  Queue auto-writer expanded {0} new task(s)." -f $autoAddedTasks) -ForegroundColor Green
}
if ($polishAddedTasks -gt 0) {
  Write-Host '  Final UI polish stage was missing and has been queued once for this cycle.' -ForegroundColor Green
}
$stageTotal = 6
$iterationBudget = Get-RecommendedIterationLimit $MaxIterations
Write-Host ("  Iteration budget: {0} ({1}; {2} open task(s))" -f $iterationBudget.Limit, $iterationBudget.Source, $iterationBudget.OpenTasks) -ForegroundColor Yellow
Write-Status $null 'startup' ("AgentOps runner initialized. Iteration budget = {0} ({1})." -f $iterationBudget.Limit, $iterationBudget.Source) 'idle'
Write-ProgressSnapshot $null 0 $stageTotal 'Startup' 'Runner initialized and health check completed.'
[void](Show-ChangeSummary 'Startup working tree')

if ($PrepareOnly) {
  Write-Status $null 'prepared' 'Prepare-only run completed.' 'idle'
  Show-Banner 'Prepare-only run complete.'
  Release-RunnerLock
  exit 0
}

$iteration = 0

while ($iteration -lt $iterationBudget.Limit) {
  $task = Get-NextTask
  if ($null -eq $task) {
    Write-Status $null 'complete' 'Queue clean.' 'done'
    Show-Banner 'Queue clean.'
    Release-RunnerLock
    exit 0
  }

  $iteration++
  Write-Status $task 'prepare' 'Preparing task context.' 'running'
  Show-Banner ("Task {0}/{1}: {2} - {3}" -f $iteration, $iterationBudget.Limit, $task.Number, $task.Title) '-'
  Write-Host ("  Queue entry : {0}" -f $task.Number) -ForegroundColor Yellow
  Show-Stage 1 $stageTotal 'Prepare task' 'Resolve spec, publish CURRENT_TASK, checkpoint if needed.'
  Write-ProgressSnapshot $task 1 $stageTotal 'Prepare task' 'Resolving task spec and prompt.'

  $specFile = Resolve-SpecFile $task
  Write-Host ("  Spec file   : {0}" -f $specFile) -ForegroundColor Yellow
  Write-CurrentTaskFile $task $specFile
  Write-Host ("  Current task: {0}" -f $currentTaskFile) -ForegroundColor Yellow

  Git-CheckpointBeforeTask $task

  $specContent = Get-Content $specFile -Raw -Encoding UTF8
  $timeoutBudget = Get-CodexTimeoutBudget $task $specContent
  Write-Host ("  Codex budget: {0} minute(s), score {1}, {2} word spec" -f $timeoutBudget.Minutes, $timeoutBudget.Score, $timeoutBudget.WordCount) -ForegroundColor Yellow

  $prompt = Build-Prompt $task $specFile
  Write-Host ("  Saved prompt: {0}" -f $lastPromptFile) -ForegroundColor Yellow

  Write-Status $task 'codex' 'Running Codex executor.' 'running'
  Show-Stage 2 $stageTotal 'Execute with Codex' 'Waiting for Codex to finish the current task.'
  Write-ProgressSnapshot $task 2 $stageTotal 'Codex executor' 'Launching Codex.'
  try {
    $codexExit = Run-Codex $prompt $timeoutBudget.Seconds $task 2 $stageTotal
  } catch {
    $codexMessage = $_.Exception.Message
    $resumeCode = if ($codexMessage -match 'timed out') { 124 } else { 1 }
    $resumeState = if ($resumeCode -eq 124) { 'resume' } else { 'failed' }
    Write-Status $task 'codex-failed' $codexMessage $resumeState
    Show-Banner ("FAILED: Codex stopped on task {0}" -f $task.Number) '!'
    Write-Host ("  {0}" -f $codexMessage) -ForegroundColor Red
    Release-RunnerLock
    exit $resumeCode
  }
  if ($codexExit -ne 0) {
    Write-Status $task 'codex-failed' ("Codex exited with code {0}." -f $codexExit) 'failed'
    Show-Banner ("FAILED: Codex exited with code {0} on task {1}" -f $codexExit, $task.Number) '!'
    Show-FilePreview 'Codex stdout' $codexStdoutLog 10
    Show-FilePreview 'Codex stderr' $codexStderrLog 10
    [void](Show-ChangeSummary 'Changes after failed Codex run')
    Release-RunnerLock
    exit $codexExit
  }
  Write-Host '  Codex stage complete.' -ForegroundColor Green
  [void](Show-ChangeSummary 'Changes after Codex')

  Write-Status $task 'queue-check' 'Checking that Codex marked the task complete.' 'running'
  Show-Stage 3 $stageTotal 'Check queue update' 'Task must be marked [x] before we continue.'
  Write-ProgressSnapshot $task 3 $stageTotal 'Queue check' 'Checking TASK_QUEUE.md completion marker.'
  $updatedQueue = Get-Content $queueFile -Encoding UTF8
  $donePattern = "^- \[x\] $($task.Number)\b"
  $markedDone = $updatedQueue | Where-Object { $_ -match $donePattern } | Select-Object -First 1
  if (-not $markedDone) {
    Write-Status $task 'queue-check-failed' 'Task was not marked done in TASK_QUEUE.md.' 'failed'
    Show-Banner ("FAILED: task {0} was not marked done in TASK_QUEUE.md" -f $task.Number) '!'
    Write-Host '  Task completion: no' -ForegroundColor Red
    Write-Host '  Codex returned successfully but did not check off the task.' -ForegroundColor Red
    Show-FilePreview 'Codex stdout' $codexStdoutLog 10
    Show-FilePreview 'Codex stderr' $codexStderrLog 10
    [void](Show-ChangeSummary 'Changes waiting for review')
    Release-RunnerLock
    exit 1
  }
  Write-Host '  Task completion: yes, queue item is marked [x].' -ForegroundColor Green
  Write-Host '  Queue update check passed.' -ForegroundColor Green

  Write-Status $task 'tester' 'Running local tester/build stage.' 'running'
  Show-Stage 4 $stageTotal 'Run tester' 'Build/test the task locally.'
  Write-ProgressSnapshot $task 4 $stageTotal 'Tester' 'Running build and local test checks.'
  $testExit = Run-Tester $task
  if ($testExit -ne 0) {
    Write-Status $task 'tester-failed' ("Tester failed with code {0}." -f $testExit) 'failed'
    Show-Banner ("FAILED: tester failed after task {0}" -f $task.Number) '!'
    Show-FilePreview 'Tester result' $lastTestResultFile 18
    Show-FilePreview 'Build log' $lastTestBuildLog 18
    [void](Show-ChangeSummary 'Changes after failed tester')
    Release-RunnerLock
    exit $testExit
  }
  Write-Host '  Tester stage passed.' -ForegroundColor Green
  Show-FilePreview 'Tester result' $lastTestResultFile 10

  Write-Status $task 'verifier' 'Running verifier checks and writing VERIFICATION.md.' 'running'
  Show-Stage 5 $stageTotal 'Run verifier' 'Verify goal alignment and acceptance.'
  Write-ProgressSnapshot $task 5 $stageTotal 'Verifier' 'Checking evidence and goal alignment.'
  $verifyExit = Run-Verifier $task
  if ($verifyExit -eq 2) {
    Write-Status $task 'verifier-partial' 'Verifier requires manual review before commit.' 'review'
    Show-Banner ("STOP: verifier requires manual review for task {0}" -f $task.Number) '!'
    Show-FilePreview 'Verifier notes' $verificationFile 24
    [void](Show-ChangeSummary 'Changes waiting for manual review')
    Release-RunnerLock
    exit 2
  }
  if ($verifyExit -ne 0) {
    Write-Status $task 'verifier-failed' ("Verifier failed with code {0}." -f $verifyExit) 'failed'
    Show-Banner ("FAILED: verifier rejected task {0}" -f $task.Number) '!'
    Show-FilePreview 'Verifier notes' $verificationFile 24
    [void](Show-ChangeSummary 'Changes after verifier rejection')
    Release-RunnerLock
    exit $verifyExit
  }
  Write-Host '  Verifier stage passed.' -ForegroundColor Green
  Show-FilePreview 'Verifier notes' $verificationFile 12

  if (-not (Git-HasChanges)) {
    Write-Status $task 'commit-failed' 'No relevant git changes found after completed task.' 'failed'
    Show-Banner ("FAILED: no git changes found after completed task {0}" -f $task.Number) '!'
    Write-Host '  Refusing to continue because the task was not committed.' -ForegroundColor Red
    [void](Show-ChangeSummary 'Commit check')
    Release-RunnerLock
    exit 1
  }

  Write-Status $task 'commit' 'Creating the per-task git commit.' 'running'
  Show-Stage 6 $stageTotal 'Commit task' 'Create the mandatory safety commit for this completed task.'
  Write-ProgressSnapshot $task 6 $stageTotal 'Commit' 'Writing strict DONE entry and creating task commit.'
  Ensure-StrictDoneEntry $task
  $commitMessage = "agentops: task {0} - {1}" -f $task.Number, $task.Title
  $commitCreated = Git-CommitIfDirty $commitMessage
  if (-not $commitCreated) {
    Write-Status $task 'commit-failed' 'git commit did not create a task commit.' 'failed'
    Show-Banner ("FAILED: git commit did not create a commit for task {0}" -f $task.Number) '!'
    Release-RunnerLock
    exit 1
  }

  Write-Status $task 'done' ("Task {0} completed and committed." -f $task.Number) 'done'
  Show-Banner ("DONE: task {0} committed" -f $task.Number) '+'
  Show-QueueStatus
}

Write-Status $null 'resume-needed' ("Reached iteration budget ({0}) with queue still open." -f $iterationBudget.Limit) 'resume'
Show-Banner ("PAUSE: reached iteration budget ({0}); queue still has open tasks" -f $iterationBudget.Limit) '!'
Release-RunnerLock
exit 10
