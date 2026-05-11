# 04. Plane selection + sketch entry workflow

## Goal
Make starting a sketch feel deterministic: user explicitly picks a plane (or selects an existing one in the feature tree) before entering Sketch mode; the active plane is visibly indicated in the viewport and feature tree throughout the sketch session.

## Scope
- Select-plane dialog already exists; promote it into the workflow:
  - If no plane is selected when "Start Sketch" is clicked: open SelectPlaneDialog with Top / Front / Right + any reference planes; OK enters Sketch mode on that plane.
  - If a plane is pre-selected (in feature tree or viewport): "Start Sketch" enters Sketch mode directly with no dialog.
- Visible feedback during sketch:
  - Viewport: highlight the active plane (translucent fill in accent color).
  - Feature tree: bold or accent-tint the active plane node.
  - Top panel: mode indicator changes to "Sketch on <plane name>".
- Exit sketch: Finish or Cancel returns to 3D mode; clears active-plane highlight.

## Out of scope
- Custom plane creation (separate feature: Datum Plane).
- Sketch-on-face (later; current MVP is sketch-on-plane).

## Files likely involved
- `AvaloniaApp/Dialogs/SelectPlaneDialog.axaml(.cs)`.
- `AvaloniaApp/MainWindow.axaml(.cs)` — Start Sketch command, mode indicator.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — selected plane state, sketch session lifecycle.
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — viewport message for active-plane highlight.
- `Engine/CadProjectStore.cs` — sketch session start/finish accepting plane id.

## Expected behavior (acceptance)
1. With no selection, Start Sketch opens SelectPlaneDialog.
2. Picking Top -> dialog closes, mode = Sketch, viewport shows Top plane highlighted, feature tree marks Top as active.
3. With a plane node selected in the feature tree, Start Sketch enters sketch directly (no dialog).
4. Top panel shows "Sketch on Top" while sketching.
5. Finish or Cancel exits sketch, clears highlight, restores 3D mode.
6. Cannot enter sketch from a non-plane selection (dialog opens instead).

## Notes / hints
- Re-use existing SelectPlaneDialog; do not introduce a second variant.
- Highlight color: subtle translucent accent; do not occlude geometry.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Start Sketch with nothing selected -> dialog appears.
- [ ] Start Sketch with Top selected in tree -> direct entry.
- [ ] Mode indicator and tree highlight both reflect active plane.
- [ ] Cancel/Finish clears state cleanly; no leftover plane glow.
