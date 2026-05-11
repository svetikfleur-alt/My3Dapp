# 49. Box-select (rubber-band selection) in 3D mode

## Goal
Task 12 explicitly defers: _"Box / lasso select (later)."_ Task 11 also lists it as out-of-scope. With the single-click selection model in place (task 12), the natural follow-up is rubber-band multi-select: drag a rectangle over geometry to select all enclosed/touching bodies. This is standard in every CAD tool and essential once a model has more than a handful of bodies.

## Scope
- In 3D mode only (not in Sketch mode), holding left-button and dragging (no body under cursor at drag-start) activates box-select mode.
- A dashed-outline rectangle is drawn as a DOM overlay on the viewport canvas while dragging.
- On mouse-up: all body centroids inside the rectangle (screen-space) are added to the selection set.
- Two modes (match Onshape/Fusion convention):
  - **Left-to-right drag** ("window select"): selects only bodies fully enclosed by the rectangle.
  - **Right-to-left drag** ("crossing select"): selects any body whose bounding box intersects the rectangle.
- Shift + drag appends to the existing selection; plain drag replaces it.
- Esc during drag cancels the box without changing selection.
- Selected bodies highlight (same accent outline as single-click selection in task 12).
- Selection count shown in status bar.

## Out of scope
- Lasso / freehand select (future).
- Box-select in Sketch mode (sketch entities are small; the dot-click model suffices for now).
- Box-select on faces/edges (body-level only in v1).
- Assembly-context selection.

## Files likely involved
- Viewport JS — detect drag-start on empty space; draw CSS-overlay rectangle; on mouse-up, compute screen-space AABB for each body and test against box.
- `AvaloniaApp/Controls/WebViewportHost.cs` — receive `box-select-result` message (array of body ids) from JS; merge into selection set.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — extend `SelectionSet` to accept array of ids; notify status bar and feature tree.
- `AvaloniaApp/MainWindow.axaml` — no structural changes expected; selection highlight reuses task 12's styles.

## Expected behavior (acceptance)
1. In 3D mode, left-drag starting on empty space draws a visible dashed rectangle.
2. Release → bodies whose centroids fall inside are selected (window select / left-to-right).
3. Right-to-left drag → bodies whose screen AABB intersects the box are selected (crossing select).
4. Shift + drag adds newly boxed bodies to existing selection.
5. Plain drag replaces selection.
6. Esc while dragging cancels — no selection change.
7. Status bar shows correct selected body count.
8. Drag starting on a body centroid (hit test positive) → normal orbit, not box-select.
9. Build: 0 errors, 0 warnings.

## Notes / hints
- Distinguish drag-on-empty from drag-on-body by checking raycaster at `mousedown` position before starting the box. If raycaster hits a body, let OrbitControls handle it as an orbit drag.
- Overlay rectangle: absolutely-positioned `<div>` with `pointer-events: none; border: 1px dashed …; position: absolute` over the canvas. Create on `mousedown`, resize on `mousemove`, remove on `mouseup` or `keydown(Escape)`.
- For body screen AABB: project the 8 corners of each body's world bounding box through the camera's projection matrix. Use Three.js `Box3` + `Vector3.project(camera)`.
- Window vs crossing: check if drag went left-to-right (endX > startX) or right-to-left (endX < startX).
- Follow MERGE_PROTOCOL.md for any shared-file edits.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Left-drag on empty space shows dashed rectangle.
- [ ] Left-to-right release selects enclosed bodies only (window select).
- [ ] Right-to-left release selects intersecting bodies (crossing select).
- [ ] Shift + drag appends; plain drag replaces.
- [ ] Esc during drag cancels cleanly.
- [ ] Drag starting on a body triggers orbit, not box-select.
- [ ] Status bar count updates correctly.
- [ ] No regression on single-click selection (task 12 behaviour intact).

## Complexity
Low-medium. All logic is JS-side; the C# changes are limited to receiving and merging an array of selected ids. The tricky part is the body-screen-AABB projection, but Three.js provides the needed primitives.
