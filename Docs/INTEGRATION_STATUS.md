# INTEGRATION STATUS

Updated: 2026-07-07 (P1 complete)

## P1 result (exact spine) — DONE, verified

- `My3DApp.Occt` C++/CLI bridge builds against OCCT 7.9 (vc144); narrow API:
  box/cylinder/translate/boolean, AIS/V3d viewer with body/face/edge/vertex selection
  modes, STEP writer. Native core (`OcctCore.cpp`) compiled without /clr.
- `ExactSpineWindow` is the app's primary runtime path (legacy MainWindow +
  WebView2/Three.js + SoftwareViewportControl are OUT of the runtime path; source
  retained until P4/P6 salvage).
- Verified: native viewport renders shaded + real B-Rep edges + true wireframe;
  wheel zoom / RMB rotate / hover / LMB pick; topology events reach managed code
  (`spine-events.log`: `Selected: Face #3 of body …`); clean WM_CLOSE shutdown.
- STEP verified independently in FreeCAD 1.0: 1 valid Solid, 7 faces, bbox exactly
  80×60×30 mm, volume 130428.320 mm³ (= analytic), surfaces Plane+Cylinder (exact).
- Tests: 67/67 green incl. kernel lifecycle, handle lifetime, invalid input,
  exception translation, STEP content assertions (no TRIANGULATED entities).
- Build quirk documented: framework MSBuild lacks the .NET SDK resolver →
  build bridge with `MSBuildSDKsPath=<dotnet sdk>\Sdks`, `MSBuildEnableWorkloadResolver=false`
  (encoded in the vcxproj via DisableImplicitFrameworkReferences + explicit ref pack).
- Topology identity: transient per-instance indices only (documented in
  `TopologySelection`); `ITopologyNamingService` interface reserved for OCAF/TNaming.

## Component status vs. CAD_CANON target

| Component | Current | Target | Status |
|---|---|---|---|
| Geometry kernel | OCCT 7.9 B-Rep via `My3DApp.Occt` (P1 subset) + legacy mesh CSG still in tree | Full feature set on OCCT | P1 SUBSET WORKING |
| Viewport | OCCT AIS/V3d in Avalonia NativeControlHost (`OcctViewportControl`) | + sketch/dimension overlay, previews | P1 WORKING |
| ACL | Line-interpreter (`CadScriptLibrary`) | Real lexer/parser/AST + source maps | NOT STARTED (prior attempt on `feature/acl-language-mvp`) |
| Persistence | `.umxproj` JSON via `CadProjectStore` | ACL files; .umxproj import shim only | NOT STARTED |
| Sketch solver | Constraints stored, never solved | Solver Lite + driving dimensions | PROTOTYPE on `feature/constraint-solver-lite` (unaudited) |
| Assistant | Provider interface, Recipe-JSON contract | ACL-patch contract, DeepSeek default / Claude recovery | PARTIAL (interface reusable) |
| Export | STL/OBJ from mesh | STEP exact + STL/3MF boundary; Parasolid adapter | PARTIAL (STL mechanics reusable) |
| Capability status registry | None (no-op buttons exist) | WORKING/LIMITED/IN_DEVELOPMENT/HIDDEN | NOT STARTED |

## External dependencies

- OCCT 7.9 prebuilt Windows binaries — approved (LGPL 2.1 + exception).
- MSVC C++ workload — build-time, needs presence check before P1.
- Parasolid — requires commercial Siemens SDK; adapter slot only, honest IN_DEVELOPMENT.
- DeepSeek / Anthropic API keys — user-provided, DPAPI-protected local storage (P7).

## Stage 0 verification

- `git status` healthy, working tree clean, build check: see Stage 0 checkpoint.
- Knowledge graph: `graphify-out/` (git-ignored); refresh only after major migrations.
