[CmdletBinding()]
param(
  [int]$IntervalSeconds = 300,
  [int]$ProgressTickSeconds = 15,
  [int]$MaxLoops = 0,
  [int]$WorkerTimeoutMinutes = 45,
  [switch]$PrepareOnly,
  [switch]$Once,
  [switch]$Quiet
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$agentOpsRoot = Join-Path $root 'AgentOps'
$queueFile = Join-Path $agentOpsRoot 'TASK_QUEUE.md'
$spawnScript = Join-Path $agentOpsRoot 'Spawn-Codex.ps1'
$pauseFile = Join-Path $agentOpsRoot 'PAUSE'
$runnerLockFile = Join-Path $agentOpsRoot 'RUNNER.lock'
$statusFile = Join-Path $agentOpsRoot 'STATUS.md'
$progressFile = Join-Path $agentOpsRoot 'PROGRESS.md'
$runSummaryFile = Join-Path $agentOpsRoot 'RUN_SUMMARY.md'
$heartbeatFile = Join-Path $agentOpsRoot 'HEARTBEAT.md'
$heartbeatStateFile = Join-Path $agentOpsRoot 'HEARTBEAT_STATE.json'
$heartbeatLogFile = Join-Path $agentOpsRoot 'HEARTBEAT.log'
$heartbeatLockFile = Join-Path $agentOpsRoot 'HEARTBEAT.lock'
$workerStdoutFile = Join-Path $agentOpsRoot 'HEARTBEAT_LAST_WORKER.log'
$workerStderrFile = Join-Path $agentOpsRoot 'HEARTBEAT_LAST_WORKER.err'

function Get-AgentOpsTimestamp {
  return (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
}

function Ensure-AgentOpsHeartbeatFiles {
  if (-not (Test-Path $agentOpsRoot)) {
    New-Item -ItemType Directory -Path $agentOpsRoot -Force | Out-Null
  }
  if (-not (Test-Path $queueFile)) {
    Set-Content -Path $queueFile -Value "# Active queue (in priority order)`r`n" -Encoding UTF8
  }
  if (-not (Test-Path $spawnScript)) {
    throw "Worker script not found: $spawnScript"
  }
}

function Write-AgentOpsLine {
  param(
    [string]$Message,
    [string]$Color = 'Gray'
  )

  $stamp = Get-AgentOpsTimestamp
  Add-Content -Path $heartbeatLogFile -Value ("[{0}] {1}" -f $stamp, $Message) -Encoding UTF8
  if (-not $Quiet) {
    Write-Host $Message -ForegroundColor $Color
  }
}

function Show-HeartbeatBanner {
  if ($Quiet) { return }

  $bar = '=' * 76
  Write-Host $bar -ForegroundColor Cyan
  Write-Host 'AgentOps heartbeat co-pilot' -ForegroundColor Cyan
  Write-Host ("Project: {0}" -f $root) -ForegroundColor Cyan
  Write-Host ("Worker : {0}" -f $spawnScript) -ForegroundColor Cyan
  Write-Host ("Cadence: every {0}s; progress tick every {1}s" -f $IntervalSeconds, $ProgressTickSeconds) -ForegroundColor Cyan
  Write-Host $bar -ForegroundColor Cyan
  Write-Host ''
}

function Get-QueueSnapshot {
  if (-not (Test-Path $queueFile)) {
    return [pscustomobject]@{
      Done = 0
      Open = 0
      NextNumber = ''
      NextTitle = ''
      NextSpec = ''
      NextLine = ''
    }
  }

  $lines = @(Get-Content -Path $queueFile -Encoding UTF8)
  $doneLines = @($lines | Where-Object { $_ -match '^\s*-\s*\[x\]' })
  $openLines = @($lines | Where-Object { $_ -match '^\s*-\s*\[ \]' })
  $nextLine = ''
  if ($openLines.Count -gt 0) {
    $nextLine = [string]$openLines[0]
  }

  $number = ''
  $title = ''
  $spec = ''
  if ($nextLine -match '^\s*-\s*\[ \]\s*(?<num>\S+)\s+(?<rest>.*)$') {
    $number = $Matches['num']
    $rest = [string]$Matches['rest']
    if ($rest -match '(?<spec>TASKS[\\/]\S+)\s*$') {
      $spec = $Matches['spec'].Trim()
      $specIndex = $rest.LastIndexOf($spec, [StringComparison]::Ordinal)
      if ($specIndex -gt 0) {
        $title = $rest.Substring(0, $specIndex).Trim()
        # Keep the source queue free to use arrows/dashes, but parse with ASCII-safe rules.
        $title = ($title -replace '^\s*\P{L}+', '').Trim()
        $title = ($title -replace '\s*\P{L}+$', '').Trim()
      }
    } else {
      $title = $rest.Trim()
    }
  }

  return [pscustomobject]@{
    Done = $doneLines.Count
    Open = $openLines.Count
    NextNumber = $number
    NextTitle = $title
    NextSpec = $spec
    NextLine = $nextLine
  }
}

function Test-IsFinalUiPolishText {
  param([string]$Text)

  if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
  return ($Text.ToLowerInvariant() -match 'final\s+ui\s+polish|final-ui-polish|end-of-cycle\s+polish')
}

function Get-UiPolishStageStatus {
  if (-not (Test-Path $queueFile)) {
    return [pscustomobject]@{
      Message = 'missing queue'
      Line = ''
      PendingCount = 0
      CompletedCount = 0
    }
  }

  $lines = @(Get-Content -Path $queueFile -Encoding UTF8)
  $polishLines = @($lines | Where-Object { Test-IsFinalUiPolishText $_ })
  $pending = @($polishLines | Where-Object { $_ -match '^\s*-\s*\[ \]' })
  $completed = @($polishLines | Where-Object { $_ -match '^\s*-\s*\[x\]' })
  $line = if ($pending.Count -gt 0) { [string]$pending[0] } elseif ($completed.Count -gt 0) { [string]$completed[-1] } else { '' }
  $message = if ($pending.Count -gt 1) {
    "duplicate pending final UI polish tasks ($($pending.Count))"
  } elseif ($pending.Count -gt 0) {
    'pending final UI polish'
  } elseif ($completed.Count -gt 0) {
    'completed this cycle'
  } else {
    'not present'
  }

  return [pscustomobject]@{
    Message = $message
    Line = $line
    PendingCount = $pending.Count
    CompletedCount = $completed.Count
  }
}

function Read-AgentOpsKeyValueFile {
  param([string]$Path)

  $values = @{}
  if (-not (Test-Path $Path)) {
    return $values
  }

  $lines = @(Get-Content -Path $Path -Encoding UTF8 -ErrorAction SilentlyContinue)
  foreach ($line in $lines) {
    if ($line -match '^\s*([^:#][^:]*):\s*(.*)\s*$') {
      $key = $Matches[1].Trim()
      $value = $Matches[2].Trim()
      if (-not [string]::IsNullOrWhiteSpace($key)) {
        $values[$key] = $value
      }
    }
  }
  return $values
}

function Get-LastTextLine {
  param([string]$Path)

  if (-not (Test-Path $Path)) {
    return ''
  }

  $lines = @(Get-Content -Path $Path -Tail 8 -Encoding UTF8 -ErrorAction SilentlyContinue)
  for ($i = $lines.Count - 1; $i -ge 0; $i--) {
    $text = [string]$lines[$i]
    if (-not [string]::IsNullOrWhiteSpace($text)) {
      return $text.Trim()
    }
  }
  return ''
}

function Get-RunnerLockState {
  if (-not (Test-Path $runnerLockFile)) {
    return [pscustomobject]@{
      Exists = $false
      IsActive = $false
      IsStale = $false
      Pid = ''
      StartedAt = ''
      Message = 'no lock'
    }
  }

  $pidValue = ''
  $startedAt = ''
  try {
    $raw = Get-Content -Path $runnerLockFile -Raw -Encoding UTF8
    $lock = $raw | ConvertFrom-Json
    if ($null -ne $lock.pid) { $pidValue = [string]$lock.pid }
    if ($null -ne $lock.started_at) { $startedAt = [string]$lock.started_at }
  } catch {
    return [pscustomobject]@{
      Exists = $true
      IsActive = $false
      IsStale = $true
      Pid = ''
      StartedAt = ''
      Message = 'lock file is unreadable'
    }
  }

  $active = $false
  if (-not [string]::IsNullOrWhiteSpace($pidValue)) {
    $process = Get-Process -Id ([int]$pidValue) -ErrorAction SilentlyContinue
    $active = $null -ne $process
  }

  return [pscustomobject]@{
    Exists = $true
    IsActive = $active
    IsStale = -not $active
    Pid = $pidValue
    StartedAt = $startedAt
    Message = if ($active) { "active pid $pidValue" } else { "stale pid $pidValue" }
  }
}

function Clear-StaleRunnerLock {
  $state = Get-RunnerLockState
  if ($state.Exists -and $state.IsStale) {
    Remove-Item -LiteralPath $runnerLockFile -Force -ErrorAction SilentlyContinue
    Write-AgentOpsLine ("Cleared stale RUNNER.lock ({0})." -f $state.Message) 'Yellow'
  }
}

function Get-HeartbeatLockState {
  if (-not (Test-Path $heartbeatLockFile)) {
    return [pscustomobject]@{
      Exists = $false
      IsActive = $false
      IsStale = $false
      Pid = ''
      Message = 'no heartbeat lock'
    }
  }

  $pidValue = ''
  try {
    $raw = Get-Content -Path $heartbeatLockFile -Raw -Encoding UTF8
    $lock = $raw | ConvertFrom-Json
    if ($null -ne $lock.pid) { $pidValue = [string]$lock.pid }
  } catch {
    return [pscustomobject]@{
      Exists = $true
      IsActive = $false
      IsStale = $true
      Pid = ''
      Message = 'heartbeat lock is unreadable'
    }
  }

  $active = $false
  if (-not [string]::IsNullOrWhiteSpace($pidValue)) {
    $process = Get-Process -Id ([int]$pidValue) -ErrorAction SilentlyContinue
    $active = $null -ne $process
  }

  return [pscustomobject]@{
    Exists = $true
    IsActive = $active
    IsStale = -not $active
    Pid = $pidValue
    Message = if ($active) { "active heartbeat pid $pidValue" } else { "stale heartbeat pid $pidValue" }
  }
}

function Acquire-HeartbeatLock {
  $state = Get-HeartbeatLockState
  if ($state.Exists -and $state.IsActive) {
    Write-AgentOpsLine ("Heartbeat supervisor is already running ({0})." -f $state.Message) 'Yellow'
    Write-HeartbeatState -State 'already-running' -Message $state.Message -Loop 0 | Out-Null
    exit 0
  }

  if ($state.Exists -and $state.IsStale) {
    Remove-Item -LiteralPath $heartbeatLockFile -Force -ErrorAction SilentlyContinue
    Write-AgentOpsLine ("Cleared stale HEARTBEAT.lock ({0})." -f $state.Message) 'Yellow'
  }

  $lockInfo = [pscustomobject]@{
    pid = $PID
    started_at = (Get-Date).ToString('o')
    machine = $env:COMPUTERNAME
    root = $root
  }
  $lockInfo | ConvertTo-Json -Depth 4 | Set-Content -Path $heartbeatLockFile -Encoding UTF8
}

function Release-HeartbeatLock {
  if (-not (Test-Path $heartbeatLockFile)) { return }

  try {
    $raw = Get-Content -Path $heartbeatLockFile -Raw -Encoding UTF8
    $lock = $raw | ConvertFrom-Json
    if ($null -ne $lock.pid -and [int]$lock.pid -eq $PID) {
      Remove-Item -LiteralPath $heartbeatLockFile -Force -ErrorAction SilentlyContinue
    }
  } catch {
  }
}

function Exit-Heartbeat {
  param([int]$Code)

  Release-HeartbeatLock
  exit $Code
}

function Reset-WorkerTailLogs {
  Set-Content -Path $workerStdoutFile -Value '' -Encoding UTF8
  Set-Content -Path $workerStderrFile -Value '' -Encoding UTF8
}

function New-HeartbeatStateObject {
  param(
    [string]$State,
    [string]$Message,
    [int]$Loop,
    [object]$WorkerPid,
    [int]$WorkerElapsedSeconds,
    [object]$LastExitCode
  )

  $queue = Get-QueueSnapshot
  $status = Read-AgentOpsKeyValueFile $statusFile
  $progress = Read-AgentOpsKeyValueFile $progressFile
  $lock = Get-RunnerLockState
  $polish = Get-UiPolishStageStatus
  $lastOut = Get-LastTextLine $workerStdoutFile
  $lastErr = Get-LastTextLine $workerStderrFile

  return [pscustomobject]@{
    Timestamp = Get-AgentOpsTimestamp
    State = $State
    Message = $Message
    Loop = $Loop
    QueueDone = $queue.Done
    QueueOpen = $queue.Open
    NextTask = $queue.NextLine
    UiPolishStage = $polish.Message
    UiPolishTask = $polish.Line
    StatusPhase = if ($status.ContainsKey('Phase')) { $status['Phase'] } else { '' }
    StatusState = if ($status.ContainsKey('State')) { $status['State'] } else { '' }
    StatusMessage = if ($status.ContainsKey('Message')) { $status['Message'] } else { '' }
    ProgressStage = if ($progress.ContainsKey('Stage')) { $progress['Stage'] } else { '' }
    ProgressPercent = if ($progress.ContainsKey('StagePercent')) { $progress['StagePercent'] } else { '' }
    RelevantLinesAdded = if ($progress.ContainsKey('RelevantLinesAdded')) { $progress['RelevantLinesAdded'] } else { '' }
    RelevantLinesRemoved = if ($progress.ContainsKey('RelevantLinesRemoved')) { $progress['RelevantLinesRemoved'] } else { '' }
    CodexStdoutLines = if ($progress.ContainsKey('CodexStdoutLines')) { $progress['CodexStdoutLines'] } else { '' }
    CodexStderrLines = if ($progress.ContainsKey('CodexStderrLines')) { $progress['CodexStderrLines'] } else { '' }
    WorkerPid = if ($null -ne $WorkerPid) { [int]$WorkerPid } else { $null }
    WorkerElapsedSeconds = $WorkerElapsedSeconds
    LastExitCode = if ($null -ne $LastExitCode) { [int]$LastExitCode } else { $null }
    IntervalSeconds = $IntervalSeconds
    ProgressTickSeconds = $ProgressTickSeconds
    RunnerLock = $lock.Message
    LastWorkerStdout = $lastOut
    LastWorkerStderr = $lastErr
  }
}

function Write-HeartbeatState {
  param(
    [string]$State,
    [string]$Message,
    [int]$Loop = 0,
    [object]$WorkerPid = $null,
    [int]$WorkerElapsedSeconds = 0,
    [object]$LastExitCode = $null
  )

  $stateObject = New-HeartbeatStateObject -State $State -Message $Message -Loop $Loop -WorkerPid $WorkerPid -WorkerElapsedSeconds $WorkerElapsedSeconds -LastExitCode $LastExitCode
  $json = $stateObject | ConvertTo-Json -Depth 5
  Set-Content -Path $heartbeatStateFile -Value $json -Encoding UTF8

  $content = @(
    "# AgentOps Heartbeat - $($stateObject.Timestamp)"
    ''
    "State: $($stateObject.State)"
    "Message: $($stateObject.Message)"
    "Loop: $($stateObject.Loop)"
    "Queue: $($stateObject.QueueDone) done, $($stateObject.QueueOpen) open"
    "NextTask: $($stateObject.NextTask)"
    "UiPolishStage: $($stateObject.UiPolishStage)"
    "UiPolishTask: $($stateObject.UiPolishTask)"
    "WorkerPid: $($stateObject.WorkerPid)"
    "WorkerElapsedSeconds: $($stateObject.WorkerElapsedSeconds)"
    "LastExitCode: $($stateObject.LastExitCode)"
    "IntervalSeconds: $($stateObject.IntervalSeconds)"
    "ProgressTickSeconds: $($stateObject.ProgressTickSeconds)"
    "RunnerLock: $($stateObject.RunnerLock)"
    ''
    "StatusPhase: $($stateObject.StatusPhase)"
    "StatusState: $($stateObject.StatusState)"
    "StatusMessage: $($stateObject.StatusMessage)"
    "ProgressStage: $($stateObject.ProgressStage)"
    "ProgressPercent: $($stateObject.ProgressPercent)"
    "RelevantLinesAdded: $($stateObject.RelevantLinesAdded)"
    "RelevantLinesRemoved: $($stateObject.RelevantLinesRemoved)"
    "CodexStdoutLines: $($stateObject.CodexStdoutLines)"
    "CodexStderrLines: $($stateObject.CodexStderrLines)"
    ''
    "LastWorkerStdout: $($stateObject.LastWorkerStdout)"
    "LastWorkerStderr: $($stateObject.LastWorkerStderr)"
    ''
    "Files:"
    "- Status: $statusFile"
    "- Progress: $progressFile"
    "- Worker stdout: $workerStdoutFile"
    "- Worker stderr: $workerStderrFile"
    "- Run summary: $runSummaryFile"
  ) -join [Environment]::NewLine

  Set-Content -Path $heartbeatFile -Value $content -Encoding UTF8
  return $stateObject
}

function Show-HeartbeatTick {
  param(
    [string]$State,
    [string]$Message,
    [int]$Loop,
    [object]$WorkerPid = $null,
    [int]$WorkerElapsedSeconds = 0,
    [object]$LastExitCode = $null
  )

  $stateObject = Write-HeartbeatState -State $State -Message $Message -Loop $Loop -WorkerPid $WorkerPid -WorkerElapsedSeconds $WorkerElapsedSeconds -LastExitCode $LastExitCode
  $phase = if ([string]::IsNullOrWhiteSpace($stateObject.StatusPhase)) { '-' } else { $stateObject.StatusPhase }
  $stage = if ([string]::IsNullOrWhiteSpace($stateObject.ProgressStage)) { '-' } else { $stateObject.ProgressStage }
  $percent = if ([string]::IsNullOrWhiteSpace($stateObject.ProgressPercent)) { '-' } else { "$($stateObject.ProgressPercent)%" }
  $delta = if ([string]::IsNullOrWhiteSpace($stateObject.RelevantLinesAdded) -and [string]::IsNullOrWhiteSpace($stateObject.RelevantLinesRemoved)) {
    '+? -?'
  } else {
    "+$($stateObject.RelevantLinesAdded) -$($stateObject.RelevantLinesRemoved)"
  }
  $pidText = if ($null -ne $WorkerPid) { [string]$WorkerPid } else { '-' }

  Write-AgentOpsLine ("heartbeat loop={0} state={1} worker={2} elapsed={3}s queue={4}/{5} phase={6} stage={7} {8} diff={9} polish={10}" -f $Loop, $State, $pidText, $WorkerElapsedSeconds, $stateObject.QueueDone, $stateObject.QueueOpen, $phase, $stage, $percent, $delta, $stateObject.UiPolishStage) 'DarkCyan'
  if (-not [string]::IsNullOrWhiteSpace($stateObject.LastWorkerStdout)) {
    Write-AgentOpsLine ("  worker stdout: {0}" -f $stateObject.LastWorkerStdout) 'DarkGray'
  }
  if (-not [string]::IsNullOrWhiteSpace($stateObject.LastWorkerStderr)) {
    Write-AgentOpsLine ("  worker stderr: {0}" -f $stateObject.LastWorkerStderr) 'Yellow'
  }
}

function Wait-HeartbeatInterval {
  param(
    [string]$State,
    [string]$Reason,
    [int]$Loop,
    [object]$LastExitCode = $null
  )

  $total = [Math]::Max(1, $IntervalSeconds)
  $tick = [Math]::Max(1, [Math]::Min($ProgressTickSeconds, $total))
  $remaining = $total

  while ($remaining -gt 0) {
    Show-HeartbeatTick -State $State -Message ("{0} Next check in {1}s." -f $Reason, $remaining) -Loop $Loop -LastExitCode $LastExitCode
    $sleep = [Math]::Min($tick, $remaining)
    Start-Sleep -Seconds $sleep
    $remaining -= $sleep
  }
}

function Invoke-WorkerPass {
  param([int]$Loop)

  if (Test-Path $workerStdoutFile) { Remove-Item -LiteralPath $workerStdoutFile -Force -ErrorAction SilentlyContinue }
  if (Test-Path $workerStderrFile) { Remove-Item -LiteralPath $workerStderrFile -Force -ErrorAction SilentlyContinue }

  $powershellExe = (Get-Command powershell.exe -ErrorAction Stop).Source
  $argumentList = @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $spawnScript,
    '-MaxIterations',
    '1'
  )

  Write-AgentOpsLine ("Starting worker pass {0}: Spawn-Codex.ps1 -MaxIterations 1" -f $Loop) 'Cyan'
  Write-AgentOpsLine 'Worker launch mode: current process supervisor, redirected child process, no visible secondary terminal.' 'DarkGray'

  $quotedArgs = $argumentList | ForEach-Object {
    $arg = [string]$_
    if ($arg -match '[\s"]') {
      '"' + ($arg -replace '"', '\"') + '"'
    } else {
      $arg
    }
  }

  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = $powershellExe
  $psi.Arguments = ($quotedArgs -join ' ')
  $psi.WorkingDirectory = $root
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.CreateNoWindow = $true

  $script:AgentOpsWorkerStdoutFile = $workerStdoutFile
  $script:AgentOpsWorkerStderrFile = $workerStderrFile
  $script:AgentOpsHeartbeatQuiet = [bool]$Quiet
  $script:AgentOpsWorkerStdoutLines = 0
  $script:AgentOpsWorkerStderrLines = 0

  $process = New-Object System.Diagnostics.Process
  $process.StartInfo = $psi
  $process.EnableRaisingEvents = $true

  $outputHandler = [System.Diagnostics.DataReceivedEventHandler]{
    param($sender, $eventArgs)
    if ($null -eq $eventArgs.Data) { return }

    Add-Content -Path $script:AgentOpsWorkerStdoutFile -Value $eventArgs.Data -Encoding UTF8
    $script:AgentOpsWorkerStdoutLines++
    if (-not $script:AgentOpsHeartbeatQuiet) {
      $display = [string]$eventArgs.Data
      if ($display.Length -gt 220) { $display = $display.Substring(0, 220) + '...' }
      Write-Host ("    worker> {0}" -f $display) -ForegroundColor DarkGray
    }
  }

  $errorHandler = [System.Diagnostics.DataReceivedEventHandler]{
    param($sender, $eventArgs)
    if ($null -eq $eventArgs.Data) { return }

    Add-Content -Path $script:AgentOpsWorkerStderrFile -Value $eventArgs.Data -Encoding UTF8
    $script:AgentOpsWorkerStderrLines++
    if (-not $script:AgentOpsHeartbeatQuiet) {
      $display = [string]$eventArgs.Data
      if ($display.Length -gt 220) { $display = $display.Substring(0, 220) + '...' }
      Write-Host ("    worker! {0}" -f $display) -ForegroundColor Yellow
    }
  }

  [void]$process.add_OutputDataReceived($outputHandler)
  [void]$process.add_ErrorDataReceived($errorHandler)

  [void]$process.Start()
  $process.BeginOutputReadLine()
  $process.BeginErrorReadLine()
  Write-AgentOpsLine ("Worker started with PID {0}." -f $process.Id) 'Green'

  $watch = [Diagnostics.Stopwatch]::StartNew()
  $timeoutSeconds = [Math]::Max(60, $WorkerTimeoutMinutes * 60)
  $nextTick = 0

  try {
    while (-not $process.WaitForExit(1000)) {
      $elapsed = [int]$watch.Elapsed.TotalSeconds
      if ($elapsed -ge $nextTick) {
        Show-HeartbeatTick -State 'worker-running' -Message 'Worker is still active.' -Loop $Loop -WorkerPid $process.Id -WorkerElapsedSeconds $elapsed
        Write-AgentOpsLine ("  worker output lines: stdout {0}, stderr {1}" -f $script:AgentOpsWorkerStdoutLines, $script:AgentOpsWorkerStderrLines) 'DarkGray'
        $nextTick = $elapsed + [Math]::Max(1, $ProgressTickSeconds)
      }

      if ($elapsed -ge $timeoutSeconds) {
        Write-AgentOpsLine ("Worker timed out after {0}s. Stopping PID {1} so the next pass can resume cleanly." -f $timeoutSeconds, $process.Id) 'Red'
        try { $process.Kill() } catch {}
        $process.WaitForExit()
        $watch.Stop()
        Show-HeartbeatTick -State 'worker-timeout' -Message 'Worker timed out; heartbeat stopped it.' -Loop $Loop -WorkerElapsedSeconds ([int]$watch.Elapsed.TotalSeconds) -LastExitCode 124
        return 124
      }
    }

    $process.WaitForExit()
    $watch.Stop()
    $exitCode = [int]$process.ExitCode
    Write-AgentOpsLine ("Worker exit code: {0}; output lines stdout {1}, stderr {2}." -f $exitCode, $script:AgentOpsWorkerStdoutLines, $script:AgentOpsWorkerStderrLines) $(if ($exitCode -eq 0) { 'Green' } else { 'Yellow' })
    Show-HeartbeatTick -State 'worker-exited' -Message ("Worker exited with code {0}." -f $exitCode) -Loop $Loop -WorkerElapsedSeconds ([int]$watch.Elapsed.TotalSeconds) -LastExitCode $exitCode
    return $exitCode
  } finally {
    try { $process.remove_OutputDataReceived($outputHandler) } catch {}
    try { $process.remove_ErrorDataReceived($errorHandler) } catch {}
    if ($process) { $process.Dispose() }
    Remove-Variable AgentOpsWorkerStdoutFile -Scope Script -ErrorAction SilentlyContinue
    Remove-Variable AgentOpsWorkerStderrFile -Scope Script -ErrorAction SilentlyContinue
    Remove-Variable AgentOpsHeartbeatQuiet -Scope Script -ErrorAction SilentlyContinue
    Remove-Variable AgentOpsWorkerStdoutLines -Scope Script -ErrorAction SilentlyContinue
    Remove-Variable AgentOpsWorkerStderrLines -Scope Script -ErrorAction SilentlyContinue
  }
}

function Get-ExitExplanation {
  param([int]$ExitCode)

  switch ($ExitCode) {
    0 { return 'success or queue clean' }
    2 { return 'manual review required' }
    10 { return 'worker budget ended; queue can resume' }
    124 { return 'worker timeout; queue can resume after cooldown' }
    default { return 'failure; inspect logs before continuing' }
  }
}

Ensure-AgentOpsHeartbeatFiles
if (-not (Test-Path $heartbeatLogFile)) {
  Set-Content -Path $heartbeatLogFile -Value ("# AgentOps heartbeat log - {0}" -f (Get-AgentOpsTimestamp)) -Encoding UTF8
}
Show-HeartbeatBanner
Acquire-HeartbeatLock
Set-Content -Path $heartbeatLogFile -Value ("# AgentOps heartbeat log - {0}" -f (Get-AgentOpsTimestamp)) -Encoding UTF8
Reset-WorkerTailLogs
Write-AgentOpsLine 'Heartbeat supervisor is online. It will run one worker pass at a time and keep reporting state.' 'Green'
Write-AgentOpsLine ("Cadence: one worker pass every {0}s; progress/status refresh every {1}s." -f $IntervalSeconds, $ProgressTickSeconds) 'Green'
Write-AgentOpsLine ("Visible files: {0}, {1}, {2}" -f $heartbeatFile, $workerStdoutFile, $workerStderrFile) 'DarkGray'

Clear-StaleRunnerLock
$queueAtStart = Get-QueueSnapshot
Write-AgentOpsLine ("Queue at start: {0} done, {1} open." -f $queueAtStart.Done, $queueAtStart.Open) 'Yellow'
if (-not [string]::IsNullOrWhiteSpace($queueAtStart.NextLine)) {
  Write-AgentOpsLine ("Next task: {0}" -f $queueAtStart.NextLine) 'Yellow'
}
$polishAtStart = Get-UiPolishStageStatus
Write-AgentOpsLine ("UI polish stage: {0}" -f $polishAtStart.Message) 'Yellow'
if (-not [string]::IsNullOrWhiteSpace($polishAtStart.Line)) {
  Write-AgentOpsLine ("  {0}" -f $polishAtStart.Line) 'DarkGray'
}
Show-HeartbeatTick -State 'prepared' -Message 'Heartbeat initialized.' -Loop 0

if ($PrepareOnly) {
  Write-AgentOpsLine 'Prepare-only mode complete; no worker was started.' 'Green'
  Exit-Heartbeat 0
}

$loop = 0
$lastExit = 0

while ($true) {
  if ($MaxLoops -gt 0 -and $loop -ge $MaxLoops) {
    Show-HeartbeatTick -State 'max-loops' -Message ("Reached MaxLoops={0}." -f $MaxLoops) -Loop $loop -LastExitCode $lastExit
    Write-AgentOpsLine ("Reached MaxLoops={0}; stopping heartbeat." -f $MaxLoops) 'Yellow'
    Exit-Heartbeat 10
  }

  if (Test-Path $pauseFile) {
    Show-HeartbeatTick -State 'paused' -Message 'PAUSE file exists. Waiting.' -Loop $loop -LastExitCode $lastExit
    Write-AgentOpsLine ("PAUSE file found at {0}. Waiting {1}s before recheck." -f $pauseFile, $IntervalSeconds) 'Yellow'
    Wait-HeartbeatInterval -State 'paused' -Reason 'PAUSE file exists.' -Loop $loop -LastExitCode $lastExit
    continue
  }

  Clear-StaleRunnerLock
  $lockState = Get-RunnerLockState
  if ($lockState.Exists -and $lockState.IsActive) {
    Show-HeartbeatTick -State 'waiting-for-lock' -Message ("Another worker owns RUNNER.lock: {0}" -f $lockState.Message) -Loop $loop -LastExitCode $lastExit
    Write-AgentOpsLine ("Active RUNNER.lock ({0}); waiting {1}s instead of launching another worker." -f $lockState.Message, $IntervalSeconds) 'Yellow'
    Wait-HeartbeatInterval -State 'waiting-for-lock' -Reason 'Another worker is active.' -Loop $loop -LastExitCode $lastExit
    continue
  }

  $queue = Get-QueueSnapshot
  if ($queue.Open -le 0) {
    Show-HeartbeatTick -State 'complete' -Message 'Queue is clean.' -Loop $loop -LastExitCode $lastExit
    Write-AgentOpsLine 'Queue is clean. Heartbeat co-pilot is done.' 'Green'
    Exit-Heartbeat 0
  }

  $loop++
  Show-HeartbeatTick -State 'starting-worker' -Message 'Launching one safe worker pass.' -Loop $loop -LastExitCode $lastExit
  $lastExit = Invoke-WorkerPass -Loop $loop
  $explanation = Get-ExitExplanation $lastExit
  Write-AgentOpsLine ("Worker pass {0} finished: exit={1} ({2})." -f $loop, $lastExit, $explanation) $(if ($lastExit -eq 0) { 'Green' } elseif ($lastExit -eq 10 -or $lastExit -eq 124) { 'Yellow' } else { 'Red' })

  if ($lastExit -eq 0) {
    $afterQueue = Get-QueueSnapshot
    if ($afterQueue.Open -le 0) {
      Show-HeartbeatTick -State 'complete' -Message 'Worker reported success and queue is clean.' -Loop $loop -LastExitCode $lastExit
      Write-AgentOpsLine 'All queued tasks appear complete.' 'Green'
      Exit-Heartbeat 0
    }

    Write-AgentOpsLine ("Worker exit 0 but {0} task(s) remain. Continuing after {1}s." -f $afterQueue.Open, $IntervalSeconds) 'Yellow'
  } elseif ($lastExit -eq 10 -or $lastExit -eq 124) {
    Write-AgentOpsLine ("Resumable exit code {0}; waiting {1}s before the next pass." -f $lastExit, $IntervalSeconds) 'Yellow'
  } elseif ($lastExit -eq 2) {
    Show-HeartbeatTick -State 'manual-review' -Message 'Verifier requested manual review.' -Loop $loop -LastExitCode $lastExit
    Write-AgentOpsLine 'Manual review required. Stopping so a human can inspect the verification notes.' 'Red'
    Exit-Heartbeat 2
  } else {
    Show-HeartbeatTick -State 'failed' -Message ("Worker failed with exit code {0}." -f $lastExit) -Loop $loop -LastExitCode $lastExit
    Write-AgentOpsLine ("Worker failed with exit code {0}. Inspect heartbeat and worker logs before continuing." -f $lastExit) 'Red'
    Exit-Heartbeat $lastExit
  }

  if ($Once) {
    Show-HeartbeatTick -State 'once-complete' -Message 'Once mode completed one worker pass.' -Loop $loop -LastExitCode $lastExit
    Write-AgentOpsLine 'Once mode requested; stopping after one pass.' 'Yellow'
    Exit-Heartbeat $lastExit
  }

  Wait-HeartbeatInterval -State 'sleeping' -Reason 'Waiting before the next worker pass.' -Loop $loop -LastExitCode $lastExit
}
