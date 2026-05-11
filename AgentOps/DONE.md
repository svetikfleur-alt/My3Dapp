# Done

This is a strict completion ledger. Entries here mean the task is marked complete in
TASK_QUEUE.md and has at least build or commit evidence. Partial or unverified work
belongs in LOG.md or HANDOFF.md, not here.

- 20 - Fix Avalonia/WinForms namespace clash (verified) - queue marked [x]; current build passes with 0 warnings and 0 errors; no active WinForms namespace build clash remains.
- 21 - Viewport navigation (2026-04-27) - orbit, pan, zoom, fit-to-view, sketch-mode rotate lock, cursor feedback; merged in commit d15df2a; build reported clean.
- 22 - Sketch tool expansion (2026-04-27) - polygon, slot, spline, mirror, trim, offset, 2D fillet wiring and render support; commits 0519c68/e960a6c and merge 5b36fb9; build reported clean.
- 23 - Additional 3D operators (2026-04-27) - revolve, 3D fillet, chamfer, shell, linear pattern wiring; commits ef7487a/c839bcd; build reported clean.
- 24 - Move tool (2026-04-27) - move toggle, M shortcut, Esc cancel, Enter commit; commits a5fc947/3c3311d; build reported clean.
- 25 - Assistant panel fix (2026-04-27) - provider/key state, compact layout, structured message rows; commits f7079c1/0f7ef59; build reported clean.
- 26 - Boolean body operations (2026-04-27) - union/subtract/intersect feature plumbing, dialog, toolbar, body visibility handling; commit b714c4d; build reported clean.
- 27 - Sketch grid and snap markers (2026-04-27) - sketch grid overlay, snap markers, G toggle, software fallback rendering; commits deb8023/01624dd; build reported clean.
- 28 - Circular Pattern feature (2026-04-27) - dialog, enum/action wiring, ViewModel method, toolbar button, feature summary; commits 9cb78d0/4fbeebe; build reported clean.
- 29 - Hole feature (2026-04-27) - hole model/action/dialog/tree wiring and 3D shortcut; commit 9e4f197; build reported clean.
- 30 - Export panel STL/OBJ (2026-04-28) - export dialog, toolbar command, Ctrl+E, ViewModel/export methods, status confirmation; commit 1f97318; tracking commit 83c753b.
- 35 - 2D sketch transform tool (2026-04-27) - transform toggle, JS drag preview, UV delta commit, entity mutation; commit 1e58706; build reported clean.
- 36 - Sketch Mirror proper implementation (2026-04-27) - two-click mirror axis, line/circle/arc reflection, preview axis; commit e58bef1; build reported clean.
- 37 - Sketch Trim proper implementation (2026-04-27) - line/arc/circle intersection detection and segment split trimming; commit 07d58ee; build reported clean.
- 38 - Construction lines / centerline toggle (2026-04-27) - construction flag, session toggle, profile filtering, dashed render; commit 3bc4af0; build reported clean.
- 39 - Angle dimension between two lines (2026-04-27) - two-line pick flow, angle calculation, viewport arc/label, edit rotation guard; commit da2c808; build reported clean.

- 31 - Extrude Join / Cut / Symmetric modes (2026-05-01) - 4-mode Operation ComboBox in dialog, target body dropdown for Join/Cut, engine branching with BooleanSolid.Union for Join / diagnostic stub for Cut, Symmetric flag passed to ExtrudeSolid, feature tree labels; build 0 errors 0 warnings.

- 32 - Sketch constraints round 2 (2026-05-01) - Added Tangent/Parallel/Perpendicular/Concentric enum values, engine handlers with v1 geometry adjustment, 4 SVG icons, 4 toolbar buttons, 4 click handlers; build 0 errors 0 warnings.

- 33 - Edit sketch from feature tree (2026-05-01) - EditSketch action/handler, edit-session path in HandleFinishSketch, dependent body suppression in viewport, EditSketchAsync on ViewModel, double-click dispatch, right-click context menu with Edit Sketch + Delete, "editing…" secondary text indicator; build 0 errors 0 warnings.

Queue correction:
- Task 01 was previously marked done without matching completion evidence. It is now pending again in TASK_QUEUE.md and is intentionally not listed as done here.
