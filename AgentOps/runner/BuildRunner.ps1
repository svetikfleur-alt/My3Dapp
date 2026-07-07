# AgentOps Build Runner - watches AgentOps/build_request.txt, runs dotnet build, writes AgentOps/build_result.json
# Runs every minute via Task Scheduler. Exits silently if no request.

$ErrorActionPreference = 'Stop'
$root        = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$agentOps    = Join-Path $root 'AgentOps'
$runnerDir   = Join-Path $agentOps 'runner'
$reqFile     = Join-Path $agentOps 'build_request.txt'
$resFile     = Join-Path $agentOps 'build_result.json'
$logFile     = Join-Path $runnerDir 'runner.log'
$lockFile    = Join-Path $runnerDir 'runner.lock'

function W-Log($msg) {
  $line = "{0}  {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $msg
  Add-Content -Path $logFile -Value $line
}

# Lock - skip if a prior run is still active and fresh (< 15 min)
if (Test-Path $lockFile) {
  $age = (Get-Date) - (Get-Item $lockFile).LastWriteTime
  if ($age.TotalMinutes -lt 15) { exit 0 }
  Remove-Item $lockFile -Force
}

if (-not (Test-Path $reqFile)) { exit 0 }

# Acquire lock
Set-Content -Path $lockFile -Value (Get-Date -Format o)

try {
  $reqContent = Get-Content $reqFile -Raw -ErrorAction SilentlyContinue
  W-Log ("REQUEST received: " + ($reqContent -replace "`n", " | ").Trim())

  $sln = Join-Path $root 'My3DApp.sln'
  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $buildLog = Join-Path $runnerDir ("build_{0}.log" -f $stamp)

  $proc = Start-Process -FilePath 'dotnet' `
                        -ArgumentList @('build', $sln, '-nologo', '-v', 'minimal') `
                        -WorkingDirectory $root `
                        -NoNewWindow -PassThru `
                        -RedirectStandardOutput $buildLog `
                        -RedirectStandardError ($buildLog + '.err')

  $proc | Wait-Process -Timeout 600 -ErrorAction SilentlyContinue
  if (-not $proc.HasExited) {
    $proc | Stop-Process -Force
    $exit = -1
  } else {
    $exit = $proc.ExitCode
  }

  $stdoutTail = ''
  if (Test-Path $buildLog) { $stdoutTail = (Get-Content $buildLog -Tail 40 | Out-String).Trim() }
  $stderrTail = ''
  if (Test-Path ($buildLog + '.err')) { $stderrTail = (Get-Content ($buildLog + '.err') -Tail 40 | Out-String).Trim() }

  $errorCount = 0
  $warnCount  = 0
  if (Test-Path $buildLog) {
    $errorCount = (Select-String -Path $buildLog -Pattern '\berror\b' -CaseSensitive:$false -SimpleMatch:$false | Measure-Object).Count
    $warnCount  = (Select-String -Path $buildLog -Pattern '\bwarning\b' -CaseSensitive:$false -SimpleMatch:$false | Measure-Object).Count
  }

  $result = [ordered]@{
    timestamp   = (Get-Date -Format o)
    exit_code   = $exit
    success     = ($exit -eq 0)
    errors      = $errorCount
    warnings    = $warnCount
    log_file    = $buildLog
    stdout_tail = $stdoutTail
    stderr_tail = $stderrTail
    request     = $reqContent
  }
  $result | ConvertTo-Json -Depth 4 | Set-Content -Path $resFile -Encoding UTF8
  W-Log ("RESULT exit={0} errors={1} warnings={2}" -f $exit, $errorCount, $warnCount)

  Remove-Item $reqFile -Force
}
catch {
  W-Log ("EXCEPTION: " + $_.Exception.Message)
  $err = [ordered]@{
    timestamp = (Get-Date -Format o)
    exit_code = -2
    success   = $false
    errors    = -1
    warnings  = -1
    error     = $_.Exception.Message
  }
  $err | ConvertTo-Json -Depth 3 | Set-Content -Path $resFile -Encoding UTF8
}
finally {
  if (Test-Path $lockFile) { Remove-Item $lockFile -Force }
}
