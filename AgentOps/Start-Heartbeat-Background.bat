@echo off
setlocal
title AgentOps Heartbeat Co-Pilot

echo Starting AgentOps heartbeat in this terminal...
echo Default cadence: one worker pass every 5 minutes.
echo Progress refresh: every 15 seconds.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Heartbeat-Background.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if "%EXIT_CODE%"=="0" (
  echo Heartbeat finished successfully.
) else (
  echo Heartbeat stopped with exit code %EXIT_CODE%.
)
echo.
echo Press any key to close this window.
pause >nul
exit /b %EXIT_CODE%
