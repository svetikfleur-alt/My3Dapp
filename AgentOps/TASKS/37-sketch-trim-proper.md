# 37. Sketch Trim — proper implementation

## Goal
Replace the stub `CreatePlacedTrim` in `CadProjectStore.cs` (line ~1229) which currently returns `null` with a working Trim tool that removes the segment of a sketch entity between two intersection points when the user clicks on it.

## Scope
- **Intersection detection**: compute all intersections between committed sketch entities (line-line, line-circle, line-arc, circle-arc, arc-arc pairings — at minimum line-line and line-circle).
- **Segment identification**: given a click point, determine which entity segment the user clicked and which pair of intersections bounds it.
- **Segment removal**: split the clicked entity at its bounding intersections and delete the clicked segment; keep the other segments as new entities.
- **Entity types**: support Line (split into 0–2 sub-lines) and Arc (split into 0–2 sub-arcs). Circle split into two arcs is a stretch goal; stub with a message if too complex.
- Remove the current stub that returns `null` and the "not yet fully implemented" message from the visible UX.

## Out of scope
- Extend tool (opposite of Trim).
- Trim across splines or polygons — stub with a message.
- Auto-closing open profiles after trim.

## Files likely involved
- `Engine/CadProjectStore.cs` — replace `CreatePlacedTrim`; add intersection helpers.
- `Engine/CadModel.cs` — possibly new `CadSketchSegment` sub-entity or reuse existing types.
- `AvaloniaApp/Controls/WebViewportHost.cs` — click hit-testing already routes to `PlaceSketchEntity`; no extra wiring needed if trim commits via that path.

## Expected behavior (acceptance)
1. Activate Trim tool; click a line segment between two intersecting lines → clicked segment removed; other segments remain.
2. Clicking a line with no nearby intersection produces a friendly message ("No intersection found near click").
3. The "not yet fully implemented" stub message is gone from the happy path.
4. Line-line trim works. Line-circle trim works.
5. Build: 0 errors, 0 warnings.

## Notes / hints
- Hit-testing: use a point-to-segment distance threshold (e.g., 5 sketch units or proportional to zoom level stored in session).
- Line-segment split: given line A→B intersected at parameter t1 < t2, keep A→P(t1) and P(t2)→B; remove the middle.
- Line-circle intersection: quadratic formula; two solutions give two arc-boundary parameters.
- Prioritize correctness for the common case (two crossing lines) over generality.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Two crossing lines: clicking one arm removes only that arm.
- [ ] Line intersecting circle: clicking the interior chord segment removes it.
- [ ] Click with no intersection shows informational message rather than silent failure.
- [ ] No regression in other sketch tools (Trim activation doesn't break Line/Rect/Circle).

## Complexity
Medium-hard — intersection math is mechanical; the tricky part is hit-testing and correct segment splitting for arcs.
