# 21. Viewport navigation (orbit / pan / zoom / walk / fit)

## Goal
Make the viewport feel like a real CAD viewport: orbit, pan, zoom, fit-to-view, and a working "Walk" mode (or whatever the current navigation mode is called in this codebase) that actually moves the camera. User reports walk navigation is currently broken. Other modes may be partial.

## Scope
- Audit current viewport input handling. Identify which navigation modes exist, which work, which are stubs.
- Implement / repair the standard CAD navigation set:
  1. **Orbit** — middle-mouse drag (or Shift+left-drag fallback). Rotates camera around scene center.
  2. **Pan** — middle-mouse-with-modifier (or Ctrl+middle-drag). Translates camera and target together along the screen plane.
  3. **Zoom** — mouse wheel zooms toward cursor; pinch-to-zoom on touchpad if supported by Avalonia + WebView2.
  4. **Fit / Zoom-to-fit** — keyboard shortcut (e.g. F or Home). Frames the current scene contents in the viewport.
  5. **Walk** — first-person-like navigation (WASD + mouse-look, or whatever the existing "Walk" mode was supposed to be). Currently broken. Either repair it or simplify it to a working subset and update the UI label.
- Update viewport feedback: cursor changes (open-hand for pan, rotate icon for orbit, etc.).
- Consistent across viewport states: should work whether sketch mode is active or not, except disable navigation that would conflict with active sketch interaction.

## Out of scope
- New camera projections (orthographic toggle, perspective tweaks) — separate task.
- Saved viewpoints / named views.
- Animated camera transitions.
- Touch/pen gestures beyond what Avalonia gives natively.

## Files likely involved
- `AvaloniaApp/Controls/WebViewportHost.cs` (1975 lines) — main viewport host, likely contains the input handlers.
- `AvaloniaApp/Controls/*Viewport*` — any sibling files for viewport input or rendering.
- `Engine/` — camera state if it lives there.
- Whatever JavaScript / HTML the WebView2 hosts for the rendering surface — input may be handled on the JS side and posted to .NET; trace both ends.

## Expected behavior (acceptance)
1. Orbit, Pan, Zoom, Fit work via standard mouse / keyboard inputs and produce visible camera changes in the viewport.
2. Walk mode either works correctly (camera moves on WASD + drag-look) or is disabled/relabeled with a comment in code explaining why deferred.
3. Cursor icon changes appropriately for the active navigation gesture.
4. No navigation gesture causes the app to hang, throw, or render artifacts.
5. Sketch mode disables conflicting gestures (e.g. left-click is sketching, not orbiting) but keeps wheel-zoom and pan available.
6. A short note added to `Blueprint/UI/05` (or a new `Blueprint/UI/05a-viewport-navigation.md`) describing the canonical bindings, so future tasks reference one source of truth.

## Notes / hints
- Reference: `References/onshape/blocks/04_viewport.jpg` — Onshape's viewport navigation cues.
- Blueprint principles: viewport is a real rendering area, not decorative — these gestures must dispatch to the actual camera state, not just visual hints.
- If the WebView2-side rendering has its own camera math, the cleanest path may be JS-side input → postMessage to .NET → Avalonia camera state update → re-render. Don't dual-source camera state.

## Verifier checklist
- Build returns 0 errors.
- Run app, enter 3D mode. Middle-drag should orbit; wheel should zoom. Make sure both work in both Light and Dark themes.
- Press F (or whatever fit shortcut you wired) — viewport should reframe.
- Try Walk mode if implemented; confirm it doesn't crash and produces camera movement.
- Switch to sketch mode. Confirm orbit is suppressed (clicking is sketching) but wheel-zoom still works.
- One commit with the change.
