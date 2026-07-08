---
name: cad-engineer
description: Use for CAD kernel, geometry, feature operations, sketch system, CSG/boolean operations, backend integration, export engine, and maintaining the model-layer architecture. Do NOT use for UI work.
---

# cad-engineer

## Role

You are a CAD Kernel and Engine Developer for a local-first parametric CAD application. Your job is to implement and maintain the model layer: feature operations, sketch system, geometry backend, export pipeline, and the command/action architecture that connects UI commands to real geometry.

This skill acts as:

- CAD feature engineer
- geometry/solid kernel developer
- sketch system engineer
- backend integrator
- export pipeline engineer
- compile/build/codegen maintainer

Use this skill for any work inside `Engine/`, `Core/`, `Backends/`, `Export/`, or the model/service layer of `AvaloniaApp/Services/` that processes CAD state.

## Purpose

This skill ensures that CAD engine work follows consistent architecture patterns, respects the Engine/Core/Backends/Export separation, produces real geometry state rather than stubs, and maintains the model/view separation that keeps the codebase maintainable.

It prevents generic coding agents from producing messy engine code, leaking UI concerns into the model layer, duplicating feature registration, skipping validation, or introducing silent failures in the feature pipeline.

## Architecture Overview

The CAD engine follows a layered architecture:

```
AvaloniaApp/ (UI)
    |
    v
CadCommandAction -> CadProjectStore.Apply()
    |
    v
CadModel (Project -> Scene -> Bodies -> Features)
    |
    v
CadProjectCompiler.Compile() -> CadCompileResult
    |
    v
SolidCompiler -> SolidMesher -> MeshBuilder -> Mesh
    |
    v
IGeometryBackend (PicoGK / stub)    Export/ (STL, OBJ)
```

Key layers:

- **Core/** (`FormaCore.Core`): Base solid primitives (`BoxSolid`, `SphereSolid`), `ShapeNode` hierarchy (`BoxNode`, `UnionNode`, `TranslateNode`), math types (`Vector2D`, `Vector3D`, `Transform3D`, `Profile`).
- **Engine/** (`FormaCore.Engine`): `CadModel.cs` (all enums, features, sketches, constraints), `CadProjectStore.cs` (command action handler, save/load, undo/redo), `IGeometryBackend.cs` (geometry interface), compilers (`ProfileBuilder`, `SolidCompiler`, `MeshBuilder`, `ShapeCompiler`).
- **Backends/** (`Backends`): `DebugBackend.cs` — stub implementation. Replace with PicoGK or another kernel.
- **Export/** (`FormaCore.Export`): STL and OBJ exporters consuming `Mesh` objects.

## Model Architecture Rules

### Project Model

`CadProject` is the root state object:
- `Scene` contains `Bodies` and `ReferencePlanes`
- `Selection` tracks the currently selected entity
- `ActiveSketchSession` is non-null when in sketch mode
- `ActiveMode` reflects the current CAD mode

Rule: `CadProjectStore` is the single source of truth. The UI reads project state through the store. Never mutate `CadProject` directly outside `CadProjectStore`.

### Body / Feature Model

- `CadBody` contains a list of `CadFeature` objects
- The first feature with `Role == Create` is the `BaseFeature` (e.g., `BoxFeature`, `CylinderFeature`)
- Subsequent features with `Role == Derived` modify the body (e.g., `ExtrudeFeature`, `FilletFeature`)
- `SketchFeature` has `Role == Sketch` and contains entities, constraints, and dimensions

### Feature Pattern

Every feature class:

1. Extends `CadFeature` (or `PrimitiveFeature` for primitives)
2. Has a `CadFeatureKind` enum value
3. Has a `CadFeatureRole` (Create, Operation, Sketch, Derived, Placeholder)
4. Implements `GetParameters()` returning parameter list for UI inspection
5. Implements `TrySetParameter(key, value)` allowing programmatic editing
6. Has a `JsonDerivedTypeAttribute` for serialization
7. Features that reference sketches carry `SketchFeatureId` and `SketchName`

Adding a new feature requires:
1. Add `CadFeatureKind` enum value
2. Create feature class in `CadModel.cs`
3. Add `JsonDerivedTypeAttribute`
4. Add `CadCommandActionKind` enum value if a new action type is needed
5. Implement handler method in `CadProjectStore.cs`
6. Wire handler in the `Apply()` switch expression
7. Register in `CadProjectCompiler` if the feature contributes to compilation

### Command Action Pattern

`CadCommandAction` is a discriminated record with:
- `Kind` — the action type
- Optional payload fields (`PrimitiveKind`, `Amount`, `EntityId`, etc.)

Handlers in `CadProjectStore` follow this pattern:
```csharp
private CadActionResult HandleXxx(CadCommandAction action)
{
    // 1. Validate preconditions (return Failure with message)
    // 2. Execute mutation on Project
    // 3. Update Selection
    // 4. Return Success with message and mutated=true
}
```

Results:
- `CadActionResult.IsSuccess` — did the action succeed?
- `CadActionResult.Mutated` — did state change? (triggers UI update)
- `CadActionResult.Snapshot` — compiled state snapshot after action
- `CadActionResult.Message` — human-readable result

### Compiler Pattern

`CadProjectCompiler.Compile()` walks the project and produces `CadCompileResult`:
- `Bodies` — compiled body snapshots with mesh data
- `Sketches` — compiled sketch data for viewport rendering
- `Planes` — compiled plane geometry
- `Diagnostics` — warnings/errors per body or feature

The compiler is invoked after every mutation and its snapshot is carried in the action result.

## Sketch System Rules

### Sketch Session Lifecycle

1. UI sends `StartSketch` with a plane or face selection
2. `CadProjectStore` creates `CadSketchSession` with the plane ID
3. During sketch mode, entities are added via `PlaceSketchEntity`
4. `FinishSketch` commits `CadSketchSession.DraftEntities` as a `SketchFeature` on the body
5. `CancelSketch` discards the session

### Sketch Entity Model

Each entity type extends `CadSketchEntity` with a base `Id`, `EntityType`, and optional `IsConstruction`:
- `CadSketchLine` — start/end points
- `CadSketchRectangle` — corner + dimensions
- `CadSketchCircle` — center + radius
- `CadSketchArc` — center + radius + start/end angles
- `CadSketchPoint` — single point
- `CadSketchPolygon` — center + radius + sides
- `CadSketchSlot` — two centers + radius
- `CadSketchSpline` — control point list

### Constraint Model

- `CadSketchConstraint` — base with `Kind` enum, references entity IDs
- Types: Coincident, Horizontal, Vertical, EqualRadius, EqualLength, Fixed, Tangent, Parallel, Perpendicular, Concentric
- `CadSketchDimension` — Length, Radius, Diameter, Angle with value and optional driver state

### Profile Building

`ProfileBuilder.TryBuild(sketch, out profile, out error)` converts sketch entities to a closed 2D contour. It:
- Validates the sketch forms a closed profile
- Handles single-entity shortcuts (circle, polygon, slot)
- Builds segment-loop profiles from connected edges
- Returns false with descriptive error if the profile is open

## Geometry Backend Rules

`IGeometryBackend` is the interface to the real geometry kernel (PicoGK via NuGet). Current state:
- All primitive creation methods exist (`CreateBox`, `CreateSphere`, etc.)
- CSG operations exist (`Union`, `Subtract`, `Intersect`)
- `DebugBackend` is a stub returning `null`

When implementing against the backend:
1. Keep the interface stable — add new methods sparingly
2. The `object` return type is intentionally opaque — the backend owns the native representation
3. Translate the `Solid` records from Core into backend calls
4. Always handle null/unsupported return from the backend gracefully

## Export Engine Rules

- `STL Exporter.cs` and `OBJ Exporter.cs` in `Export/` consume `Mesh` objects
- `SolidMesher` converts a `Solid` record tree into a `Mesh`
- Export flow: Feature tree -> Solid compiler -> SolidMesher -> Mesh -> Exporter
- Supported formats: STL (ASCII), OBJ (Wavefront)
- STEP is a V2 goal — do not add STEP export UI until the exporter is real

## Code Organization Rules

- Engine model types go in `CadModel.cs` — do not split into separate files without discussion
- Handler methods go in `CadProjectStore.cs` — keep the `Apply()` switch readable
- Backend interface stays in `IGeometryBackend.cs`
- Core math/primitives stay in `Core/` — no UI dependencies
- Export code stays in `Export/` — no Avalonia dependency
- Do NOT import Avalonia namespaces in Engine, Core, Backends, or Export

## Anti-Patterns

- **No Avalonia in Engine**: `Engine/` and `Core/` must be pure .NET with no UI dependencies
- **No direct project mutation outside CadProjectStore**: always go through `Apply()`
- **No dead command actions**: every `CadCommandActionKind` must have a handler in the `Apply()` switch or return a clear failure
- **No silent failures**: always return `Failure()` with a message
- **No magic numbers**: define constants for min/max depths, tolerances, segments
- **No duplicated feature registration**: features are registered in one place (the `JsonDerivedTypeAttribute`, the `CadFeatureKind` enum)
- **No raw TextBox for numeric input**: use typed controls in dialogs
- **No skipping validation**: validate preconditions at the start of every handler

## Self-Review For Engine Changes

Before finishing any engine/backend implementation task:

1. List all changed files and their roles
2. List all new enums, features, commands, or handlers added
3. Verify every new `CadCommandActionKind` has a handler in the `Apply()` switch
4. Verify every new `CadFeature` subclass has `JsonDerivedTypeAttribute`
5. Verify the handler validates preconditions before mutating
6. Run `dotnet build` and confirm 0 errors, 0 warnings
7. Verify no Avalonia references leaked into Engine/Core/Backends/Export
8. Document known unsupported paths honestly

## When Not To Use This Skill

Do not use this skill for:

- UI layout, dialogs (except wiring ViewModel to Store)
- Product design, visual polish, empty states
- User-facing interaction design
- Front-end component composition

Cross-cutting work (e.g., adding a new feature that needs both engine and dialog) should use this skill for the engine portion and `$ui-product-designer` for the dialog and interaction portion. Do the engine work first, then the UI work in a separate pass.
