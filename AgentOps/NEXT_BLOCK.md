# Next Block — 2026-05-16

## Task 69 — Hole feature: real CSG subtract geometry

**Spec:** `AgentOps/TASKS/69-hole-csg-geometry.md`

**Why this block:**
Priority 1 ("stabilize runtime"): the Hole feature has full UI, dialog, and feature tree support but removes no material from the body. The `HandleHoleSelectedBody` stub is the last [HANDOFF] blocker from task 29. Fixing it makes a real user-visible 3D operation work end-to-end.

**What to do:**
1. Find `HandleHoleSelectedBody` in `Engine/CadProjectStore.cs` (the `[HANDOFF]` / `TODO` comment around line ~1047).
2. Construct an in-memory cylinder mesh as the "tool body":
   - Diameter = `HoleDiameter`, center offset = `HoleOffsetX` / `HoleOffsetY` on the face plane.
   - Through-All depth: host body bounding-box Z extent × 1.1 (so it always punches through).
   - Blind depth: `HoleDepthValue`.
3. Pass the cylinder through the same `BooleanOperation.Subtract` path that `HandleBooleanOperation` already uses for Union (verify Union works as reference).
4. Remove the `[HANDOFF]` log line and TODO comment.
5. Ensure `double-click → edit → change diameter → viewport updates`.
6. Build: 0 errors, 0 warnings.

**Acceptance:**
- [ ] Hole is visibly punched in the viewport mesh (not just a tree node).
- [ ] Through-All clears even tall bodies.
- [ ] Blind holes stop at the requested depth.
- [ ] Double-click edit updates geometry.
- [ ] No [HANDOFF] / TODO comments remain in hole code path.
- [ ] Build clean.

**Complexity:** Medium (geometry hookup + ephemeral tool-body synthesis; no new UI).

**After this block:**
→ Task 70 — Boolean Subtract / Intersect real mesh CSG (same priority tier).
→ Task 71 — Polyline sketch tool (priority 2: Sketch foundation).
