# 39. Angle dimension between two lines

## Goal
Add an Angle dimension tool to the sketch dimension toolset, following up on task 13's explicit deferral ("Angle dimension — next round"). Users pick two non-parallel lines and get a dimension arc displaying the angle between them; editing the value rotates one line to satisfy the constraint.

## Scope
- **Angle dimension tool** button in sketch toolbar (next to existing Dimension button from task 13).
- **Pick flow**: click line A → click line B → dimension arc placed at intersection.
- **Dimension display**: arc between the two lines labeled with angle in degrees (e.g., "45°"); leader lines or just the arc + text.
- **Quadrant selection**: dimension placed on the side of the intersection that was clicked (the smaller or larger angle depending on click position).
- **Edit**: clicking the angle label opens `NumericFeatureDialog` for new value; engine rotates line B about the intersection point to satisfy.
- **Simple solver**: rigid rotation of the shorter/un-anchored line; full constraint propagation deferred.
- Persist with the sketch (round-trip through `Save`/`Open` when task 08 is done).

## Out of scope
- Angle between line and reference plane.
- Angular constraint (auto-solve driving other geometry) — this task only covers the displayed dimension + single-line rotation.
- Driven (read-only) mode.
- Angle in radians mode.

## Files likely involved
- `Engine/CadModel.cs` — new `CadSketchAngleDimension` entity type (alongside existing dimension types from task 13).
- `Engine/CadProjectStore.cs` — new `HandlePlaceAngleDimension` handler; two-click state machine similar to linear dimension.
- `AvaloniaApp/MainWindow.axaml(.cs)` — Angle Dimension toolbar button.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — `IsAngleDimensionToolActive` bool.
- `AvaloniaApp/Controls/WebViewportHost.cs` — send `draw-angle-dimension` message to JS with center, line angles, value.
- Viewport JS — render arc + label; hit-test for click-to-edit.
- `AvaloniaApp/Dialogs/NumericFeatureDialog.axaml.cs` — reuse for value entry (already exists).

## Expected behavior (acceptance)
1. Angle Dimension button visible in sketch toolbar.
2. Click two non-parallel lines → arc dimension appears at intersection labeled with correct angle.
3. Clicking the arc label → NumericFeatureDialog opens; entering a new value rotates line B to satisfy.
4. Parallel lines: tool shows informative message ("Lines are parallel — angle is 0° or 180°; use linear dimension").
5. Dimension arc visible in both Light and Dark themes.
6. Build: 0 errors, 0 warnings.

## Notes / hints
- Angle between two lines: `θ = atan2(|d1 × d2|, d1 · d2)` in 2D.
- For the arc display: draw a small circular arc at the intersection point between the two direction vectors; radius = ~15% of shorter line length, clamped to min/max.
- Task 13 must be done (or at least the ToolDialogWindow scaffold) before this task since it reuses the same patterns.
- Dependency: ideally task 13 is complete, but can stub out solver and just render the dimension.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Angle dimension placeable on two crossing lines.
- [ ] Displayed angle matches geometric angle (verify with a known 45° and 90° case).
- [ ] Edit value → line rotates; dimension updates.
- [ ] Parallel lines: friendly message, no crash.
- [ ] No regression in linear/radial dimensions (task 13).

## Complexity
Medium — geometry is straightforward; two-click state and arc rendering are the main work.
