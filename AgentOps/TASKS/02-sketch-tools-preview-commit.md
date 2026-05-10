# 02. Sketch tools — preview + commit

## Goal
Make Line, Rectangle, and Circle draw with a consistent "click 1 -> live preview on move -> click 2 commits" feel, and give sketch points the same dense CAD look (small filled dots, hollow circles for endpoints / control points) seen in real CAD tools.

## Scope
- Line: previously fixed; verify chain-mode still works and start-marker is rendered immediately on first click.
- Rectangle: 4-line corner-to-corner draw with live rubber-band preview from click 1 to current pointer; commit on click 2.
- Circle: true conic circle (not polygon approximation); center-radius preview while dragging.
- Sketch point styling: replace any large/SaaS-looking markers with dense CAD glyphs: filled dot (1.5–2 px) for committed points, hollow circle for snap candidates, small cross for origin / construction.
- Consistent commit -> chain (Line) / single-shot (Rectangle, Circle) behavior; Esc cancels in-flight tool.

## Out of scope
- Polyline, Polygon, Slot, Arc-by-3-points (separate tools).
- Snap markers (separate task — sketch snap).
- Trim / Extend (separate task).

## Files likely involved
- `Engine/CadProjectStore.cs` — `BuildLinePreview`, `BuildRectanglePreview`, `BuildCirclePreview`, point-style helpers.
- `Engine/ProfileBuilder.cs` — Rectangle as 4 connected segments; Circle as conic.
- `AvaloniaApp/MainWindow.axaml.cs` — pointer move + click routing for active sketch tool.
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — viewport preview marshalling.
- `AvaloniaApp/Themes/Studio.Light.axaml`, `Studio.Dark.axaml` — sketch-point brushes.

## Expected behavior (acceptance)
1. Line: click 1 places origin marker; pointer move shows dashed rubber-band; click 2 commits and starts next segment in chain mode; Esc breaks chain.
2. Rectangle: click 1 anchors corner; pointer move renders 4-line rubber-band rectangle; click 2 commits all 4 segments as one closed profile.
3. Circle: click 1 anchors center; pointer move renders smooth circle preview; click 2 commits as a single conic arc, NOT a polygon.
4. Committed sketch points render as 1.5–2 px filled dots in the active sketch color; endpoints render as small hollow circles.
5. Origin reference renders as a small cross with axis tick.
6. Esc during any in-flight tool clears preview + pending start without committing.
7. Active tool indicator in toolbar reflects state (already present — verify still works).

## Notes / hints
- Line code path is the reference for pattern consistency.
- Keep entity construction in `Engine/`; UI must not directly construct geometry.
- Reference: `References/zoo/blocks/05_viewport.jpg` and `References/onshape/blocks/04_viewport.jpg` — note how points read at glance.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Line: click-move-click on Top plane creates segment + chain marker.
- [ ] Rectangle: 2 clicks produce 4 connected lines; profile reports closed.
- [ ] Circle: 2 clicks produce a true circle (zoom in — should stay smooth).
- [ ] Sketch points visually match CAD style (dense, small, not filled squares).
- [ ] Esc cancels each tool cleanly.
