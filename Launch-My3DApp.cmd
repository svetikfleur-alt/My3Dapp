@echo off
setlocal

set "ROOT=%~dp0"
set "INSTALL_SCRIPT=%ROOT%Install-My3DApp.ps1"
set "APP_EXE=%ROOT%dist\My3DApp\My3DApp.exe"

if not exist "%INSTALL_SCRIPT%" (
    echo Install script not found: "%INSTALL_SCRIPT%"
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%INSTALL_SCRIPT%" -Configuration Debug -UiHost Avalonia -CloseRunningInstances -NoShortcuts
if errorlevel 1 (
    echo.
    echo Install failed. My3DApp Studio was not launched.
    exit /b 1
)

if not exist "%APP_EXE%" (
    echo Installed executable not found: "%APP_EXE%"
    exit /b 1
)

start "" "%APP_EXE%"
exit /b 0
