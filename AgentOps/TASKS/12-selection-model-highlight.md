# 12. Selection model + highlight

## Goal
Give the user a real CAD-style selection: hover pre-select, click-to-select with consistent precedence (face > edge > vertex > body), Shift to add, Ctrl to toggle, Esc / empty-click to clear. Selection state is shared between viewport, feature tree, status bar, and dialogs.

## Scope
- Hover pre-select highlight on geometry (3D bodies/faces/edges and sketch entities).
- Click selection with precedence rule; selection visible across viewport (accent outline) + feature tree (row highlight).
- Shift+click adds; Ctrl+click toggles; Esc or empty-click clears.
- Selection set exposed via the shell view-model so commands can consume it (Extrude reads sketch selection, Delete removes selected, etc.).
- Status bar selection segment shows count + most-recent type ("3 selected — face").
- Selection respects mode: in Sketch only sketch entities are selectable; in 3D only bodies/faces/edges.

## Out of scope
- Box / lasso select (later).
- Filter dropdown (face only / edge only / etc.) — later.
- Selection in nested assemblies (out of MVP per Blueprint).

## Files likely involved
- `AvaloniaApp/Controls/WebViewportHost.cs` — JS bridge for hover/click hit-test, selection messages.
- `AvaloniaApp/Controls/SoftwareViewportControl.cs` — fallback parity.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — Selection collection, event plumbing.
- `AvaloniaApp/MainWindow.axaml(.cs)` — feature tree row binding to active selection.
- `Engine/CadProjectStore.cs` — query helpers (entity by id, face by id).

## Expected behavior (acceptance)
1. Hover a body in 3D mode -> outline pre-select; leaving clears it.
2. Click body -> stays selected (accent), tree highlights matching node.
3. Shift+click another body -> both selected, count = 2 in status bar.
4. Ctrl+click a selected body -> deselects it.
5. Esc clears selection in any mode.
6. Click empty space clears selection.
7. In Sketch mode, only sketch entities are hoverable / selectable.
8. Selection set is consumed by Extrude (closed profile from selected entities, if any) and Delete.

## Notes / hints
- Three.js raycaster already in place — extend it; don't bolt on a parallel pipeline.
- Tree row highlight should be distinct from focus highlight (different brush).

## Verifier checklist
- [ ] Build runner ok.
- [ ] Hover + click on a Box body works in 3D mode.
- [ ] Shift+click adds; Ctrl+click toggles.
- [ ] Esc clears.
- [ ] Sketch entity selection works in Sketch mode but not in 3D.
- [ ] Status bar count matches selection set.
