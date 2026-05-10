# 35. 2D sketch transform tool (move / rotate / scale on sketch entities)

## Goal
A 2D analog of task 24's 3D Move tool: a discrete top-panel button that, when active, lets the user move (and ideally rotate / scale) selected sketch entities. NOT an always-on gizmo on every selection — invoked from the top panel as a function, only shows handles while active.

## Scope
- Top panel button under sketch tools (icon: arrow-cross or move glyph), active only when a sketch session is open.
- When activated:
  - User selects sketch entities (or has them pre-selected) → translation handles appear on the selection's bounding box / pivot.
  - Drag → translates the selected entities along the dragged axis (or freely if center handle).
  - Optional: rotate handle on the corner; scale handles on edges/corners.
  - Esc cancels in-progress drag and exits the tool.
  - Enter commits and exits the tool.
- When inactive: NO handles on sketch entities — just the standard selection highlight.
- Persist transformed entity positions in CadProjectStore so they roundtrip through Save/Open.

## Out of scope
- Numeric transform dialog (could be a follow-up).
- World-space vs sketch-plane-space toggles.
- Snap to other entities while dragging — handled by the grid/snap task separately.

## Files likely involved
- `AvaloniaApp/MainWindow.axaml(.cs)` — top panel button + activation.
- `AvaloniaApp/Controls/WebViewportHost.cs` — handle rendering and drag input in sketch mode.
- `Engine/` — sketch entity types and the transform application.
- Sketch tool registry / enum.

## Expected behavior (acceptance)
1. Top panel has a "Transform" (or "Move 2D") button, visible only in sketch mode.
2. By default no transform handles on sketch entities — just selection highlight per task 12.
3. Activate Transform tool: select entity → handles appear on bounding box.
4. Drag the move handle: entities translate. Release commits.
5. Esc: cancels in-progress drag, exits tool.
6. Enter: commits, exits tool.
7. Selecting another tool deactivates Transform and hides handles.
8. Theme parity Light/Dark for handles.
9. Build returns 0 errors.

## Notes / hints
- Sibling pattern to task 24 (3D Move tool — gizmo only when active). Reuse the activation/state pattern if 24 is already implemented.
- Reference Onshape and Fusion sketch transform UX: handles appear on bounding-box corners/edges + center for free move.
- Don't break Line / Rectangle / other sketch tools; Transform is mutually exclusive with creation tools.

## Complexity
Moderate. UI + input + transform math. Sonnet should handle.

## Verifier checklist
- Build returns 0 errors, 0 warnings.
- Open app, enter sketch mode. Place a few entities. NO handles on selection by default.
- Click Transform tool: handles appear. Drag move handle, entities translate. Esc exits cleanly.
- Switch to Line tool: Transform deactivates, handles disappear.
- Theme switch Light/Dark — handles visible in both.
- One commit with the change.
