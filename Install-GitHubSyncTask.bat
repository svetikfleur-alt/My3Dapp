@echo off
setlocal
title Install My3DApp GitHub Sync Task
echo Installing scheduled GitHub sync task ...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-GitHubSyncTask.ps1"
echo.
echo Install exit code: %errorlevel%
pause
