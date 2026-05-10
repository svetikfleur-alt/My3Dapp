# 53. Hole feature — real CSG subtract geometry

## Goal
Task 29 left hole geometry stubbed with a HANDOFF comment: `// TODO(HANDOFF): CSG subtract not yet wired to geometry backend.` The feature tree entry appears, parameters persist, and the label renders — but no material is actually removed from the body. This task wires the real cylinder-subtract so holes are visible in the viewport.

## Scope
- In `CadProjectStore.HandleHoleSelectedBody` (around line 1047), replace the `[HANDOFF]` stub with a real cylinder-body construction and Boolean subtract.
- Reuse the existing `BooleanOperation.Subtract` path that `HandleBooleanOperation` already calls — i.e., construct an in-memory `HoleCylinderBody` at the requested diameter/depth/offset and pass it through `CompileBody` as the tool body.
- "Through All" depth: use the host body's bounding-box Z extent + margin (e.g. ×1.1) so the cylinder always punches through.
- "Blind" depth: cylinder height = `HoleDepthValue`.
- X/Y offset: translate cylinder center by `HoleOffsetX`, `HoleOffsetY` on the selected face plane.
- Preview: update the live cylinder preview in the viewport to use the same geometry (currently may be approximated).
- Remove the `[HANDOFF]` log line and the TODO comment once geometry is live.

## Out of scope
- Countersink / counterbore profiles.
- Angled or non-normal holes.
- Multiple holes in one feature.
- Changing the UI dialog (that is done).

## Files likely involved
- `Engine/CadProjectStore.cs` — `HandleHoleSelectedBody`, `CompileBody`, related helpers.
- `Engine/MeshBuilder.cs` — cylinder mesh used as CSG tool (may need `BuildCylinderMesh` helper if not present).
- `Engine/SolidMesher.cs` or `Engine/ShapeCompiler.cs` — Boolean subtract path (verify it works for non-primitive tool bodies).

## Expected behavior (acceptance)
1. Add a Box body → activate Hole tool → pick face → set ⌀10, Blind 15mm → commit → hole visibly punched in viewport mesh.
2. "Through All" removes material cleanly regardless of body height.
3. Editing the feature (double-click) and changing diameter updates viewport.
4. Feature tree label unchanged: "Hole (⌀d × Through/Xmm)".
5. Build: 0 errors, 0 warnings.

## Notes / hints
- `CompileBody` for BooleanOp takes a `BooleanFeature` which references BodyA + BodyB by index. For holes, synthesise an ephemeral cylinder body rather than a named user body — add it to the compile context temporarily if needed.
- If the subtract mesh path already works (Union works per task 26 log), Subtract likely works too — verify with a manual test before writing geometry code.
- The `[HANDOFF]` log line in the DONE.md entry for task 29 documents the exact location.

## Verifier checklist
- [ ] Hole is visible as a void in the mesh (not just a tree node).
- [ ] Through-all punches completely through even tall bodies.
- [ ] Blind holes stop at the requested depth.
- [ ] No `[HANDOFF]` / TODO comments remain in the hole code path.
- [ ] Build clean.

## Complexity
Medium — geometry hookup + CSG tool-body synthesis; no new UI.
