@echo off
setlocal
title AgentOps Continuous Improvement

echo ==========================================================
echo AgentOps continuous improvement runner
echo Project : C:\Users\Lena\My3DApp
echo Script  : %~dp0Start-Continuous-Improvement.ps1
echo Status  : %~dp0HEARTBEAT.md
echo Logs    : %~dp0HEARTBEAT.log
echo Worker  : %~dp0worker.log
echo ==========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Continuous-Improvement.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo ==========================================================
echo Continuous runner exit code: %EXIT_CODE%
echo ==========================================================
echo.
echo Inspect if needed:
echo   %~dp0HEARTBEAT.md
echo   %~dp0PROGRESS.md
echo   %~dp0RUN_SUMMARY.md
echo   %~dp0HEARTBEAT.log
echo   %~dp0worker.log
echo.
echo Press any key to close this window.
pause >nul
exit /b %EXIT_CODE%
