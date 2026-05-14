# Current State — 2026-05-14

## What works (code-verified)

- All 53 previously queued tasks (01–63) are marked complete in TASK_QUEUE.md.
- **CSG Hole geometry** — `CadProjectStore.cs` lines 4872–4897: real cylinder-subtract
  wired; ThroughAll uses 10000mm height; Blind uses `DepthValue`; offset applied via
  TransformedSolid. The HANDOFF stub is replaced.
- **Boolean Subtract + Intersect** — `SolidMesher.cs` lines 17–20 and 98–115:
  Möller–Trumbore ray-cast + centroid filter; not a stub any longer.
- All sketch tools (line, rect, circle, arc, polygon, slot, spline, point, mirror,
  trim, offset, 2D fillet, construction lines).
- Sketch constraints round 1 + round 2 (coincident, horiz/vert, equal, tangent,
  parallel, perpendicular, concentric, fix).
- Feature dialogs exist for every 3D operation.
- Feature tree: icons, rename, edit-sketch, delete, right-click context menu.
- Undo/Redo, Save/Open, Export (STL/OBJ).
- View cube, measurement tool, section view, smooth camera snap.
- Body visibility toggle, datum plane creation.

## What is unfinished / unverified

- **Build status unknown** — dotnet is not available in the planning environment.
  Last recorded clean build was task 33 (2026-05-01). Tasks 34–63 have no build
  evidence logged to DONE.md; they are marked done in TASK_QUEUE.md only.
- **Sketch preview rendering** — nested sub-agent queue (`AgentOps/AgentOps/`)
  lists three open issues: "Fix Point rendering", "Fix Line preview",
  "Fix Rectangle preview". These are not tracked in the outer queue and have no
  evidence of fix or verification.
- **DONE.md gap** — formal entries stop at task 33; tasks 34–63 never received a
  DONE.md entry with build evidence.

## What remains in backlog (outer queue)

| # | Task | Priority tier |
|---|------|---------------|
| 65 | CAD command dialog standardization | polish |
| 66 | Sketch solver and definition behavior | **sketch foundation** |
| 67 | Shell consistency pass | polish |
| 68 | Copilot structured CAD assistance | polish |

## Key file sizes (proxy for scope)
- `Engine/CadProjectStore.cs` — 5 222 lines
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — 3 298 lines
- `AvaloniaApp/MainWindow.axaml` — 1 391 lines
- `AvaloniaApp/Controls/WebViewportHost.cs` — 3 700+ lines
