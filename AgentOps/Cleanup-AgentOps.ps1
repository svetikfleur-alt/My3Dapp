# Cleanup-AgentOps.ps1
# Removes obsolete AgentOps artifacts: merged worktrees, stale build logs, test files.
# Run after a reconciliation pass when worktree branches are already in main.

$ErrorActionPreference = 'Continue'
$root = 'C:\Users\Lena\My3DApp'

Write-Host ""
Write-Host "=========================================="
Write-Host " AgentOps cleanup"
Write-Host "=========================================="
Write-Host ""

Push-Location $root
try {
  # 1. Keep external/agent worktrees untouched. This cleanup only owns AgentOps runtime artifacts.
  Write-Host "Skipping .claude/worktrees; cleanup is scoped to AgentOps runtime files." -ForegroundColor DarkGray
  Write-Host ""

  # 2. Old build logs in runner/
  $runnerDir = Join-Path $root "AgentOps\runner"
  if (Test-Path $runnerDir) {
    $logs = Get-ChildItem $runnerDir -File | Where-Object {
      ($_.Name -like "build_*.log") -or ($_.Name -like "build_*.log.err")
    } | Where-Object {
      $_.LastWriteTime -lt (Get-Date).AddHours(-12)
    }
    Write-Host ("Removing {0} old build logs (>12h old)..." -f $logs.Count) -ForegroundColor Yellow
    foreach ($log in $logs) {
      Remove-Item -Force $log.FullName -ErrorAction SilentlyContinue
    }
    Write-Host "Old build logs done." -ForegroundColor Green
  }
  Write-Host ""

  # 3. Test/scratch files in AgentOps/
  $junk = @(
    "AgentOps\testfile",
    "AgentOps\testfile_xyz",
    "AgentOps\CURRENT_TASK.md",
    "AgentOps\current_task_prompt.md",
    "AgentOps\LAST_CODEX_PROMPT.md",
    "AgentOps\LAST_CODEX_STDOUT.log",
    "AgentOps\LAST_CODEX_STDERR.log",
    "AgentOps\LAST_TEST_RESULT.json",
    "AgentOps\LAST_TEST_BUILD.log",
    "AgentOps\LAST_VERIFY_RESULT.json",
    "AgentOps\LAST_APP_BEHAVIOR.json",
    "AgentOps\LAST_HEALTH_RESULT.json",
    "AgentOps\LAST_QUEUE_AUTOWRITE.md",
    "AgentOps\APP_BEHAVIOR.md",
    "AgentOps\HEALTH.md",
    "AgentOps\PROGRESS.md",
    "AgentOps\RUN_SUMMARY.md",
    "AgentOps\STATUS.md",
    "AgentOps\HEARTBEAT.md",
    "AgentOps\HEARTBEAT_STATE.json",
    "AgentOps\HEARTBEAT.log",
    "AgentOps\HEARTBEAT.lock",
    "AgentOps\HEARTBEAT_LAST_WORKER.log",
    "AgentOps\HEARTBEAT_LAST_WORKER.err",
    "AgentOps\VERIFICATION.md",
    "AgentOps\RUNNER.lock",
    "AgentOps\LOCK",
    "AgentOps\build_result.json",
    "AgentOps\last_agent_failure.log",
    "AgentOps\spawner.state.json",
    "AgentOps\codex-home",
    "temp_video_review"
  )
  Write-Host "Removing runtime and scratch files..." -ForegroundColor Yellow
  foreach ($f in $junk) {
    $p = Join-Path $root $f
    if (Test-Path $p) {
      Remove-Item -Recurse -Force $p -ErrorAction SilentlyContinue
      Write-Host ("  - removed: {0}" -f $f)
    }
  }
  Write-Host ""

  # 4. Stale build_result.json (last build is older than 1 day)
  $br = Join-Path $root "AgentOps\build_result.json"
  if (Test-Path $br) {
    $age = (Get-Date) - (Get-Item $br).LastWriteTime
    if ($age.TotalDays -gt 1) {
      Write-Host "Removing stale build_result.json (>1 day old)..." -ForegroundColor Yellow
      Remove-Item -Force $br -ErrorAction SilentlyContinue
    }
  }

  # 5. Stale LOCK file in AgentOps/
  $lock = Join-Path $root "AgentOps\LOCK"
  if (Test-Path $lock) {
    $age = (Get-Date) - (Get-Item $lock).LastWriteTime
    if ($age.TotalHours -gt 6) {
      Write-Host "Clearing stale LOCK..." -ForegroundColor Yellow
      Set-Content -Path $lock -Value "1970-01-01T00:00:00Z" -Encoding UTF8
    }
  }

  Write-Host ""
  Write-Host "=========================================="
  Write-Host " Cleanup complete"
  Write-Host "=========================================="
}
finally {
  Pop-Location
}
