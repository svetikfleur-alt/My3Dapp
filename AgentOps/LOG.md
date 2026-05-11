done: 30 — Export panel STL/OBJ (2026-04-28) — ExportDialog, OnExportClick, Ctrl+E, ExportFormat ViewModel, WorkspaceController export methods, status-bar confirm; build 0 errors 0 warnings; commit 1f97318
done: 29 — Hole feature (2026-04-27) — HoleFeature, HoleDepthKind, HoleSelectedBody; dialog (diameter/offset/depth); toolbar ⌀ button; H shortcut 3D-only; double-click edit; CSG stub with HANDOFF; 0 errors 0 warnings
done: 28 — Circular Pattern feature (2026-04-27) — CircularPatternDialog, enum+wiring, ViewModel method, toolbar CP button, LP click wired, feature tree label; 0 errors
done: 27 — Sketch grid and snap markers (2026-04-27) — canvas overlay, auto-scale grid, 5-type snap hit-test, G toggle, Avalonia button, SW fallback; 0 errors; commit deb8023
done: 26 — Boolean body operations (2026-04-27) — BooleanFeature, 3 action kinds, cross-body compile, invisible-body filter, BooleanBodyDialog, toolbar 3-button group; Union meshes; Subtract+Intersect stubbed; 0 errors; commit b714c4d
done: 39 — Angle dimension between two lines (2026-04-27) — two-click pick, atan2 angle, arc+label rendering, edit rotates line B, parallel guard; 0 errors; commit da2c808
done: 38 — Construction lines (2026-04-27) — IsConstruction flag, session mode toggle, ProfileBuilder filter, dashed JS render, toolbar button; 0 errors
done: 37 — Sketch Trim proper (2026-04-27) — intersection detection (L-L, L-Circ, L-Arc, Arc-Circ, Arc-Arc); hit-test ≤5 units; TrimLine/TrimArc; Circle stubbed; 0 errors
done: 36 — Sketch Mirror proper (2026-04-27) — two-click axis; Line/Circle/Arc reflection; preview axis+mirrored entities; Slot/Polygon/Spline skipped with message; 0 errors
done: 35 — 2D sketch transform tool (2026-04-27) — Transform toggle + JS drag translate + UV postMessage + C# entity mutation; commit 1e58706; 0 errors
done: 21 — Viewport navigation (2026-04-27) — orbit/pan/zoom/fit implemented; walk deferred; 5 pre-existing truncated files repaired as baseline
verified: Add sketch dialog window (and reusable dialog window pattern) -> partial
partial: Add sketch dialog window (and reusable dialog window pattern)
popped: Polish top panel / toolbar
executed: Polish top panel / toolbar
verified: Polish top panel / toolbar -> partial
partial: Polish top panel / toolbar
popped: Fix Extrude from closed profile
executed: Fix Extrude from closed profile
verified: Fix Extrude from closed profile -> partial
partial: Fix Extrude from closed profile
done: 25 — Assistant panel fix (2026-04-27) — key state + layout; 5 baseline truncations re-fixed (consolidated with 21)
done: 22 — Sketch tool expansion (2026-04-27) — rendering for Polygon/Slot/Spline added; dialog base-class ambiguity fixed; missing XAML handlers added; build green (commit e960a6c)
done: 23 — Additional 3D operators (2026-04-27) — Revolve/3D Fillet/Chamfer/Shell/Linear Pattern wired
done: 24 — Move tool (2026-04-27) — toggle gizmo on/off via M key + ToggleButton; Esc cancels, Enter commits
accepted: post-merge CS0104 fix on new sketch dialogs
done: 27 — Sketch grid and snap markers (2026-04-27) — region-based grid, snap markers (endpoint/midpoint/center/grid), G key + toggle button, software fallback grid; build green (commit 01624dd)
