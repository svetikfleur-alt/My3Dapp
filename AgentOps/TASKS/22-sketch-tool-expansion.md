# 22. Sketch tool expansion

## Goal
Bring the sketch toolset closer to a real CAD baseline. User reports sketch needs to be "made proper and have more". Current set is roughly Line / Rectangle / Circle / Arc / Point. Add the missing essentials and tighten the proper-ness of what's already there.

## Scope
Add these sketch entities (each with preview-on-move + commit-on-second-click pattern, going through the new `ToolDialogWindow` scaffold where it makes sense):
- **Polygon (regular, n-sided)** — center + radius input, n parameter via dialog.
- **Slot** — straight slot (two centers + radius). Optional dialog for "centerline length" if simpler.
- **Spline (control-point)** — click points to add control points, double-click or Enter to commit, Esc cancels.
- **Mirror** — select sketch entities + mirror axis (sketch line) → produces mirrored copies.
- **Trim** — pick a curve segment between intersections, removes that segment.
- **Offset** — pick a curve, distance via dialog, produces parallel offset copy.
- **2D fillet** — pick two intersecting sketch curves + radius via dialog, replaces the corner with a tangent arc.

Also tighten existing tools as part of this task:
- All sketch tools route through the dialog scaffold (consistent with task 03 outcome) where options are present; tools without options still go inline.
- Cursor crosshair while a sketch tool is active.
- Esc cancels the active sketch tool and returns the cursor to default.
- Each new entity persists in `CadProjectStore` and renders correctly.

## Out of scope
- Parametric constraints (separate task 07/13).
- Dimensioning (separate task 13).
- 3D pattern (covered by feature-side tasks).

## Files likely involved
- `Engine/` — sketch entity types, geometry, persistence.
- `AvaloniaApp/Dialogs/` — new dialog option views per tool that needs options (Polygon-n, Offset-distance, Fillet-radius).
- `AvaloniaApp/MainWindow.axaml(.cs)` — top panel buttons / commands for new tools.
- `AvaloniaApp/Controls/WebViewportHost.cs` — input + preview rendering for new tools.
- Sketch tool registry / enum if it exists in `Engine/`.

## Expected behavior (acceptance)
1. Polygon, Slot, Spline, Mirror, Trim, Offset, 2D fillet are all selectable from the sketch toolbar and produce committed sketch entities.
2. Polygon dialog asks for n (sides count), default 6, valid range 3..50.
3. Offset dialog asks for distance (mm), accepts negative for inward.
4. 2D fillet dialog asks for radius, default 1.0 mm.
5. All new tools have correct preview-on-move + commit-on-click behavior.
6. Esc cancels any active sketch tool cleanly.
7. Sketch tree (in feature tree) shows new entity types with appropriate names.
8. Build returns 0 errors.

## Notes / hints
- Reference: `References/onshape/blocks/03_left_feature_panel.jpg` for the kind of clean tool list the sketch toolbar should look like.
- Blueprint principle: feature-based, parametric. Even sketch entities should have parametric handles (n for polygon, distance for offset, radius for fillet) so the assistant can later reason about and modify them.
- Don't try to implement perfect spline math — a simple Catmull-Rom or quadratic Bezier through control points is enough for v1.

## Verifier checklist
- Build returns 0 errors.
- Each new tool icon appears in sketch toolbar.
- Smoke-test each: enter sketch mode, click tool, complete the gesture, confirm entity appears in viewport and feature tree.
- Esc during any tool returns to default.
- Polygon with n=3 and n=12 both render correctly.
- Offset with positive and negative distance work.
- Fillet between two intersecting lines produces a tangent arc of the requested radius.
- One commit with the additions.
