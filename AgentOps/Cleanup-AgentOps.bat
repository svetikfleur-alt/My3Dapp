@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Cleanup-AgentOps.ps1"
echo.
pause
