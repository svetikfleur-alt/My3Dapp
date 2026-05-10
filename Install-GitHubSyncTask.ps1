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

$action = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$scriptPath`""

$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date
$trigger.Repetition = New-ScheduledTaskRepetitionSettingsSet `
    -Interval (New-TimeSpan -Minutes $EveryMinutes) `
    -Duration (New-TimeSpan -Days 3650)

$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -MultipleInstances IgnoreNew

$principal = New-ScheduledTaskPrincipal `
    -UserId $env:USERNAME `
    -LogonType Interactive `
    -RunLevel Limited

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Force `
    -Description "Auto-sync My3DApp local folder to GitHub phone-cloud-sync branch."

Write-Host ("Installed scheduled task '{0}' to run every {1} minutes." -f $TaskName, $EveryMinutes) -ForegroundColor Green
