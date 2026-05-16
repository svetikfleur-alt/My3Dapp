# Current State — 2026-05-16

## Branch
`claude/clever-bell-86mc9` — based on cloud-sync snapshot 2026-05-11 (single commit: 2ff2b12).

## What works
- Full sketch mode: Line, Circle, Arc, Rectangle, Polygon, Slot, Spline, Fillet 2D, Offset, Mirror, Trim, Transform, Rotate, Construction lines.
- Sketch constraints: Coincident, Horizontal, Vertical, EqualRadius, Tangent, Parallel, Perpendicular, Concentric, Fix. DOF tracker + blue/dark coloring.
- Feature tree: create, delete, double-click edit, right-click context menu, F2 rename, eye toggle, keyboard nav.
- 3D modeling: Extrude (New Body / Join / Cut / Symmetric), Revolve, 3D Fillet, Chamfer, Shell, Linear Pattern, Circular Pattern, Hole (UI only), Loft, Sweep, 3D Mirror.
- Boolean ops: Union mesh works. Subtract and Intersect are stubbed (NotSupportedException silently caught).
- Datum planes: create custom reference planes, toggle, delete.
- Viewport: orbit/pan/zoom, box-select, section view, view cube, measurement tool, body color dialog, smooth snap animation.
- Export: STL / OBJ dialog (Ctrl+E).
- Save / Open project, Undo / Redo.
- Assistant panel: provider/model selectors, context injection, mode chips.

## What is stubbed / not working
1. **Hole geometry** — `HandleHoleSelectedBody` in `Engine/CadProjectStore.cs` has a `[HANDOFF]` CSG subtract stub. Holes appear in the feature tree but no material is removed from the body.
2. **Boolean Subtract / Intersect** — mesh-level CSG throws `NotSupportedException`; caught silently. Only Union produces real geometry.
3. **Sketch solver convergence** — solver runs 4 iterations (improved to 8 on remote branch 66 not yet merged). Multi-constraint chains may not fully resolve.

## What's on remote branches not yet merged
- Task 65 (dialog standardization) — commit 6d3ebf9 on `origin/claude/youthful-hawking-wrm4i`.
- Task 66 (sketch solver pass) — commit 7dfacd9 on `origin/claude/youthful-hawking-AsO6w`.
- Inspector fix (M key guard, Hole dialog default) — commit 7714f92 on `origin/claude/inspiring-fermi-7n92i`.

## Task file numbering issue (resolved)
Task files `53-hole-csg-geometry.md`, `54-boolean-csg-subtract-intersect.md`, `55-polyline-sketch-tool.md` conflicted with completed queue entries 53–55. Copies filed as **69, 70, 71** to avoid ambiguity. Originals kept for reference.

## Build status
`dotnet` not available in this container; last confirmed build: 0 errors 0 warnings (per commit 7714f92 on inspector branch, 2026-05-16).
