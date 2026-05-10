@echo off
setlocal
title My3DApp GitHub Sync
echo Running GitHub-Sync.ps1 ...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0GitHub-Sync.ps1"
echo.
echo Sync exit code: %errorlevel%
pause

