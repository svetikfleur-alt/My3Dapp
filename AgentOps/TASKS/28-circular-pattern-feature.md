# 28. Circular Pattern feature

## Goal
Add a Circular Pattern operator so the user can replicate a feature or body around a central axis. Task 23 adds Linear Pattern but leaves Circular Pattern out; AUDIT.md lists "No Linear or Circular Pattern" as a missing feature. This task lands after task 23 (or in parallel if Linear Pattern is already merged).

## Scope
- **Circular Pattern**: pick one or more features/bodies, an axis of rotation (coordinate axis X/Y/Z or a sketch line used as axis), instance count, and total angle (default 360°). Produces N copies evenly distributed around the axis.
- Lives under the same 3D features group as Linear Pattern (top panel).
- Opens a dialog via the `ToolDialogWindow` scaffold (following task 23's pattern exactly).
- Parameters:
  - Feature(s)/Body to pattern — dropdown or selection picker (pre-filled from current selection).
  - Axis — segmented X / Y / Z buttons, or "Sketch axis" option.
  - Count — numeric stepper, minimum 2, default 4.
  - Total Angle — numeric, default 360°; values < 360° produce a partial arc arrangement.
  - Preview — live viewport preview before commit.
- Committed feature appears in the feature tree as "Circular Pattern (N × <feature name>)".
- Feature is editable post-commit (double-click → dialog with current params).
- Roundtrips through Save/Open when task 08 lands (use `CadProjectStore` parameter persistence pattern from task 23).

## Out of scope
- Variable spacing (non-uniform) around the arc.
- Pattern along a freeform spline axis.
- Nested patterns (pattern of a pattern).
- Assembly-level patterning.

## Files likely involved
- `Engine/CadModel.cs` — add `CircularPatternSelectedBody` (or `CircularPatternFeature`) to `CadCommandActionKind`; add a `CircularPatternParams` record if needed.
- `Engine/CadProjectStore.cs` — `HandleCircularPatternFeature()` method; wire into `Apply()` switch.
- `AvaloniaApp/Dialogs/CircularPatternDialog.axaml(.cs)` — new dialog following `NumericFeatureDialog` or `RevolveFeatureDialog` as a template.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — command and CanExecute for Circular Pattern; enable when ≥ 1 body in scene.
- `AvaloniaApp/MainWindow.axaml(.cs)` — toolbar button in 3D features group.
- `AvaloniaApp/Controls/WebViewportHost.cs` — preview rendering for circular arrangement.
- Feature tree rendering — "Circular Pattern" node with instance count in label.

## Expected behavior (acceptance)
1. Button visible in top panel 3D group; active when ≥ 1 body in scene.
2. Click Circular Pattern → dialog opens; Body and Axis pre-filled from selection.
3. Count = 4, Angle = 360°, Axis = Y → 4 copies at 0°, 90°, 180°, 270° around Y.
4. Partial angle: Count = 3, Angle = 180° → 3 copies at 0°, 90°, 180°.
5. Count = 1 rejected (minimum 2) with a non-blocking message.
6. Total Angle = 0° rejected similarly.
7. Preview in viewport updates live as count/angle/axis change.
8. OK commits; feature tree shows "Circular Pattern (4 × Box)".
9. Double-click reopens dialog; changing count from 4 to 6, OK → 6 copies in viewport.
10. Cancel leaves scene unchanged.
11. Build: 0 errors, 0 warnings.

## Notes / hints
- The rotation transform per instance is simply `i * (TotalAngle / Count)` degrees around the chosen axis, applied to the source body's mesh/feature.
- For the geometry, if the backend supports a `Rotate(mesh, axis, angle)` primitive, call it N times. If not, use a matrix rotation on the vertex positions (acceptable for v1, mark as "no parametric kernel" in HANDOFF if so).
- Mirror the dialog structure from `RevolveFeatureDialog.axaml` — axis choice + numeric angle is the same pattern; reuse the control style.
- Reference: `References/onshape/blocks/04_viewport.jpg` — feature toolbar layout in Onshape.
- **Dependency**: task 23 adds `LinearPattern` to `CadCommandActionKind`. Coordinate with that numbering — don't reuse the same enum value. If task 23 hasn't landed yet, add the enum entry alongside whatever 23 adds.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Button appears in toolbar; disabled when scene is empty.
- [ ] Count=4 / Angle=360 / Axis=Y: 4 copies evenly around Y axis.
- [ ] Count=3 / Angle=180: 3 copies in a 180° arc.
- [ ] Count=1: rejected with a message.
- [ ] Preview updates live in dialog.
- [ ] Feature tree shows label with count and source feature name.
- [ ] Double-click to edit: count/angle change applies correctly.
- [ ] Cancel: no orphan nodes.
- [ ] One commit.

## Complexity
Moderate. Closely mirrors LinearPattern (task 23). Dialog and ViewModel work are straightforward; geometry rotation transform is the main challenge. Sonnet is suitable.
