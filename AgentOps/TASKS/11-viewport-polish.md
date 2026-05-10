# 11. Viewport polish

## Goal
Bring the viewport to baseline CAD usability: View Cube / standard view buttons, axis triad, hover pre-select highlight, click-to-select on bodies/edges/faces, Zoom-to-Fit.

## Scope
- View Cube widget OR a row of 6 standard-view buttons (Top / Bottom / Front / Back / Left / Right) plus Iso.
- Axis triad / compass widget at viewport corner.
- Hover pre-select: hovering geometry shows accent-tinted outline before click.
- Click-to-select: single-click selects body/face/edge (whichever is closest); selection visible in feature tree and status bar.
- Zoom-to-Fit (F key) frames all visible geometry.
- Display-mode toggle (Shaded / Wireframe) — at least these two.

## Out of scope
- Box-select / Ctrl-click multi-select (later).
- Section view (later).
- Hidden line render mode.

## Files likely involved
- `AvaloniaApp/Controls/WebViewportHost.cs` — message wiring for view ops, hover/select hooks.
- `AvaloniaApp/Controls/SoftwareViewportControl.cs` — ensure parity.
- `AvaloniaApp/MainWindow.axaml(.cs)` — view controls UI.
- Viewport JS (under WebView2 resources) — pre-select highlight, view-cube widget; if not present, add minimal HTML overlay.

## Expected behavior (acceptance)
1. View Cube or view buttons present at viewport corner; clicking changes camera.
2. Axis triad visible at lower-left of viewport.
3. Hovering a body shows outline highlight; click selects it.
4. Selection visible in feature tree (matching node highlighted) and status bar (count > 0).
5. F (or Zoom-to-Fit button) frames all geometry within margin.
6. Display-mode toggle switches between Shaded and Wireframe.

## Notes / hints
- Three.js OrbitControls + edge geometry are already present per AUDIT — extend rather than reintroduce.
- Keep overlays as DOM elements over the canvas; do not block raycasts on the canvas.

## Verifier checklist
- [ ] Build runner ok.
- [ ] View Cube / view buttons reachable; Iso button works.
- [ ] Axis triad visible.
- [ ] Hover highlight on a Box body.
- [ ] Click selects body; tree highlights matching node.
- [ ] F frames all bodies.
- [ ] Wireframe toggle renders edges only.
