[CmdletBinding()]
param(
    [string]$TaskName = "My3DApp GitHub Sync",
    [int]$EveryMinutes = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$scriptPath = Join-Path $repoRoot "GitHub-Sync.ps1"

if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Sync script not found: $scriptPath"
}

$escapedScriptPath = $scriptPath.Replace('"', '\"')
$taskCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$escapedScriptPath`""

Write-Host ("Installing scheduled task '{0}' to run every {1} minutes..." -f $TaskName, $EveryMinutes) -ForegroundColor Cyan

& schtasks /Create /SC MINUTE /MO $EveryMinutes /TN $TaskName /TR $taskCommand /F | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create scheduled task '$TaskName'."
}

Write-Host ("Scheduled task '{0}' installed successfully." -f $TaskName) -ForegroundColor Green
