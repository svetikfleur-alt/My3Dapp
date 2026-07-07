# CURRENT WORK PACKET

## Stage 0 — Repository preparation + knowledge graph  ✅ (this packet)

- Located real git root (`D:\My3DApp\My3DApp`), fixed broken index (23 stale
  `.agent-worktrees` gitlinks), pruned 27 stale worktree registrations.
- Committed outstanding local work (baseline `917c1fc`), bundle backup created.
- Archived temp/research dirs, deleted generated output, updated `.gitignore`.
- Ran `/graphify` over active source; findings in INTEGRATION_STATUS.md.

## Next — P1: OCCT bring-up + native viewport vertical slice

Goal: minimal ACL file (`part` + sketch rect + `extrude`) -> feature graph ->
OCCT box solid -> AIS/V3d viewport hosted in Avalonia (`NativeControlHost`) with
face selection -> STEP export verified in an external reader.

New projects: `My3DApp.Occt` (C++/CLI interop), `OcctViewportControl` (Avalonia).
Blockers to clear first: OCCT 7.9 prebuilt binaries + MSVC C++ workload present.

## Phase order (approved plan)

P0 repo ✅ -> P1 OCCT slice -> P2 ACL core -> P3 solver lite + dims ->
P4 features to MVP gate -> P5 ACL persistence + export -> P6 Onshape-like shell ->
P7 assistant (DeepSeek/Claude) -> MVP+.
