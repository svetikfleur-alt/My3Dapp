[CmdletBinding()]
param(
  [int]$IntervalSeconds = 300,
  [int]$ProgressTickSeconds = 15,
  [int]$WorkerTimeoutMinutes = 45,
  [switch]$PrepareOnly,
  [switch]$Once
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$agentOpsRoot = Join-Path $root 'AgentOps'
$heartbeatScript = Join-Path $agentOpsRoot 'Heartbeat-AgentOps.ps1'
$heartbeatLockFile = Join-Path $agentOpsRoot 'HEARTBEAT.lock'
$heartbeatFile = Join-Path $agentOpsRoot 'HEARTBEAT.md'
$heartbeatLogFile = Join-Path $agentOpsRoot 'HEARTBEAT.log'

function Get-HeartbeatLockState {
  if (-not (Test-Path $heartbeatLockFile)) {
    return [pscustomobject]@{ Exists = $false; Active = $false; Pid = ''; Message = 'not running' }
  }

  try {
    $lock = Get-Content -Path $heartbeatLockFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $pidText = if ($null -ne $lock.pid) { [string]$lock.pid } else { '' }
    if (-not [string]::IsNullOrWhiteSpace($pidText)) {
      $process = Get-Process -Id ([int]$pidText) -ErrorAction SilentlyContinue
      if ($null -ne $process) {
        return [pscustomobject]@{ Exists = $true; Active = $true; Pid = $pidText; Message = "already running as PID $pidText" }
      }
    }
  } catch {
  }

  Remove-Item -LiteralPath $heartbeatLockFile -Force -ErrorAction SilentlyContinue
  return [pscustomobject]@{ Exists = $true; Active = $false; Pid = ''; Message = 'stale lock cleared' }
}

if (-not (Test-Path $agentOpsRoot)) {
  New-Item -ItemType Directory -Path $agentOpsRoot -Force | Out-Null
}

if (-not (Test-Path $heartbeatScript)) {
  throw "Heartbeat script not found: $heartbeatScript"
}

$state = Get-HeartbeatLockState
if ($state.Active) {
  Write-Host ("AgentOps heartbeat is {0}." -f $state.Message) -ForegroundColor Yellow
  Write-Host ("Status: {0}" -f $heartbeatFile) -ForegroundColor DarkGray
  Write-Host ("Log   : {0}" -f $heartbeatLogFile) -ForegroundColor DarkGray
  exit 0
}

Write-Host 'Background terminal spawning is disabled for AgentOps stability.' -ForegroundColor Yellow
Write-Host 'Running the heartbeat in this same PowerShell process instead.' -ForegroundColor Yellow
Write-Host ("Status: {0}" -f $heartbeatFile) -ForegroundColor Cyan
Write-Host ("Log   : {0}" -f $heartbeatLogFile) -ForegroundColor Cyan
Write-Host ''

Push-Location $root
try {
  & $heartbeatScript `
    -IntervalSeconds $IntervalSeconds `
    -ProgressTickSeconds $ProgressTickSeconds `
    -WorkerTimeoutMinutes $WorkerTimeoutMinutes `
    -Quiet:$true `
    -Once:$Once `
    -PrepareOnly:$PrepareOnly
  exit $LASTEXITCODE
} finally {
  Pop-Location
}
