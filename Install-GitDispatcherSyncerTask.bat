@echo off
setlocal
title Install My3DApp Git Dispatcher Syncer Task
echo Installing scheduled Git dispatcher syncer task ...
echo.
call "%~dp0Git-Dispatcher-Syncer.bat" install-schedule %*
set "INSTALL_EXIT=%ERRORLEVEL%"
echo.
echo Install exit code: %INSTALL_EXIT%
pause
exit /b %INSTALL_EXIT%
