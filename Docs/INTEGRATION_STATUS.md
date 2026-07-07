# INTEGRATION STATUS

Updated: 2026-07-07 (Stage 0)

## Component status vs. CAD_CANON target

| Component | Current | Target | Status |
|---|---|---|---|
| Geometry kernel | Mesh CSG (`Core/`, `Engine/SolidMesher`) + dormant PicoGK | OCCT B-Rep via `My3DApp.Occt` C++/CLI | NOT STARTED |
| Viewport | WebView2 + Three.js (`WebViewportHost`) | OCCT AIS/V3d in Avalonia NativeControlHost | NOT STARTED |
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
