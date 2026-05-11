@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"
title AgentOps

set "AGENTOPS_DIR=%~dp0"
for %%I in ("%AGENTOPS_DIR%..") do set "PROJECT_ROOT=%%~fI"
set "QUEUE_FILE=%AGENTOPS_DIR%TASK_QUEUE.md"
set "CURRENT_FILE=%AGENTOPS_DIR%CURRENT_TASK.md"
set "STATUS_FILE=%AGENTOPS_DIR%STATUS.md"
set "SPAWNER_PS1=%AGENTOPS_DIR%Spawn-NextTask.ps1"
set "RUNNER_INSTALL_PS1=%AGENTOPS_DIR%runner\Install-BuildRunner.ps1"
set "PAUSE_FILE=%AGENTOPS_DIR%PAUSE"
set "LOCK_FILE=%AGENTOPS_DIR%LOCK"
set "RUNNER_TASK=AgentOps-BuildRunner"

if "%~1"=="" goto run

if /I "%~1"=="run" goto run
if /I "%~1"=="status" goto status
if /I "%~1"=="pause" goto pause
if /I "%~1"=="resume" goto resume
if /I "%~1"=="cleanup" goto cleanup
if /I "%~1"=="runner-install" goto runnerinstall
if /I "%~1"=="help" goto help

echo Unknown command: %~1
echo.
goto help

:run
call :banner "AgentOps auto development"
call :validate
if errorlevel 1 exit /b 1

if exist "%PAUSE_FILE%" (
    echo AgentOps is paused.
    echo Run: orchestrator.bat resume
    exit /b 2
)

if exist "%LOCK_FILE%" (
    echo Detected lock file:
    echo   %LOCK_FILE%
    echo.
    echo Continuing anyway, but another AgentOps loop may already be active.
    echo Use: orchestrator.bat status
    echo.
)

call :ensure_runner
if errorlevel 1 exit /b 1

call :show_summary
echo.
echo Starting autonomous spawner...
echo Project root: %PROJECT_ROOT%
echo.
pushd "%PROJECT_ROOT%"
powershell -NoProfile -ExecutionPolicy Bypass -File "%SPAWNER_PS1%"
set "SPAWNER_EXIT=%ERRORLEVEL%"
popd
echo.
echo AgentOps finished with exit code %SPAWNER_EXIT%.
exit /b %SPAWNER_EXIT%

:status
call :banner "AgentOps status"
call :validate
if errorlevel 1 exit /b 1
call :show_summary
echo.
if exist "%PAUSE_FILE%" (
    echo State: PAUSED
) else (
    echo State: RUNNING
)
if exist "%LOCK_FILE%" (
    echo Lock : present
) else (
    echo Lock : none
)
schtasks /Query /TN "%RUNNER_TASK%" >nul 2>&1
if errorlevel 1 (
    echo Runner: not installed
    exit /b 0
)
echo Runner: installed
exit /b 0

:pause
call "%AGENTOPS_DIR%Pause-AgentOps.bat"
exit /b %ERRORLEVEL%

:resume
call "%AGENTOPS_DIR%Resume-AgentOps.bat"
exit /b %ERRORLEVEL%

:cleanup
call "%AGENTOPS_DIR%Cleanup-AgentOps.bat"
exit /b %ERRORLEVEL%

:runnerinstall
call :banner "Install AgentOps build runner"
call :validate
if errorlevel 1 exit /b 1
pushd "%PROJECT_ROOT%"
powershell -NoProfile -ExecutionPolicy Bypass -File "%RUNNER_INSTALL_PS1%"
set "RUNNER_EXIT=%ERRORLEVEL%"
popd
exit /b %RUNNER_EXIT%

:help
echo AgentOps orchestrator
echo.
echo Agent priority:
echo   1. codex
echo   2. claude
echo   3. copilot
echo   The first available agent is used.
echo.
echo Usage:
echo   orchestrator.bat run            ^(default^)
echo   orchestrator.bat status
echo   orchestrator.bat pause
echo   orchestrator.bat resume
echo   orchestrator.bat cleanup
echo   orchestrator.bat runner-install
echo   orchestrator.bat help
echo.
echo This is the main Windows entrypoint for autonomous AgentOps development.
exit /b 0

:validate
if not exist "%QUEUE_FILE%" (
    echo Missing queue file:
    echo   %QUEUE_FILE%
    exit /b 1
)
if not exist "%SPAWNER_PS1%" (
    echo Missing spawner:
    echo   %SPAWNER_PS1%
    exit /b 1
)
if not exist "%RUNNER_INSTALL_PS1%" (
    echo Missing runner installer:
    echo   %RUNNER_INSTALL_PS1%
    exit /b 1
)
exit /b 0

:ensure_runner
schtasks /Query /TN "%RUNNER_TASK%" >nul 2>&1
if not errorlevel 1 (
    echo Build runner already installed.
    exit /b 0
)

echo Build runner not found. Installing it now...
pushd "%PROJECT_ROOT%"
powershell -NoProfile -ExecutionPolicy Bypass -File "%RUNNER_INSTALL_PS1%"
set "RUNNER_EXIT=%ERRORLEVEL%"
popd
if not "%RUNNER_EXIT%"=="0" (
    echo Failed to install build runner. Exit code: %RUNNER_EXIT%
    exit /b %RUNNER_EXIT%
)
echo Build runner installed.
exit /b 0

:show_summary
set "OPEN_COUNT=0"
set "DONE_COUNT=0"
set "NEXT_TASK="

for /f "usebackq delims=" %%L in ("%QUEUE_FILE%") do (
    set "LINE=%%L"
    echo(!LINE!| findstr /B /C:"- [ ] " >nul && (
        set /a OPEN_COUNT+=1
        if not defined NEXT_TASK set "NEXT_TASK=!LINE!"
    )
    echo(!LINE!| findstr /B /C:"- [x] " >nul && set /a DONE_COUNT+=1
)

echo Queue: !DONE_COUNT! done, !OPEN_COUNT! open
echo Agents: codex ^> claude ^> copilot ^(first available^)
if defined NEXT_TASK (
    echo Next : !NEXT_TASK!
)

if exist "%CURRENT_FILE%" (
    echo.
    echo Current task file:
    for /f "usebackq delims=" %%L in ("%CURRENT_FILE%") do echo   %%L
)

if exist "%STATUS_FILE%" (
    echo.
    echo Status file:
    for /f "usebackq delims=" %%L in ("%STATUS_FILE%") do (
        echo   %%L
    )
)
exit /b 0

:banner
echo.
echo ============================================================
echo %~1
echo ============================================================
echo.
exit /b 0
