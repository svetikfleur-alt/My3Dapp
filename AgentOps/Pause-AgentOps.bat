@echo off
set TARGET=%~dp0PAUSE
echo paused at %DATE% %TIME% > "%TARGET%"
echo.
echo === AgentOps PAUSED ===
echo Conveyor halted. Orchestrator, auditor, reporter will skip future runs.
echo To resume: double-click Resume-AgentOps.bat
echo.
pause
