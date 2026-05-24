@echo off
setlocal
title My3DApp Git Dispatcher Syncer

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Git-Dispatcher-Syncer.ps1" %*
set "DISPATCHER_EXIT=%ERRORLEVEL%"

echo.
echo Dispatcher exit code: %DISPATCHER_EXIT%
exit /b %DISPATCHER_EXIT%
