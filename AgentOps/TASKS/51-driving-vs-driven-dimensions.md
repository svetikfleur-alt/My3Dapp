# 51. Driving vs driven sketch dimensions

## Goal
AUDIT.md flags: _"No driving vs driven distinction."_ Currently all sketch dimensions are implicitly driving (they constrain geometry). Once task 32 (constraints round 2) lands and task 47 (DOF tracker) is in, the system will know when a sketch is fully-constrained. Adding a redundant dimension to a fully-constrained sketch should not silently overwrite geometry — it should create a **driven** (reference) dimension that shows the measured value as a read-only annotation without changing the geometry. This matches Onshape ("Reference dimension") and SolidWorks ("Driven dimension") behaviour.

## Scope
- A `IsDriven` boolean is added to `SketchDimension` (linear, radial, angle) in `CadModel.cs`.
- When the user places a dimension and DOF ≤ 0 (sketch is fully constrained per task 47): the newly placed dimension is automatically marked `IsDriven = true` and the geometry does NOT change.
- If DOF > 0: dimension is `IsDriven = false` (driving), geometry moves to satisfy the value.
- The user can also manually toggle a dimension between driving and driven by right-clicking the dimension label in the viewport and choosing "Make Driving" / "Make Reference".
- Visual distinction:
  - Driven dimensions are rendered with parentheses around the value: `(25.0 mm)`.
  - Driven dimensions use a muted/grey colour in the viewport label (vs. the accent colour for driving dimensions).
- Editing a driven dimension (double-click) shows its current measured value as read-only; it cannot be changed unless switched to driving mode (which re-evaluates DOF and may overconstrain).
- A warning toast/snackbar: "Sketch is fully constrained — dimension added as reference."

## Out of scope
- Automatic solver that moves geometry to satisfy conflicting driving dimensions (geometric constraint solver — future major feature).
- Per-dimension suppression or rollback.
- Exporting driven dimensions as drawing annotations (future).

## Files likely involved
- `Engine/CadModel.cs` — add `IsDriven` to `SketchLinearDimension`, `SketchRadialDimension`, `SketchAngleDimension`.
- `Engine/CadProjectStore.cs` — in dimension-placement handlers (`HandleAddLinearDimension`, `HandleAddRadialDimension`, `HandleAddAngleDimension`): call `ComputeSketchDof` (from task 47); if DOF ≤ 0 after adding, mark dimension as driven; else apply geometry update as before.
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — update `BuildLinearDimensions`, `BuildRadialDimensions`, `BuildAngleDimensions` to pass `isDriven` flag in the JSON emitted to the viewport JS.
- Viewport JS — render driven dimensions with parentheses and grey colour; skip geometry-update message for driven dims. Right-click on dim label → context menu "Make Driving" / "Make Reference".
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — surface "added as reference" notification; wire right-click toggle action.
- `AvaloniaApp/Themes/Studio.Dark.axaml`, `Studio.Light.axaml` — ensure driven-dim label colour tokens exist (can be simple grey overrides on the existing label style).

## Expected behavior (acceptance)
1. Open a blank sketch; place a line; add a horizontal dimension (50 mm) → geometry adjusts to 50 mm. DOF was > 0 → driving.
2. Fully constrain the sketch (e.g., fix + horizontal + vertical + two distances). DOF = 0.
3. Add another distance dimension → geometry does NOT change. Viewport shows `(50.0 mm)` in grey. Toast: "Sketch is fully constrained — dimension added as reference."
4. Double-click the driven dimension label → value is read-only (grayed input or no edit at all).
5. Right-click driven dimension → "Make Driving" option. Clicking it: if DOF would stay ≥ 0 after removing one constraint, promote to driving; otherwise warn overconstrained.
6. Right-click a driving dimension → "Make Reference" option. Converts to driven immediately; DOF increases by 1.
7. Driven dimensions survive save/load round-trip (IsDriven persists in JSON).
8. Build: 0 errors, 0 warnings.

## Notes / hints
- `ComputeSketchDof` from task 47: call it *before* adding the new dimension. If the result is already 0, mark incoming dim as driven. If result > 0, proceed driving and subtract 1 DOF.
- Do NOT modify geometry when placing a driven dimension — the `SketchLinearDimension.Value` stores the target, but `CadProjectStore` must skip the geometry-move step when `IsDriven = true`.
- For measured value of driven dim: recompute from entity positions (distance between two points, etc.) rather than storing a potentially stale value.
- Parentheses convention: `(value unit)` — standard across SolidWorks and Onshape.
- This task depends on task 47 (`ComputeSketchDof`) being landed. If 47 is not yet done, note that dependency and implement the DOF check inline as a simple count (copy the logic from 47's spec) to unblock this task.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Driving dimension adjusts geometry and renders in accent colour without parentheses.
- [ ] Placing dim on a fully-constrained sketch → driven; no geometry change; parentheses; grey colour.
- [ ] Toast notification shown.
- [ ] Driven dim double-click → read-only.
- [ ] Right-click driving dim → "Make Reference" works.
- [ ] Right-click driven dim → "Make Driving" works (when DOF allows).
- [ ] IsDriven survives JSON round-trip.
- [ ] No regression on existing dimension placement (tasks 13 + 39).

## Complexity
Medium. The DOF check is a single call; the main work is threading `IsDriven` through the rendering pipeline and implementing the right-click toggle. No geometric solver changes.
