# CURRENT WORK PACKET

## Stage 0 — Repository preparation + knowledge graph  ✅ (this packet)

- Located real git root (`D:\My3DApp\My3DApp`), fixed broken index (23 stale
  `.agent-worktrees` gitlinks), pruned 27 stale worktree registrations.
- Committed outstanding local work (baseline `917c1fc`), bundle backup created.
- Archived temp/research dirs, deleted generated output, updated `.gitignore`.
- Ran `/graphify` over active source; findings in INTEGRATION_STATUS.md.

## P1 — OCCT exact-kernel bring-up + native viewport spine ✅ (verified 2026-07-07)

Delivered: `My3DApp.Occt` C++/CLI bridge (OCCT 7.9), engine boundary
(`Engine/Exact/*`), `OcctViewportControl` (NativeControlHost + AIS/V3d),
`ExactSpineWindow` as primary runtime path, app-triggered STEP verified in
FreeCAD, 67/67 tests. Details: INTEGRATION_STATUS.md.

## Next — P2: ACL core

Real lexer/parser/AST + source maps, units, expressions, functions, if/loops,
line:col diagnostics; feature graph builder feeding `IExactCadKernel`;
`Tools/AclSmokeRunner` as parser golden-file harness. Inspect
`feature/acl-language-mvp` branch (dddd64a) before writing new code.

## Phase order (approved plan)

P0 repo ✅ -> P1 OCCT slice ✅ -> P2 ACL core -> P3 solver lite + dims ->
P4 features to MVP gate -> P5 ACL persistence + export -> P6 Onshape-like shell ->
P7 assistant (DeepSeek/Claude) -> MVP+.
