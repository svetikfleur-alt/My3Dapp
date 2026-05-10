# 31. Extrude Join / Cut / Symmetric modes

## Goal
Task 01 (Extrude full operator) was closed as done, but AUDIT.md (2026-04-25) still records: "Extrude dialog only offers 'New Body' — Join, Cut, Symmetric not exposed (enum has them)." `CadExtrudeOperation` already has `NewBody`, `Join`, and `Cut`; the dialog spec mentioned all four modes but only `NewBody` was wired. Finish the job: expose all operation modes in the Extrude dialog and make them functional.

## Scope
- **Extrude dialog** (`ExtrudeFeatureDialog.axaml`) currently shows only a distance field. Add an Operation mode selector — segmented control or radio group: **New Body** / **Join** / **Cut** / **Symmetric**.
- **Symmetric mode**: extrudes half-distance in each direction from the sketch plane (so total travel = entered value; sketch plane stays centered). Add `Symmetric` to `CadExtrudeOperation` enum if not yet present.
- **Join mode**: merges the extruded volume with an existing body. Requires the scene to contain ≥ 1 existing body; if not, Join option is greyed out with tooltip "No bodies to join".
- **Cut mode**: subtracts the extruded volume from an existing body. Same guard as Join. If the scene has exactly one body, pre-select it; if multiple, show a "Target body" dropdown.
- **New Body** (default): existing behavior — always enabled.
- Live viewport preview updates when the mode changes (same semi-transparent preview, just showing result shape difference where feasible; Join/Cut preview can show as additive/subtractive tint).
- Committed feature tree node label reflects mode: "Extrude (Join)", "Extrude (Cut ← Box)", "Extrude (Symmetric)", "Extrude".

## Out of scope
- Boolean operations between already-existing bodies (that is task 26).
- Multi-profile Extrude (multiple closed loops selecting different operations).
- Asymmetric Symmetric (different distances per side).
- Draft angle.

## Files likely involved
- `Engine/CadModel.cs` — add `Symmetric` to `CadExtrudeOperation` enum (if absent); update `CadCommandAction` payload to include `ExtrudeOperation` field if it only carries `Amount`.
- `Engine/CadProjectStore.cs` — `HandleExtrudeSelectedSketch()`: branch on `action.ExtrudeOperation`; implement Join (add to existing body), Cut (subtract), Symmetric (split extrusion around plane).
- `AvaloniaApp/Dialogs/ExtrudeFeatureDialog.axaml(.cs)` — add Operation mode control; bind selected mode to action payload; add Target Body dropdown (visible/enabled only for Join/Cut with >1 body).
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — `AvailableBodies` collection for the dropdown; update Extrude CanExecute.
- `AvaloniaApp/Controls/WebViewportHost.cs` — extend preview message to carry operation mode so JS can tint additive/subtractive differently.

## Expected behavior (acceptance)
1. Extrude dialog shows 4 mode options: New Body, Join, Cut, Symmetric.
2. Scene empty + Join selected → Join option greyed out (tooltip shown); OK disabled.
3. New Body (default): existing behavior unchanged.
4. Symmetric, distance=20 → body extends 10 mm above and 10 mm below sketch plane.
5. Join: extruded volume merges with the target body; no separate new body appears in tree; feature node reads "Extrude (Join ← <BodyName>)".
6. Cut: extruded volume is removed from target body; feature node reads "Extrude (Cut ← <BodyName>)".
7. Multiple bodies present + Cut selected → target body dropdown appears and is required.
8. Preview updates in viewport when mode radio changes (at minimum: no crash; ideally tinted preview).
9. Build: 0 errors, 0 warnings.

## Notes / hints
- `CadExtrudeOperation.Symmetric` may not exist yet — check and add to enum + switch in `CadProjectStore`.
- For Join/Cut geometry: if `IGeometryBackend` has `Union(a, b)` / `Subtract(a, b)`, call those; if not, stub with a TODO comment and a status-bar message "Join/Cut geometry not yet supported by backend" — but still persist the intent in the feature tree.
- The target body for Join/Cut: if exactly one body exists, auto-select it silently. If zero bodies → disable. If >1 → show dropdown. Pre-fill from current selection.
- `CadCommandAction` likely carries parameters as named fields (inspect the record/class definition near `CadCommandActionKind`). Add `ExtrudeOperation ExtrudeOp` and `Guid? TargetBodyId` fields if not present.
- Keep the distance field and direction (Normal/Reverse) from the original dialog; just add the mode radio group above or below it.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Dialog shows 4 mode options.
- [ ] New Body: existing behavior (baseline test — create a box via extrude).
- [ ] Symmetric: body centered on sketch plane.
- [ ] Join: one body in scene + extrude → single merged body, no extra tree node.
- [ ] Cut: extrude removes material from target body.
- [ ] Join/Cut with no existing body: option greyed out.
- [ ] Multiple bodies + Cut: target dropdown shown and required.
- [ ] Feature tree node label reflects operation mode.
- [ ] One commit.

## Complexity
Moderate. Dialog extension is straightforward; the engine branching (Join/Cut geometry) is the risk. Backend stub is acceptable for v1 if CSG is not ready. Sonnet is appropriate.
