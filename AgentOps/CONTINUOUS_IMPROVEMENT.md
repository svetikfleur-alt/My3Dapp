# Continuous Improvement Runner

Use this as the main long-running AgentOps entrypoint.

Files:
- `AgentOps\Start-Continuous-Improvement.bat`
- `AgentOps\Start-Continuous-Improvement.ps1`

What it does:
- runs the existing `Heartbeat-AgentOps.ps1` supervisor in the same visible terminal
- keeps the queue moving until it is clean or a failure stops the run
- shows where to watch status and logs while it works
- preserves the existing tester / verifier / commit loop already implemented in `Spawn-Codex.ps1`

Recommended commands:

Continuous loop:
```powershell
.\AgentOps\Start-Continuous-Improvement.bat
```

One worker pass only:
```powershell
.\AgentOps\Start-Continuous-Improvement.bat -Once
```

Prepare AgentOps state only:
```powershell
.\AgentOps\Start-Continuous-Improvement.bat -PrepareOnly
```

Show current supervisor status:
```powershell
.\AgentOps\Start-Continuous-Improvement.bat -StatusOnly
```

Useful files to watch:
- `AgentOps\HEARTBEAT.md`
- `AgentOps\PROGRESS.md`
- `AgentOps\RUN_SUMMARY.md`
- `AgentOps\HEARTBEAT.log`
- `AgentOps\worker.log`

Behavior:
- queue seeds can still come from `AgentOps\TASK_SOURCE.md`
- queue execution still comes from `AgentOps\TASK_QUEUE.md`
- the heartbeat loop still stops safely on:
  - Codex failure
  - tester failure
  - verifier failure
  - commit failure
  - worker timeout

This file is only the human-facing entrypoint guide.
