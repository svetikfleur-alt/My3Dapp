# 27. Sketch grid and snap markers

## Goal
Add a visible grid and snap markers to the sketch mode viewport. Task 02 (sketch preview/commit) explicitly deferred this: "Snap markers — separate task." Currently there is no grid overlay, no endpoint/midpoint/intersection snap, and no inferred-constraint hints while drawing. These are table-stakes for a usable CAD sketch environment.

## Scope
- **Grid overlay** in sketch mode: a faint uniform grid drawn on the active sketch plane, scaled to the current unit (mm). Spacing auto-scales with zoom so it never becomes pixel noise. Grid can be toggled via a button in the sketch toolbar or `G` shortcut; state persists per-session.
- **Grid snap**: when the pointer is within a snap radius of a grid intersection, cursor snaps to it. Visual indicator: crosshair glyph at the snap point.
- **Geometric snap markers** (shown while a sketch tool is active):
  - **Endpoint** snap — hollow square at line/arc endpoints.
  - **Midpoint** snap — small triangle at segment midpoint.
  - **Center** snap — cross glyph at circle/arc center.
  - **Intersection** snap — × glyph where two entities cross.
  - **On-entity** snap — dash glyph: cursor is constrained to lie on the hovered segment.
- Snap priority order: Endpoint > Center > Midpoint > Intersection > On-entity > Grid.
- **Snap radius**: configurable constant (default 8 px screen-space); larger makes snap sticky, smaller makes it loose.
- Snap is always on while a sketch tool is active; grid snap can be toggled off independently.
- No inferred constraint application in this task — just visual cues. (Constraint application belongs in task 07.)

## Out of scope
- Inferred-constraint application (parallel/perpendicular/tangent hints that auto-apply — task 07).
- Polar tracking / angle snap.
- Snap to reference planes in 3D mode.
- Settings UI for snap radius (hardcode default; settings page is future).

## Files likely involved
- `AvaloniaApp/Controls/WebViewportHost.cs` — add JS for grid draw loop and snap-hit-test in `pointermove` handler; pass snap point back to C# for cursor position readout.
- `AvaloniaApp/Controls/SoftwareViewportControl.cs` — parallel grid + snap rendering for the software fallback path.
- `Engine/CadProjectStore.cs` — `HandleUpdateSketchPreview` may need to receive a "snap point" field from the action so the committed entity lands at the snapped position.
- `Engine/CadModel.cs` — `CadCommandAction` may need a `SnappedPosition` payload field.
- `AvaloniaApp/MainWindow.axaml(.cs)` — grid toggle button in sketch toolbar; `G` shortcut binding.
- `AvaloniaApp/Themes/Studio.Light.axaml`, `Studio.Dark.axaml` — grid color tokens (very low opacity; dark slightly lighter, light slightly darker than background).

## Expected behavior (acceptance)
1. Enter sketch mode → grid appears on the sketch plane. Grid lines are faint (≤15% opacity) and do not obscure geometry.
2. Zoom in: grid auto-subdivides to maintain ~40–80 px between lines; zoom out: grid coarsens.
3. Press `G` (or toggle button) → grid hidden; press again → grid returns.
4. Hover near a line endpoint while Line tool active → hollow-square snap marker appears; released click commits at exact endpoint.
5. Hover near segment midpoint → triangle snap marker.
6. Hover near circle center → cross snap marker.
7. Two overlapping entities: hover near intersection → × snap marker.
8. Snap point is sent to the store so committed entity exactly shares the snapped coordinate (no floating-point drift visible at zoom).
9. Software fallback viewport shows grid and snap markers at parity with WebView2 path (may be simpler rendering but functional).
10. Build: 0 errors, 0 warnings.

## Notes / hints
- Grid rendering in JS: draw to a Canvas overlay or use Three.js `GridHelper` limited to the sketch plane. Keep draw calls minimal — draw only visible lines, not the whole infinite grid.
- Snap hit-test is pure math: for each entity in the current sketch, compute projected screen coords of endpoints/midpoints/centers, find minimum distance to pointer, snap if < radius.
- The `UpdateSketchPreview` action already carries pointer position — add a `SnappedX`/`SnappedY` field (nullable double) so the store can prefer it over raw pointer when non-null.
- Reference: `References/onshape/blocks/05_sketch_mode.jpg` — shows Onshape snap markers style.
- Keep snap marker rendering in JS; pass the snapped world position back via the existing C#→JS message bridge.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Sketch mode shows grid; 3D mode does not.
- [ ] Grid auto-scales with zoom (no pixel-level noise at any zoom level).
- [ ] `G` toggles grid visibility.
- [ ] Endpoint snap fires within 8 px screen-space of a line endpoint; marker shown.
- [ ] Midpoint snap fires at segment center; marker shown.
- [ ] Center snap fires at circle center; marker shown.
- [ ] Intersection snap fires where two entities cross; marker shown.
- [ ] Committed entity coordinate matches snap point exactly (verify via coordinate readout).
- [ ] Software fallback shows grid (may be simplified).
- [ ] One commit.

## Complexity
Moderate. Mostly JS/viewport work plus minor C# action-payload extension. Grid math is straightforward; snap hit-testing is O(n·entities) and fast enough. Sonnet is fine.

## User addendum (2026-04-27, revised): grid appears where you draw

User clarified: the grid is NOT a cursor-tracker. It is a **region-based backdrop that appears wherever the user is actively drawing**.

Requirements layered on top of the base spec:
- Grid rendering is **region-based**, not full-viewport, not cursor-tracking.
- The grid covers a **rectangular region defined by the active sketch's bounding box plus a small margin** (e.g. +100 px screen-space).
- Region shape: rectangular/square, axis-aligned to the sketch plane.
- **No sketch entities yet, sketch mode active**: grid appears as a small square centered on the sketch plane origin (or on the user's first click point if available).
- **As the user draws more entities**: the grid region expands to keep covering all of them with margin.
- **Idle (sketch mode not active)**: grid is hidden, or only a very faint origin marker remains.
- The grid is a **static backdrop for the sketch region**, NOT a cursor-following overlay. It updates only when sketch entities change or on zoom.
- Edges of the grid region fade softly to transparent (no hard rectangle cut-off).
- Theme-aware colors: faint in Light, slightly more visible in Dark; always subordinate to actual geometry.

This replaces the "Grid overlay" item in Scope above. Keep all the snap-marker behavior unchanged — those remain global/per-entity and only show when a sketch tool is active.
