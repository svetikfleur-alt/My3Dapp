@echo off
setlocal
title AgentOps Codex Runner

echo ==========================================================
echo AgentOps Codex sequential runner
echo Project: C:\Users\Lena\My3DApp
echo Script : %~dp0Spawn-Codex.ps1
echo Log    : %~dp0worker.log
echo ==========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Spawn-Codex.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo ==========================================================
echo Runner exit code: %EXIT_CODE%
echo ==========================================================
echo.
echo Inspect if needed:
echo   %~dp0worker.log
echo   %~dp0RUN_SUMMARY.md
echo   %~dp0PROGRESS.md
echo   %~dp0LAST_CODEX_STDOUT.log
echo   %~dp0LAST_CODEX_STDERR.log
echo.
echo Press any key to close this window.
pause >nul

exit /b %EXIT_CODE%
