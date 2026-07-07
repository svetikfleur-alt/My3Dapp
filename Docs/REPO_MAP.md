# REPO MAP (active source inventory)

Git root: `D:\My3DApp\My3DApp` · remote: `origin = github.com/svetikfleur-alt/My3Dapp`
Branch: `feature/cad-mvp-ui-truth-repair` · ~466 tracked files.
Backup: `D:\My3DApp\backup\My3DApp-allrefs-2026-07-07.bundle` (all refs).
Archive (non-active, outside repo): `D:\My3DApp\archive-2026-07-07\`.

## Active product source (compiles into My3DApp.exe, Avalonia mode)

| Path | Role |
|---|---|
| `Program.cs`, `My3DApp.csproj` | Entry point, single app project (net10.0-windows) |
| `AvaloniaApp/` | UI: MainWindow (4.2K loc), StudioShellViewModel (5.7K loc), Controls (WebViewportHost — legacy viewport), Services (workspace controller, assistant, ACL interpreter), Dialogs, Themes |
| `Engine/` | CadProjectStore (5.2K loc, state + .umxproj persistence — legacy), CadModel, CadProjectCompiler, mesh CSG pipeline (to be replaced by OCCT), kernel adapters |
| `Core/` | Solid records + mesh math (mesh-only, to be replaced) |
| `Backends/` | IGeometryBackend stub |
| `Export/` | STL/OBJ exporters (STL mechanics reusable as boundary export) |
| `Assets/` | Icons, app manifest, web assets (web assets legacy with WebView2) |

## Active non-compiled

- `tests/EngineTests/` — xUnit (mesh/export focused, thin)
- `Tools/AclSmokeRunner/`, `Tools/PicoGKBringup/` — console harnesses (excluded from app build)
- `Docs/` — this map, CAD_CANON, CURRENT_WORK_PACKET, INTEGRATION_STATUS + legacy arch docs

## Not active (kept in repo, excluded from build and graph)

- `Studio/` — archived WinForms UI (csproj-excluded in Avalonia mode; slated for removal)
- `AgentOps/` — agent automation scripts (109 files, not product code)
- `Blueprint/`, `blueprint deployer/`, `PartLibrary/`, `Server GPT/`, `examples/`,
  `demo-assets/`, `workflows/`, `References/`, `Samples/`, `src/` (TS experiments) —
  stale/legacy; no .cs compiled from any of them
- `ThirdParty/` — vendored upstream drops (git-ignored)

## Archived to D:\My3DApp\archive-2026-07-07\

`agent-worktrees/` (23 stale worktree copies; branches remain in git),
`temp_picogk_source`, `temp_build_assistant`, `temp_run_cmdsys`, `temp_sync_backup`,
`temp_verify_picogk`, `temp_video_review`, `temp_acl_branch.acl`, `temp_rx.cs`.

## Deleted (generated, restorable)

`bin/`, `obj/`, `dist/` (publish + WebView2 cache, ~48K files), `logs/`, `.venv/`.

## Graph evidence (graphify, 2026-07-07 — 2,085 nodes / 5,566 edges / 99 communities, AST-only)

- **No import cycles** across the active source — layering (UI -> Engine -> Core) holds.
- **God nodes** (edge count): `StudioShellViewModel` 248, `MainWindow` 195,
  `CadProjectStore` 154, `StudioWorkspaceController` 146, `CadSketchEntity` 77,
  `Mesh` 75, `WebViewportHost` 55. These four hubs are the OCCT/ACL migration hotspots:
  every pipeline replacement (kernel, viewport, persistence) routes through them.
- **UI reaches into meshing directly**: `StudioWorkspaceController` (UI layer) references
  both `CadProjectStore` and `SolidMesher` — the UI-side controller is doing engine work;
  boundary to restore during OCCT integration.
- Mesh types (`Mesh`, `Vector2D`, `SolidMesher`) are deeply woven into Engine + UI —
  confirms the mesh-in-the-model violation; replacement surface is well-localized to
  `Core/`, `Engine/SolidMesher`, `Engine/MeshBuilder`, viewport controls.
- Dead/parallel subsystem confirmed: `SoftwareViewportControl` (53 edges) is a second
  full viewport implementation beside `WebViewportHost`.
- Graph health: 218 dangling-endpoint edges (external/BCL refs), 376 collapsed duplicate
  edges — normal for AST extraction; graph usable, noted for honesty.
- Query the graph: `graphify query "<question>"` from repo root (`graphify-out/`, git-ignored).
  Refresh only after a major migration (`/graphify --update`).

## Reuse candidates (from git branches, not working tree)

- `feature/constraint-solver-lite` (8e50aa4) — solver prototype + xUnit tests
- `feature/acl-language-mvp` (dddd64a) — prior ACL language attempt (inspect before P2)
