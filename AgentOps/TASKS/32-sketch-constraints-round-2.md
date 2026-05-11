# 32. Sketch constraints — round 2 (Tangent / Parallel / Perpendicular / Concentric / Fix)

## Goal
Task 07 landed basic sketch constraints (Coincident, Horizontal, Vertical, Equal) and explicitly deferred: "Tangent, Parallel, Perpendicular, Concentric, Symmetric — next round." This task is that next round. These five constraints are table-stakes for any parametric sketch workflow — Tangent alone enables arc-to-line smooth joins that are used in virtually every real part.

## Scope
Add these five constraint types to the existing constraint infrastructure:

- **Tangent** — a line and an arc (or two arcs) are tangent at their shared point. Applied when user selects a line endpoint coincident with an arc endpoint (or two arc endpoints at the same point) and clicks Tangent. Glyph: small "T" near the tangent point.
- **Parallel** — two lines have the same direction vector. Applied to two selected line entities. Glyph: two parallel bars "∥" near each line.
- **Perpendicular** — two lines are at 90°. Applied to two selected line entities. Glyph: small right-angle symbol near intersection.
- **Concentric** — two circles (or arcs) share the same center. Applied to two selected circles/arcs. Glyph: concentric-circle icon near shared center.
- **Fix** — locks an entity (point, line, arc, circle) at its current position/size — it becomes immovable by the constraint solver. Applied to one selected entity. Glyph: padlock icon near entity.

For each:
- Add the value to `CadSketchConstraintKind` enum.
- Add handler logic in `CadProjectStore.HandleApplySketchConstraint()` — adjust geometry to satisfy the constraint on apply, then record it.
- Add a toggle button in the sketch session Constraints toolbar (following task 07's layout).
- Constraint glyph renders in viewport at the appropriate location.
- Constraint persists with the sketch on Finish and reloads correctly.
- Invalid application (e.g. Tangent applied to two lines with no shared point) shows a non-blocking status-bar error and does not apply.

## Out of scope
- Equal-length (line length equality) and Equal-angle — defer to a potential round 3.
- Symmetric constraint (requires a mirror line concept; defer until mirror tool from task 22 is stable).
- Auto-inferred constraints during draw (task 07's scope boundary; still deferred).
- Constraint solver / DOF tracking UI (overconstrained detection is a future task).

## Files likely involved
- `Engine/CadModel.cs` — add 5 values to `CadSketchConstraintKind` enum: `Tangent`, `Parallel`, `Perpendicular`, `Concentric`, `Fix`.
- `Engine/CadProjectStore.cs` — `HandleApplySketchConstraint()`: add branches for each new kind; implement the geometry adjustment (e.g. for Parallel: compute target direction, rotate shorter line to match; for Tangent: translate arc endpoint to line endpoint and set tangent direction).
- `AvaloniaApp/MainWindow.axaml(.cs)` — add 5 toggle buttons in sketch Constraints toolbar; bind to `ApplyConstraintCommand` passing the new kind.
- `AvaloniaApp/Controls/WebViewportHost.cs` — extend glyph rendering: add glyph types for each new constraint (simple text/SVG overlays near affected entities).
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — selection-aware enable/disable for each button (e.g. Tangent button enabled only when a line + arc are selected; Parallel enabled only when 2 lines selected).

## Expected behavior (acceptance)
1. Sketch session shows 9 constraint buttons total (4 from task 07 + 5 new).
2. **Tangent**: select a line and a tangent arc → click Tangent → arc end meets line and is tangent (no angle at junction); glyph "T" appears.
3. **Parallel**: select two lines → click Parallel → both lines have same angle; glyph "∥" appears on each.
4. **Perpendicular**: select two intersecting lines → click Perpendicular → lines form 90°; right-angle glyph appears.
5. **Concentric**: select two circles → click Concentric → circles share center; glyph appears near center.
6. **Fix**: select a circle → click Fix → circle position and radius locked; moving the sketch does not affect it; padlock glyph appears.
7. Invalid Tangent (two lines, no shared point) → status bar: "Tangent requires a line and an arc sharing a point." No constraint applied.
8. Constraint persists after Finish Sketch and re-entry into the sketch.
9. Constraint can be deleted (same mechanism as task 07 constraints).
10. Build: 0 errors, 0 warnings.

## Notes / hints
- Constraint application math: keep it simple for v1 — `Parallel` just snaps the shorter line's angle to match the longer; `Perpendicular` adds 90°; `Tangent` positions the arc so its tangent at the shared point equals the line direction; `Concentric` sets both centers to their midpoint; `Fix` just records the entity's current values as immutable.
- For Fix: the constraint solver (if any) should skip this entity when resolving. If there's no solver yet (likely), Fix is just a flag stored in the constraint record — it becomes meaningful when a solver is added. Still wire the UI and the record.
- Glyph rendering in JS: simple overlaid SVG or Text2D at the midpoint/shared point of the selected entities. Follow the existing task-07 glyph pattern exactly.
- Dependency: task 07 must be landed first (or in the same branch). Check that `CadSketchConstraintKind` enum and `HandleApplySketchConstraint` method exist before starting.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] All 5 buttons visible in sketch toolbar.
- [ ] Tangent: arc meets line smoothly; glyph shown.
- [ ] Parallel: two lines aligned; glyph shown.
- [ ] Perpendicular: 90° angle; glyph shown.
- [ ] Concentric: two circles share center; glyph shown.
- [ ] Fix: entity cannot be moved by other constraints; padlock glyph shown.
- [ ] Invalid Tangent → non-blocking status error; no crash; no constraint applied.
- [ ] Constraints persist across Finish/re-edit sketch cycle.
- [ ] Constraint deletion still works for all 9 constraint types.
- [ ] One commit.

## Complexity
Moderate. Mostly enum extension + constraint application math (simple vector ops) + glyph rendering. The Fix constraint has no immediate runtime effect without a solver — document that clearly in HANDOFF. Sonnet is fine.
