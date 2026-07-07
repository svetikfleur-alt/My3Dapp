# Registers the AgentOps Build Runner as a Windows Task Scheduler task (user-level, no admin).
# Run once. To remove later: run Uninstall-BuildRunner.ps1.
# Uses a VBS launcher (BuildRunner-Hidden.vbs) so no console window flashes when the task fires.

$taskName = 'AgentOps-BuildRunner'
$vbs      = Join-Path $PSScriptRoot 'BuildRunner-Hidden.vbs'
$ps1      = Join-Path $PSScriptRoot 'BuildRunner.ps1'
$root     = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$reqFile  = Join-Path $root 'AgentOps\build_request.txt'

if (-not (Test-Path $ps1)) {
  Write-Error "BuildRunner.ps1 not found next to this installer."
  exit 1
}
if (-not (Test-Path $vbs)) {
  Write-Error "BuildRunner-Hidden.vbs not found next to this installer."
  exit 1
}

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

$action    = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument ('"{0}"' -f $vbs)
$trigger   = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1) -RepetitionDuration ([TimeSpan]::FromDays(3650))
$settings  = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -Hidden -ExecutionTimeLimit (New-TimeSpan -Minutes 30)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description "AgentOps build runner: silent wscript wrapper around BuildRunner.ps1." | Out-Null

Write-Host ("Installed: {0} (silent wscript wrapper, no console window)." -f $taskName)
Write-Host ("Test: drop a line into {0} and within 1 minute build_result.json should appear." -f $reqFile)
