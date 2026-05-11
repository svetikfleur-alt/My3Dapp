# 30. Export panel (STL / OBJ)

## Goal
Wire the existing `STLExporter` and `OBJExporter` classes in `Export/` to a real export UI. The top-panel Export button currently renders a raw "↓" placeholder and does nothing; the exporters are complete but unreachable from the user. Make export a first-class action: pick format, pick destination, export selected body (or entire scene).

## Scope
- **Export dialog** — opens when the user clicks the Export button (top panel) or presses `Ctrl+E`. Contains:
  - Format selector: STL (default) / OBJ radio buttons.
  - Scope selector: "Selected body" / "All bodies" radio buttons (disable "Selected body" when nothing is selected).
  - Target file path — text field + "Browse…" button that opens a native `SaveFileDialog` filtered to `.stl` / `.obj`.
  - Export button (primary) / Cancel button.
- On Export:
  1. Compile the current project via `CadProjectStore.Compile()`.
  2. For "All bodies", merge meshes; for "Selected body", compile only that body.
  3. Call `STLExporter.Export(mesh, path)` or `OBJExporter.Export(mesh, path)`.
  4. On success: status-bar message "Exported to <filename>" and dialog closes.
  5. On error: inline error label in dialog; dialog stays open.
- Replace the "↓" placeholder in `MainWindow.axaml` with a proper Export button (SVG icon or themed label until icon assets exist); bind `Ctrl+E`.

## Out of scope
- STEP / IGES / 3MF export (future).
- Export of reference planes or sketch entities.
- Batch export of multiple projects.
- Per-body material / color export metadata.

## Files likely involved
- `AvaloniaApp/Dialogs/ExportDialog.axaml(.cs)` — new dialog; follows `ToolDialogWindow` pattern.
- `AvaloniaApp/MainWindow.axaml(.cs)` — replace "↓" Export placeholder with `ExportCommand`; bind `Ctrl+E`; route to dialog.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — `ExportCommand` ICommand; CanExecute always true (full-scene export always valid).
- `Export/STL Exporter.cs`, `Export/OBJ Exporter.cs` — no changes expected; confirm both accept `Mesh` and a file path.
- `Engine/CadProjectStore.cs` — `Compile()` already exists; ensure it returns a mesh usable by the exporters (check `CadCompileResult` shape).

## Expected behavior (acceptance)
1. `Ctrl+E` and the Export toolbar button both open the Export dialog.
2. Format = STL, Scope = All bodies, path chosen → clicking Export produces a valid `.stl` file on disk; status bar reads "Exported to model.stl".
3. Format = OBJ, Scope = All bodies → valid `.obj` file with `v` and `f` lines.
4. Scope = "Selected body" with no selection → radio button is disabled (cannot choose it).
5. Scope = "Selected body" with a body selected → only that body's mesh is exported.
6. Browse button opens native `SaveFileDialog` with correct extension filter.
7. Path field missing → Export button disabled with tooltip "Choose a file path".
8. Compile error (e.g. invalid geometry) → inline error label shown, file NOT written.
9. Cancel → no file written, dialog closes.
10. Build: 0 errors, 0 warnings.

## Notes / hints
- `CadCompileResult` is returned by `CadProjectStore.Compile()`. Inspect its shape to find the `Mesh` (or collection of meshes per body) — the exporters need a single `Mesh` object.
- For "All bodies" merging: create a new `Mesh` by concatenating vertices and re-indexing triangles from each body mesh.
- `SaveFileDialog` in Avalonia: use `Avalonia.Platform.Storage.IStorageProvider.SaveFilePickerAsync` (Avalonia 11+) rather than the WinForms dialog — it works cross-platform and doesn't trigger the WinForms namespace clash.
- The `ToolDialogWindow` modal pattern from task 20 is the right scaffold — use `ShowDialog<bool>` and handle the result in `MainWindow`.
- Export button icon: until a real SVG asset exists, a `TextBlock` with "Export" text is acceptable. Task 06 (top-panel polish) can swap in an icon later.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] `Ctrl+E` opens Export dialog.
- [ ] Export toolbar button opens Export dialog.
- [ ] STL file produced: open in a mesh viewer or check file starts with "solid model".
- [ ] OBJ file produced: file contains `v` and `f` lines.
- [ ] "Selected body" radio disabled when selection is empty.
- [ ] "Selected body" exports only the selected mesh.
- [ ] Missing path → Export button disabled.
- [ ] Cancel → no file written.
- [ ] Status bar confirms export path on success.
- [ ] One commit.

## Complexity
Low-moderate. Exporters are complete; only UI wiring and dialog scaffolding needed. No geometry work. Sonnet is fine.
