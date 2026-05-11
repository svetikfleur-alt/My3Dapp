# 24. Move/Translate as a discrete tool (not always-on gizmo)

## Goal
Change manipulation UX: the move/translate gizmo (the colored arrows on the model) currently appears constantly on the model. User wants this to be a tool that's invoked when needed — pressed from the toolbar or via shortcut — and not a permanent overlay on geometry.

## Scope
- Find where the move/translate gizmo (the three colored arrow handles) is currently rendered.
- Make it conditional on an active "Move" tool / mode rather than the default selection state.
- Add a Move tool button to the top panel (Features section, or wherever transform tools belong) and a keyboard shortcut (e.g. `M` or `G` for grab — pick whichever fits the existing convention).
- When Move tool is active: gizmo appears on selection, drag along an axis translates, Esc cancels, Enter commits.
- When Move tool is inactive: NO gizmo arrows on the model. Selection still highlights the geometry per task 12, but no transform handles.
- Same pattern should be reusable for future Rotate / Scale tools.

## Out of scope
- Implementing Rotate or Scale tools (separate tasks; this one only restructures the discoverability).
- Numeric input dialog for precise translation (could be follow-up).
- World-space vs local-space toggle (later).

## Files likely involved
- `AvaloniaApp/Controls/WebViewportHost.cs` — gizmo rendering and input handling.
- Whatever JS/HTML the WebView2 hosts for 3D rendering — gizmo is likely drawn there.
- `AvaloniaApp/MainWindow.axaml(.cs)` — top panel button.
- `Engine/` — transform application to selected entities.
- Tool / mode state — whatever holds active tool selection.

## Expected behavior (acceptance)
1. By default, no transform gizmo is drawn on selected geometry.
2. Activating the Move tool (button click or shortcut) shows the gizmo on the current selection.
3. Dragging an axis arrow translates the selection along that axis. Releasing commits.
4. Esc while Move tool is active cancels the in-progress drag and exits the tool.
5. Selecting a different tool deactivates Move and hides the gizmo.
6. The change is theme-consistent (gizmo arrow colors aligned with the Light/Dark palette).
7. Build returns 0 errors.

## Notes / hints
- Reference: Onshape's manipulator workflow — gizmo only shows when you've picked a Move/Transform action, not always-on.
- Blueprint principle: dense, technical UI. Always-on overlays are visually noisy and clash with engineer-tool feel.
- Don't break existing selection — selection highlight (task 12) stays; only the gizmo overlay is conditional.

## Verifier checklist
- Build returns 0 errors.
- Open app, select a feature: NO arrows on the model.
- Activate Move tool: arrows appear on selection.
- Drag an axis: selection translates.
- Esc: drag cancelled, tool exits, arrows disappear.
- Pick another tool (e.g. select): arrows disappear, default selection state restored.
- Switch theme Light/Dark: gizmo colors look correct in both.
- One commit with the change.
