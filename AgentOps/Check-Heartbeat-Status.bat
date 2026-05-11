@echo off
setlocal
title AgentOps Heartbeat Status

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Check-Heartbeat-Status.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"
echo.
echo Exit code: %EXIT_CODE%
echo Press any key to close this window.
pause >nul
exit /b %EXIT_CODE%
