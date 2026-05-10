# 16. Engine — sketch geometry consolidation

## Goal
Survey `Engine/` for duplicated logic across sketch entity types (preview building, profile assembly, hit-test, serialize) and unify the obvious wins. Goal is fewer code paths to maintain before constraints / dimensions / undo land on top.

## Scope
- Audit the existing Engine files for cross-entity duplication: preview builders, geometric helpers, validation, serialization.
- Pull common helpers into `Engine/SketchEntityHelpers.cs` (or extend an existing helper file).
- Keep public engine API stable — refactor is internal only.
- Add minimal tests under `temp_verify_engine/` (or extend an existing scaffold) to lock current behavior before refactoring.
- No new features in this task — pure consolidation.

## Out of scope
- Public API renames.
- Solver work (separate constraint task).
- Performance tuning.

## Files likely involved
- `Engine/CadProjectStore.cs`
- `Engine/ProfileBuilder.cs`
- `Engine/MeshBuilder.cs`
- `Engine/ShapeCompiler.cs`
- `Engine/SolidCompiler.cs`
- New: `Engine/SketchEntityHelpers.cs` (likely).
- New / existing: `temp_verify_engine/` test scaffold.

## Expected behavior (acceptance)
1. Build runner: green.
2. Existing temp_verify_* tests still pass.
3. Line count of duplicated helper code reduced (report numbers in HANDOFF).
4. No public engine API symbol disappeared.
5. Preview / commit behavior unchanged for Line, Rectangle, Circle, Arc, Point.

## Notes / hints
- Be conservative: this task should NOT block downstream tasks; if a refactor risks regressions, flag and skip.
- Capture before/after line counts in HANDOFF for the verifier.

## Verifier checklist
- [ ] Build runner ok.
- [ ] temp_verify_engine (or existing temp_verify_*) all pass.
- [ ] Sketch tools draw identically to before.
- [ ] No public API surface change.
- [ ] HANDOFF reports line-count delta.
