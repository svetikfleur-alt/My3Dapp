# Audit  — 2026-04-25

## What works
- Avalonia desktop shell: top toolbar, left feature tree, viewport, right inspector, bottom Code/Logs tabs.
- Light/Dark theme toggle.
- WebView2 + Three.js viewport with software fallback; OrbitControls + TransformControls wired.
- 16 primitive shapes (Box, Sphere, Cylinder, Cone, Torus, Pyramid, Wedge, Prism, Capsule, Hemisphere, Ellipsoid, Arrow, Icosphere, Tetrahedron, Octahedron, Icosahedron).
- Sketch mode separation from 3D mode; mode-aware toolbar.
- Sketch tools wired: Line, Rectangle, Circle, Arc, Point.
- Plane selection dialog (Top/Front/Right) before sketch.
- Extrude, Revolve, Fillet, Shell, Mirror features.
- Reference planes auto-created (Top/Front/Right).
- Feature tree with name/type filter.
- Sketch session card with Constraints + Dimensions side panels.
- Sketch preview on hover (pointermove → sketch-preview message).
- Right-click viewport context menu (Delete, Focus Camera).
- Assistant panel: Provider/Model selectors, Auto/Do/Think/Assist/Think&Do mode chips, pending-command Apply button.
- Cursor position readout while sketching.

## What's missing or broken (by category)

### Tools (sketch entities)
- No Polyline (multi-segment connected lines).
- No Polygon (regular n-gon).
- No Slot (common CAD primitive).
- No Spline / curve through points.
- No Construction line / centerline toggle.
- No Trim or Extend.
- No Offset entity.
- No Sketch-corner Fillet.

### Viewport / 3D
- No View Cube or standard-view shortcuts (Top/Front/Right/Iso).
- No origin axis triad / compass widget.
- No Zoom-to-Fit / Focus-All command.
- No display-mode toggle (shaded / wireframe / hidden line).
- No hover pre-select highlight on bodies/edges.
- No box-select or Ctrl-click multi-select.
- No section / cross-section view.

### Features (modeling ops)
- Extrude dialog only offers "New Body" — Join, Cut, Symmetric not exposed (enum has them).
- No Boolean ops between existing bodies (Union/Subtract/Intersect).
- No Chamfer (only Fillet exists).
- No Hole feature.
- No Linear or Circular Pattern.
- No Sweep, no Loft (out of MVP but worth flagging downstream).

### Sketch workflow
- No grid / snap toggle.
- No snap markers (endpoint, midpoint, intersection, perpendicular foot).
- No inferred-constraint hints during draw.
- No type-to-dimension while creating.
- No right-click context menu on sketch entities (delete/edit).
- Editing an existing sketch from the feature tree path unclear.

### Constraints & dimensions
- Only Coincident / Horizontal / Vertical / EqualRadius exist.
- Missing: Tangent, Parallel, Perpendicular, Concentric, Fix, Equal-length, Symmetric.
- No Distance dimension between two points/lines.
- No Angle dimension between two lines.
- No driving vs driven distinction.
- Constraint glyphs not clickable / not highlighted on selection.

### Assistant integration
- Action-log / event-context model from Blueprint not visibly wired into prompt context.
- Mode chips (Auto/Do/Think/Assist) appear cosmetic — unclear if behavior changes.
- Output is freeform messages, not structured Do-mode action proposals.
- Selection / active-tool / recent-actions context may be minimal.

### Polish / feedback
- No Undo / Redo — store has no history stack.
- No Save / Open project — CadProjectStore lacks persistence methods.
- No keyboard shortcuts (Esc, L, R, C, A, P, S, E, Ctrl+Z, Ctrl+S).
- No bottom status bar (mode, units, cursor, selection count).
- No "unsaved changes" indicator.
- No tooltips with shortcut hints.

## Out of scope (Blueprint defers)
- Organic sculpting (Blueprint 02: explicitly out).
- Assemblies / mate workflows (Blueprint 02: out for now).
- Full simulation / CAM / slicer (modes exist in enum but Blueprint defers).
- Cloud / collaboration / SaaS shell (Blueprint 05: forbidden patterns).
- Chat-bubble assistant style (Blueprint 05: forbidden).

## Top 5 to fix first
1. Add Undo/Redo (Ctrl+Z, Ctrl+Y)
2. Add Save/Open project (file persistence)
3. Add keyboard shortcuts (Esc, L, R, C, A, P, S, E)
4. Add bottom status bar (mode, units, cursor, selection)
5. Add Extrude Join/Cut and Symmetric options
