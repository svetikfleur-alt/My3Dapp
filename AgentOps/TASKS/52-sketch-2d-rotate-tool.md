# 52. Sketch 2D rotate tool

## Goal
Task 35 added a 2D translate tool (drag to move sketch entities). The natural companion — rotate selected entities about a pivot — is absent. Every professional CAD sketch environment (Onshape, Fusion, SolidWorks) exposes both translate and rotate as first-class sketch operations. This task adds a rotate tool that mirrors the translate tool's UX pattern.

## Scope
- New "Rotate" toggle button in the sketch toolbar, adjacent to the existing "Transform" (translate) button.
- Activation flow:
  1. User selects one or more sketch entities (click or box-select in sketch mode once task 49 lands; single-click suffices for v1).
  2. Clicks "Rotate".
  3. A pivot point marker (small circle) is shown at the centroid of the selection. User can click-drag the pivot marker to reposition it.
  4. Dragging anywhere else rotates the selection about the pivot. Rotation angle computed from the angular delta between the initial drag vector and the current drag vector (both measured from pivot).
- Live preview: entities rotate in the JS viewport as the drag proceeds (same Group.rotation approach as translate's Group.position preview).
- On mouse-up: final rotation angle committed via `postMessage({ type: 'rotate-entities', ids: [...], pivotX, pivotY, angleDeg })`.
- C# handler `RotateSketchEntities(ids, pivotX, pivotY, angleDeg)` in `CadProjectStore.cs` applies the rotation to entity coordinates:
  - `Line`: rotate both `StartX/Y` and `EndX/Y` about pivot.
  - `Circle`: rotate `CenterX/Y` about pivot (radius unchanged).
  - `Arc`: rotate `CenterX/Y` about pivot; adjust `StartAngle` and `EndAngle` by `angleDeg`.
  - `Slot`: rotate both centre points about pivot.
  - `Polygon`: rotate `CenterX/Y` about pivot; adjust `RotationDeg` by `angleDeg`.
  - `Spline`: rotate each control point about pivot.
- Esc cancels an in-progress drag, restoring the preview to original position.
- Pressing Enter or clicking elsewhere (outside drag) commits and deactivates rotate mode.
- Snap: if snap is active (task 27), angle snaps to 5° increments while dragging; Shift held disables snap.

## Out of scope
- Scale / uniform resize tool (future).
- Rotating 3D bodies (that is the 3D Move tool's gizmo — task 24).
- Rotation with a typed exact angle (numeric input field) — future enhancement; type-to-value can be a follow-on.

## Files likely involved
- Viewport JS — `activateRotateTool(ids)`: render pivot circle; `mousedown` on pivot → drag pivot; `mousedown` elsewhere → start angle-drag; compute angleDeg from atan2 of drag vectors; update `Group.rotation.z` for live preview; `mouseup` → `postMessage` commit.
- `AvaloniaApp/Controls/WebViewportHost.cs` — receive `rotate-entities` message; call `CadProjectStore.RotateSketchEntities`.
- `Engine/CadProjectStore.cs` — `RotateSketchEntities(ids, pivotX, pivotY, angleDeg)`: rotate each entity type as described above.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — `IsRotateToolActive` bool; `ActivateRotateTool` / `DeactivateRotateTool` commands (mirror `IsTransformToolActive` from task 35).
- `AvaloniaApp/MainWindow.axaml` — "Rotate" toggle button in sketch toolbar (next to Transform); bind to `IsRotateToolActive`.
- `AvaloniaApp/Themes/Studio.Dark.axaml`, `Studio.Light.axaml` — ensure rotate button has a style (can reuse Transform button style).

## Expected behavior (acceptance)
1. In sketch mode, select a line; click Rotate → pivot marker appears at line midpoint; cursor changes to rotate cursor.
2. Drag away from pivot → line rotates live in the viewport.
3. Release mouse → rotation committed; line is in new orientation in the model.
4. Esc during drag → line snaps back to original position; rotate mode stays active.
5. Click Rotate button again → deactivates.
6. Rotate a circle → centre moves correctly; radius unchanged.
7. Rotate an arc → centre moves; start/end angles adjust.
8. Rotate a Polygon → centre moves; RotationDeg adjusts.
9. Pivot can be repositioned by dragging the pivot marker before dragging to rotate.
10. Build: 0 errors, 0 warnings.

## Notes / hints
- Angle computation: `angle = atan2(currentVec.y, currentVec.x) − atan2(startVec.y, startVec.x)` where both vectors are measured from pivot. Accumulate if dragging continuously.
- Pivot marker: small `CircleGeometry(4px)` or DOM overlay circle centred on the pivot screen position.
- Follow MERGE_PROTOCOL.md for MainWindow.axaml edits — additive only, marker comment around the Rotate button addition.
- Mirror task 35 (`TranslateSketchEntities`) as closely as possible — the C# side is nearly identical with angle math replacing delta-position math.
- For snap: if `SnapActive` (task 27): `angleDeg = Math.Round(angleDeg / 5.0) * 5.0`.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Rotate button present in sketch toolbar.
- [ ] Select + Rotate: live preview rotates about pivot.
- [ ] Mouse-up commits; geometry correct in C# model.
- [ ] Esc cancels without committing.
- [ ] Circle centre moves; radius unchanged.
- [ ] Arc centre + angles both adjusted correctly.
- [ ] Polygon centre + RotationDeg adjusted.
- [ ] Pivot reposition works.
- [ ] No regression on translate tool (task 35).

## Complexity
Low-medium. JS angle-drag logic is the new piece; C# handler is a straightforward rotation of 2D points. Mirrors task 35 closely.
