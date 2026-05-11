# 54. Boolean Subtract and Intersect — real mesh CSG

## Goal
Task 26 completed the Boolean UI and Union geometry, but noted: "Subtract+Intersect mesh stubbed (NotSupportedException caught by renderer)." Both operations silently fall through to the catch block and return the tool body unchanged. This task implements real mesh-level Boolean Subtract and Intersect.

## Scope
- In `CompileBody` (or wherever `BooleanOperation.Subtract` / `BooleanOperation.Intersect` are handled), replace the stub paths with actual CSG mesh operations.
- If a full BSP/CSG library is not present: implement a heuristic mesh-subtract using the existing triangle mesh representation — at minimum a per-face inside/outside test followed by mesh stitching for convex bodies (covers the common "cut a box with a box" case). Flag non-convex edge cases with a user-visible error rather than silently doing nothing.
- Intersect = keep only the overlap region (inside both meshes).
- After operation: resulting mesh assigned to the surviving body; tool body hidden (same as Union behavior).
- Null/degenerate result (e.g., bodies don't overlap) should surface a status message rather than crash.

## Out of scope
- Full non-manifold CSG for arbitrary organic meshes (deferred — too complex for this pass).
- UI changes (buttons/dialog already done in task 26).
- Changing how Union works.

## Files likely involved
- `Engine/CadProjectStore.cs` — `CompileBody` Boolean branch, `HandleBooleanOperation`.
- `Engine/MeshBuilder.cs` — may need `SubtractMeshes` / `IntersectMeshes` helpers.
- `Engine/SolidMesher.cs` — verify existing tessellation pipeline is suitable input.

## Expected behavior (acceptance)
1. Union: unchanged — still works.
2. Subtract: place two overlapping boxes → Subtract → result body is the first box minus the overlap volume (visually correct for axis-aligned convex case).
3. Intersect: same setup → result is only the shared volume.
4. Non-overlapping bodies → status bar / console shows a descriptive error; no crash.
5. Build: 0 errors, 0 warnings.

## Notes / hints
- Check if a lightweight CSG library (e.g. a C# port of csg.js / Carve) is already in `Backends/` or `Core/`; if so, prefer that over hand-rolling.
- The existing `BooleanOperation.Union` code path is the reference — subtract is analogous (invert face winding of tool mesh before merge).
- Keep the `NotSupportedException` path as a final fallback with a meaningful message rather than deleting it — aids future debugging.

## Verifier checklist
- [ ] Subtract produces visibly correct result for two overlapping AABBs.
- [ ] Intersect produces visibly correct result (shared volume only).
- [ ] Non-overlapping Subtract/Intersect shows an error message, no crash.
- [ ] Union still works correctly (regression).
- [ ] Build clean.

## Complexity
High — mesh CSG is geometrically complex; constrain to convex-hull approximation if needed and document the limitation.
