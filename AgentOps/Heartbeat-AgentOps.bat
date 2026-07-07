@echo off
setlocal EnableDelayedExpansion
title AgentOps Heartbeat Co-Pilot

for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
for %%I in ("%~f0") do set "BAT_STAMP=%%~tI"
for %%I in ("%~dp0Heartbeat-AgentOps.ps1") do set "PS1_STAMP=%%~tI"

echo ==========================================================
echo AgentOps Heartbeat Co-Pilot
echo Project: %PROJECT_ROOT%
echo Launcher: %~f0
echo Launcher modified: %BAT_STAMP%
echo Script: %~dp0Heartbeat-AgentOps.ps1
echo Script modified: %PS1_STAMP%
echo ==========================================================
echo.
echo This is the always-visible supervisor.
echo It launches Spawn-Codex.ps1 one safe task pass at a time,
echo writes heartbeat status, and keeps this window open.
echo Default cadence: one worker pass every 5 minutes.
echo Progress refresh: every 15 seconds while waiting/running.
echo Override example: Heartbeat-AgentOps.bat -IntervalSeconds 300 -ProgressTickSeconds 15
echo Background terminal spawning is disabled; use this visible window.
echo.
echo Live files:
echo   %~dp0HEARTBEAT.md
echo   %~dp0HEARTBEAT_STATE.json
echo   %~dp0HEARTBEAT.log
echo   %~dp0PROGRESS.md
echo   %~dp0STATUS.md
echo   %~dp0RUN_SUMMARY.md
echo   %~dp0HEARTBEAT_LAST_WORKER.log
echo   %~dp0HEARTBEAT_LAST_WORKER.err
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Heartbeat-AgentOps.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo ==========================================================
if "%EXIT_CODE%"=="0" (
  echo Heartbeat result: success / queue clean ^(exit code 0^)
) else if "%EXIT_CODE%"=="2" (
  echo Heartbeat result: manual review required ^(exit code 2^)
) else if "%EXIT_CODE%"=="10" (
  echo Heartbeat result: paused/resumable ^(exit code 10^)
) else if "%EXIT_CODE%"=="124" (
  echo Heartbeat result: worker timeout ^(exit code 124^)
) else (
  echo Heartbeat result: failure ^(exit code %EXIT_CODE%^)
)
echo ==========================================================
echo.
echo Inspect:
echo   %~dp0HEARTBEAT.md
echo   %~dp0HEARTBEAT.log
echo   %~dp0RUN_SUMMARY.md
echo   %~dp0VERIFICATION.md
echo   %~dp0APP_BEHAVIOR.md
echo.
echo Press any key to close this window.
pause >nul

exit /b %EXIT_CODE%
