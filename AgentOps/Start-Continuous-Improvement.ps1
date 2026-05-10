[CmdletBinding()]
param(
  [int]$IntervalSeconds = 300,
  [int]$ProgressTickSeconds = 15,
  [int]$WorkerTimeoutMinutes = 60,
  [int]$MaxLoops = 0,
  [switch]$PrepareOnly,
  [switch]$Once,
  [switch]$StatusOnly
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$agentOpsRoot = Join-Path $root 'AgentOps'
$heartbeatScript = Join-Path $agentOpsRoot 'Heartbeat-AgentOps.ps1'
$statusScript = Join-Path $agentOpsRoot 'Check-Heartbeat-Status.ps1'
$heartbeatFile = Join-Path $agentOpsRoot 'HEARTBEAT.md'
$heartbeatLogFile = Join-Path $agentOpsRoot 'HEARTBEAT.log'
$workerLogFile = Join-Path $agentOpsRoot 'worker.log'
$progressFile = Join-Path $agentOpsRoot 'PROGRESS.md'
$summaryFile = Join-Path $agentOpsRoot 'RUN_SUMMARY.md'
$queueFile = Join-Path $agentOpsRoot 'TASK_QUEUE.md'

function Show-Banner {
  $bar = '=' * 76
  Write-Host $bar -ForegroundColor Cyan
  Write-Host 'AgentOps continuous improvement runner' -ForegroundColor Cyan
  Write-Host ("Project : {0}" -f $root) -ForegroundColor Cyan
  Write-Host ("Queue   : {0}" -f $queueFile) -ForegroundColor Cyan
  Write-Host ("Status  : {0}" -f $heartbeatFile) -ForegroundColor Cyan
  Write-Host ("Logs    : {0}" -f $heartbeatLogFile) -ForegroundColor Cyan
  Write-Host ("Worker  : {0}" -f $workerLogFile) -ForegroundColor Cyan
  Write-Host ("Summary : {0}" -f $summaryFile) -ForegroundColor Cyan
  Write-Host ("Progress: {0}" -f $progressFile) -ForegroundColor Cyan
  Write-Host $bar -ForegroundColor Cyan
  Write-Host ''
}

function Ensure-Files {
  foreach ($path in @($agentOpsRoot)) {
    if (-not (Test-Path $path)) {
      New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
  }

  if (-not (Test-Path $heartbeatScript)) {
    throw "Heartbeat script not found: $heartbeatScript"
  }

  if (-not (Test-Path $statusScript)) {
    throw "Status script not found: $statusScript"
  }
}

Ensure-Files
Show-Banner

if ($StatusOnly) {
  & $statusScript
  exit $LASTEXITCODE
}

Write-Host 'Mode:' -ForegroundColor Yellow
if ($PrepareOnly) {
  Write-Host '  Prepare-only: validate/update AgentOps state without running Codex.' -ForegroundColor Yellow
} elseif ($Once) {
  Write-Host '  Single pass: run one worker loop and stop.' -ForegroundColor Yellow
} else {
  Write-Host '  Continuous: keep looping until queue is clean or a failure stops the runner.' -ForegroundColor Yellow
}
Write-Host ("  Cadence: every {0}s, progress heartbeat every {1}s, worker timeout {2} min." -f $IntervalSeconds, $ProgressTickSeconds, $WorkerTimeoutMinutes) -ForegroundColor Yellow
if ($MaxLoops -gt 0) {
  Write-Host ("  Max loops: {0}" -f $MaxLoops) -ForegroundColor Yellow
} else {
  Write-Host '  Max loops: unlimited (0)' -ForegroundColor Yellow
}
Write-Host ''
Write-Host 'Visible files while running:' -ForegroundColor DarkGray
Write-Host ("  {0}" -f $heartbeatFile) -ForegroundColor DarkGray
Write-Host ("  {0}" -f $progressFile) -ForegroundColor DarkGray
Write-Host ("  {0}" -f $summaryFile) -ForegroundColor DarkGray
Write-Host ("  {0}" -f $heartbeatLogFile) -ForegroundColor DarkGray
Write-Host ("  {0}" -f $workerLogFile) -ForegroundColor DarkGray
Write-Host ''

Push-Location $root
try {
  & $heartbeatScript `
    -IntervalSeconds $IntervalSeconds `
    -ProgressTickSeconds $ProgressTickSeconds `
    -WorkerTimeoutMinutes $WorkerTimeoutMinutes `
    -MaxLoops $MaxLoops `
    -PrepareOnly:$PrepareOnly `
    -Once:$Once
  exit $LASTEXITCODE
}
finally {
  Pop-Location
}
