# Engine Review Rubric

Use this rubric before accepting any engine/backend change. Rate each item as Pass, Concern, or Fail.

## Architecture Compliance

- [ ] Does the change respect the Engine/Core/Backends/Export separation?
- [ ] Are there no Avalonia or UI namespace imports in Engine, Core, Backends, or Export?
- [ ] Is `CadProjectStore` the single point of mutation for project state?
- [ ] Does the change avoid direct project mutation outside the store?

## Feature Implementation

- [ ] Are all new enums added to the correct enum type?
- [ ] Does every new `CadFeature` subclass have `JsonDerivedTypeAttribute`?
- [ ] Are sensible defaults provided for all feature parameters?
- [ ] Are `GetParameters()` and `TrySetParameter()` implemented?
- [ ] Is the feature class registered with the correct `CadFeatureKind`?

## Handler Implementation

- [ ] Are preconditions validated at the start of each handler?
- [ ] Does validation return `Failure()` with a clear message on failure?
- [ ] Does the handler update `Project.Selection` appropriately?
- [ ] Is every new `CadCommandActionKind` wired in the `Apply()` switch?
- [ ] Is `mutated: true` returned when state actually changes?
- [ ] Are there no handlers that silently do nothing?

## Serialization

- [ ] Do all new classes have proper `[JsonDerivedType]` attributes?
- [ ] Is transient state marked `[JsonIgnore]`?
- [ ] Does save/load roundtrip preserve the new state?
- [ ] Are default constructors sufficient for deserialization?

## Compilation

- [ ] Does the compiler handle the new feature type?
- [ ] Are compile errors reported as diagnostics rather than silent failures?
- [ ] Does the compiled snapshot include the new feature's state?

## Geometry And Backend

- [ ] Are `IGeometryBackend` interface changes minimal and justified?
- [ ] Is the stub backend updated consistently with the interface?
- [ ] Are unsupported backend operations handled gracefully (no crashes)?
- [ ] Does the geometry pipeline (Solid -> Mesh -> export) work for the new feature?

## Sketch System

- [ ] Are sketch entities correctly added to `DraftEntities` during session?
- [ ] Does `FinishSketch` correctly commit entities to `SketchFeature`?
- [ ] Does `CancelSketch` clean up session state?
- [ ] Does `ProfileBuilder.TryBuild()` correctly handle the new entity type?
- [ ] Are constraints and dimensions correctly applied and persisted?

## Code Quality

- [ ] No magic numbers — all constants are defined with clear names
- [ ] No silent exception swallowing
- [ ] No duplicate code patterns (extract shared logic)
- [ ] No console output or Trace.WriteLine in production paths (use diagnostics)
- [ ] Switch expressions are exhaustive or have a `_` default case

## Build And Test

- [ ] `dotnet build` passes with 0 errors, 0 warnings
- [ ] No new NuGet package dependencies added
- [ ] No new files created outside the intended subsystem
- [ ] Existing working behavior is preserved (no regression)

## Final Output

The implementation report should include:

- Files changed and their roles
- New enums, features, commands, or handlers added
- Build result (errors, warnings)
- Known limitations or unsupported paths
- Suggested next engineering task
