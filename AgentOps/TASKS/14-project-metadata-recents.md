# 14. Project metadata + recent files

## Goal
Make the app feel like a real document-based tool: a startup screen showing recent projects, an editable project name, and project location visible in the title bar.

## Scope
- Startup screen / panel shown on launch when no project open: Recent Files list, "New Project", "Open...".
- Recent files list (last 10) persisted in user settings file (e.g. `%AppData%/My3DApp/recent.json`).
- Project name editable inline (or via Project menu); shown in title and feature tree root.
- Title format: `<ProjectName> — My3DApp` with `*` prefix when dirty.
- Project location (full file path) shown in a tooltip on the title or in About / Project Info.

## Out of scope
- Project templates / starter content.
- Cloud / shared projects (Blueprint forbids SaaS).
- Multi-document editing.

## Files likely involved
- New: `AvaloniaApp/Dialogs/StartupView.axaml(.cs)` (or hosted in MainWindow as a state).
- `AvaloniaApp/Services/UserSettingsService.cs` — recents persistence.
- `AvaloniaApp/MainWindow.axaml(.cs)` — title binding, project name editing.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — Project metadata props, dirty flag.

## Expected behavior (acceptance)
1. Launching with no last-open project shows Startup view with Recents / New / Open.
2. Last 10 opened projects appear; clicking one opens it.
3. Project name editable via Project menu or click-to-edit in tree root; persists with file.
4. Title shows `<name> — My3DApp`, with `*` when dirty.
5. Hover title -> tooltip shows full file path.
6. Recents survive an app restart.

## Notes / hints
- Coordinate with task 08 (Save/Open) — they share the dirty-flag and file-path infrastructure.
- Keep the startup view minimal; no marketing copy, no SaaS chrome.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Startup view appears on first launch.
- [ ] Recents shown after opening 2+ files.
- [ ] Project name edit reflected in title and tree.
- [ ] Dirty marker behaves correctly with edit + save.
