@echo off
echo Running Spawn-Claude.ps1 ...
echo If you see this and nothing else, PowerShell errored before producing output.
echo Look for red text below for the cause.
echo ----------------------------------------------------------
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Spawn-Claude.ps1"
echo ----------------------------------------------------------
echo Exit code: %errorlevel%
echo.
pause
