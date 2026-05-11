# AgentOps merge protocol

When multiple code-tasks run in parallel worktrees and merge back into main, conflicts cost time. Every task you accept inherits these rules. The goal: a clean merge with zero manual conflict resolution at the end.

## 1. Declare your files up front

Before editing, list the files you will touch in HANDOFF.md (or in the final report). Categories:
- **Owned** — files only this task creates or fully rewrites. Other parallel tasks don't touch them.
- **Shared** — files multiple tasks may edit (MainWindow.axaml, WebViewportHost.cs, themes, CadProjectStore.cs). Edit minimally; localize changes.

If you find yourself drifting into a shared file you didn't expect, stop and reconsider scope.

## 2. Additive changes only on shared files

For shared files (MainWindow.axaml, WebViewportHost.cs, Studio.Light.axaml, Studio.Dark.axaml, CadProjectStore.cs):
- Add new sections; do NOT reorder or rewrite existing ones.
- Append your new XAML elements at the end of the relevant container, not interleaved.
- Append new C# methods at the bottom of the class, never reorder.
- Append new theme styles at the bottom of the theme file with a comment marker `<!-- task NN: <title> -->` immediately before your block.
- Never combine your changes with refactors of unrelated code.

## 3. New files for new functionality

If your task adds a new tool / dialog / feature, create new files for it:
- Dialogs go in `AvaloniaApp/Dialogs/<Name>.axaml(.cs)`.
- New controls go in `AvaloniaApp/Controls/<Name>.axaml(.cs)`.
- New engine ops go in `Engine/<Name>.cs`.

Don't bury new types inside existing large files unless absolutely necessary.

## 4. Use marker comments

Wrap your blocks of new code in shared files with markers so the merger can pick the right side:

```csharp
// region: task NN start
... your additions ...
// region: task NN end
```

```xaml
<!-- region: task NN start -->
... your additions ...
<!-- region: task NN end -->
```

These markers make 3-way merges trivial.

## 5. Never reformat

No reformatting passes, no whitespace changes, no rename refactors as part of your task. Stay surgical. If a file has 2-space indent and you prefer 4-space, leave it.

## 6. Commit hygiene

- One commit per task with the standard `agentops: NN — <title>` message.
- Don't commit lock files, build outputs, or `.claude/worktrees/` artifacts.
- If you need to fix a typo from a previous task that blocks your build, do that as a separate commit before your main one, with message `agentops: fix-build prep for NN`.

## 7. Final build before commit

Build must be 0 errors before you commit. Trigger via the build runner. Iterate up to 3 cycles. If you can't get to 0 errors, do not commit your changes — write the failure into HANDOFF.md and stop.

## 8. Tracking files always last

Update `TASK_QUEUE.md`, `DONE.md`, and `LOG.md` as the LAST step of your task, after all code commits land. These three files conflict trivially across tasks; keeping the touch surface small (one `[ ]` → `[x]`, one DONE bullet, one LOG line) makes resolution mechanical.

## 9. When two tasks both want the same shared change

Example: both task 06 (top panel polish) and task 11 (viewport polish) want to add hover styles in themes. Default rule: the lower-numbered task wins ownership of that shared edit. The higher-numbered task references task 06's styles instead of duplicating.

## 10. Reconcile cleanly, fail loud

If during reconciliation a real semantic conflict surfaces (two tasks wrote contradictory logic in the same function), do NOT silently pick one. Write a CONFLICT.md note in AgentOps with the file, lines, and both sides; mark the offending task as `partial` in DONE.md; let the user resolve.

---

This protocol is mandatory for every code-task. If a task can't follow it (genuine reason — e.g. a refactor truly is the task), the executor must call this out in HANDOFF.md before any code is changed.
