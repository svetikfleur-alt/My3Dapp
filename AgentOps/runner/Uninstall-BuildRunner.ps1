Unregister-ScheduledTask -TaskName 'AgentOps-BuildRunner' -Confirm:$false -ErrorAction SilentlyContinue
Write-Host "Removed AgentOps-BuildRunner."
