# 69. Sketch preview rendering fixes — Point, Line, Rectangle

## Background
The inner Codex agent queue (AgentOps/AgentOps/TASK_QUEUE.md) identified three runtime rendering
gaps in sketch mode: Point entity rendering, Line preview rubber-band, and Rectangle preview.
These were not caught by build checks and require runtime observation plus targeted code fixes.

## Scope
Verify and fix the three sketch preview rendering paths:

### Point entity rendering
- `CadSketchPoint` entities placed in a sketch should appear as a filled dot in both the
  software fallback viewport and the WebView Three.js viewport.
- `BuildPointPoints` returns 3 doubles (1 coordinate). The JS renders this via
  `kind === 'point'` path (line 2068 in WebViewportHost.cs). Verify this path fires correctly
  and the sprite texture is visible at correct position/depth.
- Software viewport: `DrawSketchPoint` in SoftwareViewportControl.cs draws the dot. Verify it
  handles the scaled canvas coordinate correctly.

### Line preview rubber-band
- While Line tool is active and a first point has been placed (`PendingLineStart` set),
  moving the cursor should show a rubber-band line from the start point to cursor.
- `BuildLinePreview` already produces a `CadSketchPoint` (start) + `CadSketchLine` (rubber-band).
  Verify these end up in `session.PreviewEntities` and that `BuildRenderSketches` includes the
  preview sketch correctly when `IsPreview = true`.
- Check that the JS `rebuildSketches` call is triggered on every cursor move (not debounced
  to oblivion).

### Rectangle preview
- After the first corner is placed (`PendingShapeAnchor` set), moving the cursor should show
  the rectangle outline before the second click.
- `BuildRectanglePreview` calls `CreatePlacedRectangleLines` with a temp session — verify the
  returned `CadSketchLine[]` entities are correctly routed into `PreviewEntities` and rendered.
- Note: for rectangles the preview lines are individual `CadSketchLine` objects, not a
  `CadSketchRectangle`. Confirm the renderer handles this the same way as drafted lines.

## Acceptance
1. Place Line tool first point → cursor movement shows rubber-band line.
2. Place Rectangle first corner → cursor movement shows 4-sided preview rectangle.
3. Place a Point entity → dot visible in viewport after commit.
4. Build: 0 errors, 0 warnings.

## Files likely involved
- `Engine/CadProjectStore.cs` — `BuildPreviewEntities`, `BuildLinePreview`, `BuildRectanglePreview`
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — `BuildRenderSketches`, `BuildSketchCurves`
- `AvaloniaApp/Controls/WebViewportHost.cs` — JS `rebuildSketches`, preview throttle
- `AvaloniaApp/Controls/SoftwareViewportControl.cs` — `DrawSketchPoint`, preview rendering path

## Complexity
Low–medium — wiring/plumbing fixes; no new geometry or UI needed.

## Verifier checklist
- [ ] Line rubber-band visible on cursor move after first click.
- [ ] Rectangle ghost visible on cursor move after first corner.
- [ ] Point dot visible after commit.
- [ ] No regression in Circle / Arc / Polygon preview.
- [ ] Build clean.
