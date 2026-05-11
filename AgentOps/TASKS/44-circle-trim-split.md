# 44. Circle trim — split full circle at intersection points

## Goal
Task 37 closed with circle trim explicitly stubbed: `"Circle trim is not yet supported — convert to arc first."` A full circle has no natural start/end angles, so trimming it requires finding the two nearest intersection-angle parameters and splitting the circle into one or more arcs that exclude the clicked segment. This task removes the stub and makes circle trim functional for the common case (circle crossed by at least one other entity).

## Scope
- **Circle trim**: when the clicked entity is a `CadSketchCircle`, find all intersection angles from other entities (lines, arcs, other circles).
- If fewer than 2 intersection angles found: friendly status-bar message ("Circle needs at least 2 intersections to trim — add a crossing entity first.") — no crash, no silent failure.
- If ≥ 2 angles found: identify which arc segment contains the clicked point (by angular position of `hit.T` angle), remove that arc segment, keep the rest as one or more `CadSketchArc` entities that replace the original circle.
- The circle is removed from `session.DraftEntities`; the replacement arcs are inserted.
- Circle-Line, Circle-Circle, Circle-Arc intersection cases all handled (reuse `CollectCircleIntersectionAngles` helper or extend `CreatePlacedTrim`'s existing intersection pipeline).
- No regression to the existing Line and Arc trim paths.

## Out of scope
- Trim of construction circles (defer to future construction-entity refactor).
- Splitting a circle into more than two arcs in a single trim click — one trim click removes one segment; multi-segment removal requires multiple clicks.
- UI for selecting trim mode (split vs extend).

## Files likely involved
- `Engine/CadProjectStore.cs` — replace the `case CadSketchCircle:` early-return stub (~line 1383). Add `CollectCircleIntersectionAngles(CadSketchCircle, List<CadSketchEntity>)` helper. Add `TrimCircle(CadSketchCircle, double clickAngle, List<double> intersectionAngles)` → `List<CadSketchEntity>` (returns arcs).
- `Engine/CadModel.cs` — no new model types needed; `CadSketchArc` already exists with `CenterX/Y`, `Radius`, `StartAngleDeg`, `EndAngleDeg`.

## Expected behavior (acceptance)
1. Draw a circle; draw a line crossing it at two points. Activate Trim. Click the arc between the two intersection points → that segment is removed; one or two arcs remain, circle entity gone.
2. Draw a circle crossed by two lines at 4 points. Trim click removes only the arc between the two nearest intersection angles bracketing the click.
3. Trim click on a circle with only 1 intersection point → status-bar message, no entity change.
4. Trim click on a circle with 0 intersections → status-bar message, no entity change.
5. Trim of line/arc entities is unaffected (no regression).
6. Build: 0 errors, 0 warnings.

## Notes / hints
- `hit.T` for a circle in `FindTrimHit` should be the angular parameter in degrees (0–360) of the clicked point — confirm this is set correctly when `FindTrimHit` processes a `CadSketchCircle`.
- `CollectCircleIntersectionAngles`: for each other entity compute intersection points with the full circle, then convert each point to angle `atan2(py - cy, px - cx)` normalized to [0, 360).
- `TrimCircle`: sort angles, find the pair straddling `clickAngle`, return `CadSketchArc` for the complement arcs.
- Circle-Line intersection: standard quadratic with the line parameterized; two t solutions give two points.
- Reuse the existing `CircleLineIntersection` / `CircleArcIntersection` helpers if they exist, or extract from `CollectLineIntersectionParams`.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Circle × 2 lines: trim removes only the clicked arc segment.
- [ ] Circle × 1 entity only: friendly message, no crash.
- [ ] Circle × 0 intersections: friendly message, no crash.
- [ ] Existing Line trim and Arc trim unchanged.
- [ ] No construction-line entity touched.

## Complexity
Medium. Geometry is well-understood; main effort is hooking into the existing trim dispatch and getting the angle-wrap arithmetic correct.
