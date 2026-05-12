# Current State — 2026-05-12

## Build
Last verified: 0 errors, 0 warnings (as of 2026-05-11 cloud sync).
`dotnet` CLI not available in this planner environment; build must be confirmed by Builder.

## What works (verified by code inspection)

### Core modeling
- All 16 primitives: Box, Sphere, Cylinder, Cone, Torus, Pyramid, Wedge, Prism, Capsule,
  Hemisphere, Ellipsoid, Arrow, Icosphere, Tetrahedron, Octahedron, Icosahedron.
- Extrude: New Body, Join, Cut, Symmetric modes, taper angle, shell thickness.
- Revolve, Sweep, Loft, 3D Mirror, Shell, Fillet, Chamfer.
- Boolean Union/Subtract/Intersect: CSG is actually implemented in SolidMesher.cs
  (CsgSubtract + CsgIntersect using Möller–Trumbore ray-cast inside/outside classification).
- Hole feature: CSG subtract wired in CadProjectStore.cs (line 4873-4888) via CylinderSolid +
  BooleanSolid.Subtract. Task file 53-hole-csg-geometry.md describes work that is DONE.
- Linear pattern, Circular pattern.
- Body visibility toggle (V key + eye icon).
- Body color/appearance.
- Section view (SectionViewToggle, axis + offset wired).
- Measurement tool (MeasureToolToggle wired).

### Sketch
- Tools: Line, Rectangle, Circle, Arc, Point, Polygon, Slot, Spline, Offset, Fillet 2D.
- Mirror (two-click axis), Trim (intersection detection), Transform (translate), Rotate 2D.
- Construction lines (dashed, profile-filtered).
- Grid overlay + snap markers (endpoint, midpoint, center, grid), G toggle.
- Constraints: Coincident, Horizontal, Vertical, EqualRadius,
  Tangent, Parallel, Perpendicular, Concentric.
- DOF tracker + constrained status indicator.
- Sketch on existing face.
- Angle dimension, Distance dimension, Driving vs Driven dimensions.
- Edit sketch from feature tree (double-click / right-click → Edit Sketch).
- Sketch entity right-click context menu.
- Inference hints / snap labels.

### Viewport / shell
- WebView2 + Three.js with software fallback.
- Orbit, pan, zoom, fit-to-view, camera snap animation.
- View cube widget.
- Box-select (rubber-band) in 3D mode (BoxSelectCompleted event wired).
- Datum plane creation (DatumPlaneDialog wired).
- Export panel: STL / OBJ (ExportDialog + Ctrl+E).
- Feature tree: icons, filter, rename (F2), keyboard nav, context menu.
- Undo / Redo, Save / Open project.
- Status bar, keyboard shortcuts.
- Light / Dark theme.
- Assistant panel: provider/model selectors, structured context injection.

## What is unfinished or unverified

### Unqueued task specs (files exist but not in queue)
- `TASKS/53-hole-csg-geometry.md` — work already done in code; no queue entry needed.
- `TASKS/54-boolean-csg-subtract-intersect.md` — work already done in code; no queue entry needed.
- `TASKS/55-polyline-sketch-tool.md` — NOT implemented; no PolylineMode / PolylineVertices in
  ViewModel or Engine. Added to backlog as task 69.

### Known runtime limitations (not bugs)
- Boolean Subtract/Intersect CSG is centroid-based triangle filtering (no triangle splitting);
  works for convex overlapping bodies; non-convex seams may show artifacts.
- Hole CSG uses Through-All depth of 10000 units (adequate but not bounding-box relative).
- Sketch solver constraints are applied heuristically; complex multi-constraint rigs may drift.

## Remaining backlog (all unstarted)
- [ ] 65 — CAD command dialog standardization         → TASKS/65-cad-command-dialog-standardization.md
- [ ] 66 — Sketch solver and definition behavior       → TASKS/66-sketch-solver-definition-behavior.md
- [ ] 67 — Shell consistency pass (toolbar + nav)      → TASKS/67-shell-consistency-toolbar-viewport.md
- [ ] 68 — Copilot structured CAD assistance           → TASKS/68-copilot-structured-cad-assistance.md
- [ ] 69 — Polyline sketch tool                        → TASKS/55-polyline-sketch-tool.md

## Priority assessment
1. Task 66 (Sketch solver) — core sketch foundation, highest priority.
2. Task 65 (Dialog standardization) — professional polish, second.
3. Task 67 (Shell consistency) — visual coherence, third.
4. Task 68 (Copilot) — assistive quality, fourth.
5. Task 69 (Polyline) — sketch foundation, can be tackled after 66.
