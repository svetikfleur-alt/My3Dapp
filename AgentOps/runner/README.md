# AgentOps Build Runner

Tiny Windows-side helper that lets the AgentOps verifier actually build your project.

## Install (one time)
Double-click `Install-BuildRunner.bat`. Approve any prompts. Done.

That registers a user-level Windows scheduled task `AgentOps-BuildRunner` that wakes every minute, checks for `AgentOps/build_request.txt`, and runs `dotnet build My3DApp.sln` if found.

## What it does
- Reads `AgentOps/build_request.txt` (anything inside is logged as the request marker).
- Runs `dotnet build` on the solution.
- Writes `AgentOps/build_result.json` with: exit_code, success, errors, warnings, stdout/stderr tails, full log file path.
- Deletes the request file when done.

## Uninstall
Run `Uninstall-BuildRunner.ps1` from PowerShell.

## Logs
- `runner/runner.log` — short per-run log.
- `runner/build_*.log` — full dotnet output per build.

## Troubleshooting
- No `build_result.json` after 2 minutes → open Task Scheduler, find `AgentOps-BuildRunner`, check Last Run Result.
- If the task points at an old repo path, rerun `Install-BuildRunner.bat` from the current checkout to refresh the scheduled task action.
- "dotnet not found" → `winget install Microsoft.DotNet.SDK.10` (or whichever channel matches the project).
