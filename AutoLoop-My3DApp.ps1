<#
.SYNOPSIS
  Periodically runs a command for My3DApp with runtime limits and restart cooldowns.

.DESCRIPTION
  This script loops every 5 minutes and runs the configured target command.
  If the target process exceeds 10 minutes, it is terminated and the script waits 4 hours before restarting.
  It is intended as a lightweight self-pacing automation wrapper for project tasks.

.PARAMETER TargetCommand
  The command to execute on each loop. Default is .\Build-My3DApp.ps1.

.PARAMETER LoopIntervalMinutes
  Minutes between loop starts when the previous execution finished normally.

.PARAMETER ExecutionTimeoutMinutes
  Time limit for each execution. If exceeded, the process is terminated.

.PARAMETER CooldownHours
  Sleep time after a timeout-limit hit before the next attempt.
#>

param(
    [string]$TargetCommand = ".\Build-My3DApp.ps1",
    [int]$LoopIntervalMinutes = 5,
    [int]$ExecutionTimeoutMinutes = 10,
    [int]$CooldownHours = 4
)

$ErrorActionPreference = 'Stop'

function Stop-ProcessTree {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    try {
        Start-Process -FilePath 'taskkill.exe' -ArgumentList '/PID', $ProcessId, '/T', '/F' -NoNewWindow -Wait | Out-Null
    }
    catch {
        try { Stop-Process -Id $ProcessId -Force -ErrorAction Stop } catch { }
    }
}

function Start-TargetProcess {
    param(
        [string]$CommandLine
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'powershell.exe'
    $startInfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command & { $CommandLine }"
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $process.Start() | Out-Null
    return $process
}

function Invoke-RunCycle {
    param(
        [string]$CommandLine
    )

    Write-Host "[AutoLoop] Starting target command: $CommandLine" -ForegroundColor Cyan
    $process = Start-TargetProcess -CommandLine $CommandLine
    $hasTimedOut = -not $process.WaitForExit($ExecutionTimeoutMinutes * 60 * 1000)

    if ($hasTimedOut) {
        Write-Warning "[AutoLoop] Execution exceeded $ExecutionTimeoutMinutes minutes. Terminating process tree."
        Stop-ProcessTree -ProcessId $process.Id
        Write-Host "[AutoLoop] Cooldown for $CooldownHours hour(s) before next restart." -ForegroundColor Yellow
        Start-Sleep -Seconds ($CooldownHours * 60 * 60)
        return $false
    }

    $exitCode = $process.ExitCode
    if ($exitCode -ne 0) {
        Write-Warning "[AutoLoop] Target command exited with code $exitCode." -ForegroundColor Yellow
    }
    else {
        Write-Host "[AutoLoop] Target command completed successfully." -ForegroundColor Green
    }

    return $true
}

$absoluteCommand = if ([System.IO.Path]::IsPathRooted($TargetCommand)) { $TargetCommand } else { Join-Path -Path (Get-Location) -ChildPath $TargetCommand }

Write-Host "[AutoLoop] Starting automation wrapper." -ForegroundColor Green
Write-Host "[AutoLoop] Command: $absoluteCommand" -ForegroundColor Green
Write-Host "[AutoLoop] Loop interval: $LoopIntervalMinutes minutes" -ForegroundColor Green
Write-Host "[AutoLoop] Execution timeout: $ExecutionTimeoutMinutes minutes" -ForegroundColor Green
Write-Host "[AutoLoop] Cooldown after timeout: $CooldownHours hours" -ForegroundColor Green
Write-Host ""

while ($true) {
    $cycleStart = Get-Date
    $success = Invoke-RunCycle -CommandLine $absoluteCommand

    if ($success) {
        $elapsed = (Get-Date) - $cycleStart
        $sleepSeconds = [Math]::Max(0, ($LoopIntervalMinutes * 60) - [int]$elapsed.TotalSeconds)
        if ($sleepSeconds -gt 0) {
            Write-Host "[AutoLoop] Sleeping for $sleepSeconds seconds before next loop." -ForegroundColor DarkGray
            Start-Sleep -Seconds $sleepSeconds
        }
    }
    else {
        Write-Host "[AutoLoop] Resuming after cooldown." -ForegroundColor DarkGray
    }
}
