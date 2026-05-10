# 08. Save / Open project

## Goal
Implement file persistence so the user can save a project to disk and open it back, restoring all features, sketches, planes, and constraints.

## Scope
- Project format: JSON (deterministic, diffable). Versioned schema header.
- Save / Save As / Open via File menu and Ctrl+S / Ctrl+O shortcuts.
- Recent files list (last 5) persisted to user settings file.
- "Unsaved changes" indicator (window title gets a `*` suffix when dirty).
- Round-trip test harness: small console / unit test that creates a project, saves, reloads, asserts equality on a representative model.
- Project file extension: `.my3d`.

## Out of scope
- Binary/compressed format.
- Backup files / autosave (later).
- Multi-document UI (single-document MVP).

## Files likely involved
- New: `Engine/ProjectIO.cs` — Save/Load methods, schema versioning.
- `Engine/CadProjectStore.cs` — Snapshot + Restore methods consumed by ProjectIO.
- `AvaloniaApp/MainWindow.axaml(.cs)` — File menu items, dirty flag in title.
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — wiring.
- New test project scaffold under `temp_verify_io/` if the existing temp_verify pattern is appropriate.

## Expected behavior (acceptance)
1. File > Save As... prompts for path; default extension `.my3d`.
2. File > Save writes to current path, no prompt.
3. File > Open loads a project, replaces current state, viewport refreshes.
4. Title shows project name; trailing `*` when dirty; clears on save.
5. Recent files list shows last 5 opened files; clicking one opens it.
6. Round-trip test: model -> save -> open -> snapshot equal.
7. Schema version mismatch shows a clear error dialog and refuses to load.

## Notes / hints
- Use System.Text.Json with explicit Converter for any custom types; don't rely on accidental serializability.
- Keep IO synchronous-on-disk for now; UI layer can wrap in a Task.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Save -> close -> Open round-trips a simple Box + Rectangle sketch model.
- [ ] Title dirty marker appears after edit, disappears after save.
- [ ] Recent files list functions across two sessions.
- [ ] temp_verify_io (or equivalent) passes.
