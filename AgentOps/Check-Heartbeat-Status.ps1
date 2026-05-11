[CmdletBinding()]
param(
  [int]$LogLines = 30
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$agentOpsRoot = Join-Path $root 'AgentOps'
$heartbeatFile = Join-Path $agentOpsRoot 'HEARTBEAT.md'
$heartbeatLogFile = Join-Path $agentOpsRoot 'HEARTBEAT.log'
$heartbeatLockFile = Join-Path $agentOpsRoot 'HEARTBEAT.lock'

Write-Host '============================================================' -ForegroundColor Cyan
Write-Host 'AgentOps heartbeat status' -ForegroundColor Cyan
Write-Host '============================================================' -ForegroundColor Cyan
Write-Host ''

if (Test-Path $heartbeatLockFile) {
  try {
    $lock = Get-Content -Path $heartbeatLockFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $pidText = if ($null -ne $lock.pid) { [string]$lock.pid } else { '' }
    $active = $false
    if (-not [string]::IsNullOrWhiteSpace($pidText)) {
      $active = $null -ne (Get-Process -Id ([int]$pidText) -ErrorAction SilentlyContinue)
    }
    Write-Host ("Supervisor: {0} PID {1}" -f $(if ($active) { 'running' } else { 'stale/not running' }), $pidText) -ForegroundColor $(if ($active) { 'Green' } else { 'Yellow' })
  } catch {
    Write-Host 'Supervisor: lock exists but is unreadable' -ForegroundColor Yellow
  }
} else {
  Write-Host 'Supervisor: no heartbeat lock found' -ForegroundColor Yellow
}

Write-Host ''
if (Test-Path $heartbeatFile) {
  Get-Content -Path $heartbeatFile -TotalCount 36 -Encoding UTF8
} else {
  Write-Host ("No status file yet: {0}" -f $heartbeatFile) -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'Recent heartbeat log:' -ForegroundColor Cyan
if (Test-Path $heartbeatLogFile) {
  Get-Content -Path $heartbeatLogFile -Tail $LogLines -Encoding UTF8
} else {
  Write-Host ("No log file yet: {0}" -f $heartbeatLogFile) -ForegroundColor Yellow
}
