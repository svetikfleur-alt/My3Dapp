@echo off
set TARGET=%~dp0PAUSE
if exist "%TARGET%" (
  del /q "%TARGET%"
  echo === AgentOps RESUMED ===
  echo Conveyor will pick up at the next scheduled tick.
) else (
  echo === AgentOps was not paused ===
  echo No PAUSE file found, conveyor was already running.
)
echo.
pause
