# 29. Hole feature

## Goal
Add a Hole feature — one of the most-used operations in parametric CAD. The user picks a face, positions the hole center (click on face or type X/Y in the dialog), specifies diameter and depth, and the engine cuts a cylindrical bore into the selected body. AUDIT.md lists "No Hole feature" as a gap; no existing task covers it.

## Scope
- **Simple (through or blind) cylindrical hole**:
  - Face selection: user clicks a face in the viewport; the dialog pre-fills with that face.
  - Center position: X/Y offset from face centroid (editable numeric fields); live preview as a translucent cylinder.
  - Diameter: numeric stepper, minimum > 0.
  - Depth type: "Through All" (cuts to the opposite face) or "Blind" (explicit depth value).
  - Depth value: numeric, enabled only when Blind is selected.
- Opens via `ToolDialogWindow` scaffold matching existing feature dialogs.
- Committed as a feature tree node "Hole (⌀<d> × <depth/Through>)".
- Editable post-commit (double-click → dialog with current params).
- Implemented via a Subtract Boolean between the host body and a cylinder primitive of the given diameter/depth. If the geometry backend can't do CSG, stub the subtraction as a TODO and persist the parameters — tree node still shows; mark in HANDOFF.

## Out of scope
- Countersink / counterbore / threaded holes (future Hole wizard).
- Multiple holes in one operation.
- Hole on a curved face (require planar face only for v1; reject curved faces with a clear message).
- Pattern of holes (use Circular/Linear Pattern tasks for that).

## Files likely involved
- `Engine/CadModel.cs` — add `HoleSelectedBody` to `CadCommandActionKind`; add `HoleParams` record (FaceId, CenterX, CenterY, Diameter, DepthKind, DepthValue).
- `Engine/CadProjectStore.cs` — `HandleHoleSelectedBody()`: validate face is planar, build cylinder primitive, subtract from host body; wire into `Apply()` switch.
- `AvaloniaApp/Dialogs/HoleFeatureDialog.axaml(.cs)` — new dialog; contains face label, X/Y numeric fields, diameter stepper, depth type radio, depth numeric.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — `HoleCommand` ICommand; CanExecute when a body is in the scene (face selected even better).
- `AvaloniaApp/MainWindow.axaml(.cs)` — "Hole" button in 3D features group; tooltip "Hole (H)"; bind `H` shortcut.
- `AvaloniaApp/Controls/WebViewportHost.cs` — preview cylinder rendered on face while dialog is open.

## Expected behavior (acceptance)
1. "Hole" button appears in 3D features group; active when ≥ 1 solid body is present.
2. Click Hole → dialog opens; face field shows "Select a face" if none pre-selected.
3. User clicks a flat face in viewport → face name/ID fills in, preview cylinder appears at face centroid.
4. Adjust X/Y offset → preview moves on the face.
5. Set Diameter=10, Depth=Through All → OK → cylinder void visible in body; feature tree shows "Hole (⌀10 × Through)".
6. Set Depth=Blind, Depth Value=15 → OK → blind bore stops at 15 mm depth.
7. Curved face click → rejected with status bar message "Hole requires a planar face."
8. Diameter ≤ 0 → rejected (field outline goes red, OK disabled).
9. Double-click tree node → dialog reopens with current params; changing diameter updates the hole.
10. Cancel → scene unchanged.
11. Build: 0 errors, 0 warnings.

## Notes / hints
- "Through All" depth: compute by ray-casting from the center in the face-normal direction to find the furthest intersection with the body — or simply use a very large cylinder (e.g. 10 000 mm) clipped by the Subtract; the latter is simpler for v1.
- If `IGeometryBackend` has `Subtract(body, tool)`, call it with the cylinder tool. If not, add the stub and log a clear HANDOFF note.
- Face ID: when the user clicks in the viewport, the `hit-test` message should return a face identifier. Check `WebViewportHost` hit-test handling — if only body-level picking exists, the dialog can fall back to "first face of selected body" and log that face-level picking is a follow-up.
- Reference: `References/onshape/blocks/04_viewport.jpg` — feature toolbar; `References/onshape/blocks/03_feature_tree.jpg` — feature tree naming style.
- Shortcut `H` for Hole is standard in CAD; bind it only in 3D mode (not sketch mode).

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] "Hole" button active when a body exists.
- [ ] Dialog opens; face selection works (click viewport or select from dropdown).
- [ ] Through-All hole: bore goes fully through body (or visibly deep stub if geometry deferred).
- [ ] Blind hole: bore stops at given depth.
- [ ] Curved face rejected with clear message.
- [ ] Diameter ≤ 0 rejected; OK stays disabled.
- [ ] Feature tree label shows diameter and depth type.
- [ ] Edit via double-click: changes apply.
- [ ] Cancel: no leftover geometry.
- [ ] H shortcut opens dialog in 3D mode only.
- [ ] One commit.

## Complexity
Moderate. Dialog is straightforward. Main complexity is face-level hit-testing (may need a stub) and the Subtract CSG operation. Sonnet is fine; geometry backend issues may require Opus-level debugging if CSG is not yet integrated.
