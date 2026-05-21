# CAD Architecture Overview

## Layered Design

The application follows a strict layered architecture:

```
AvaloniaApp (UI / ViewModels / Dialogs)
    |
    | dispatches CadCommandAction
    v
Engine (CadProjectStore, CadModel, Compilers)
    |
    | produces Solid records + Meshes
    v
Core (Solid primitives, ShapeNode graph, math types)
    |
    | calls IGeometryBackend
    v
Backends (PicoGK or stub)
```

Separately:

```
Engine -> SolidMesher -> Mesh -> Export/ (STL, OBJ)
```

## Key Architectural Rules

1. **Engine has no UI dependencies** — `Engine/` and `Core/` are pure .NET. No Avalonia, no WPF, no WinForms.
2. **CadProjectStore is the single source of truth** — all project state mutations go through `ProjectStore.Apply()`.
3. **Feature tree is append-only** — new features are appended to `CadBody.Features`. Deleting removes from the insertion point forward.
4. **Compilation is stateless** — `CadProjectCompiler.Compile()` reads the project and produces a snapshot. It has no side effects.
5. **Export is read-only** — exporters consume `Mesh` objects. They do not modify project state.

## Model Structure

```
CadProject
  ├── Name, Units, ActiveMode
  ├── Selection (current entity selection)
  ├── ActiveSketchSession (null when not sketching)
  └── Scene
      ├── ReferencePlanes (Top, Front, Right, Datum)
      └── Bodies
          └── Features
              ├── PrimitiveFeature (Box, Cylinder, Sphere, ...)
              ├── SketchFeature (entities, constraints, dimensions)
              ├── DerivedFeature (Extrude, Revolve, Fillet, ...)
              └── PlaceholderFeature (stub)
```

## Command Flow

```
User action (toolbar click, key shortcut)
    -> ViewModel command
    -> new CadCommandAction(Kind, ...)
    -> CadProjectStore.Apply(action)
    -> switch(action.Kind) { ... handler }
    -> CadActionResult { IsSuccess, Mutated, Snapshot, Message }
    -> ViewModel updates bindings
    -> Viewport receives compiled mesh data
```

## Feature Types

- **Primitive features**: Immediate solid geometry (Box, Cylinder, Sphere, etc.) with `Role == Create`. Each creates a new body.
- **Sketch features**: 2D sketch planes containing entities, constraints, and dimensions with `Role == Sketch`.
- **Derived features**: Built from sketches or existing geometry (Extrude, Revolve, Fillet, Chamfer, etc.) with `Role == Derived`.
- **Operation features**: Modify existing geometry (Move, Boolean) with `Role == Operation`.
- **Placeholder features**: Stubs for future implementation with `Role == Placeholder`.

## Sketch System

- Sketching requires an active `CadSketchSession` created by `StartSketch`
- Entities are added to `DraftEntities` during the session
- `FinishSketch` commits draft entities to a `SketchFeature` on the current body
- `ProfileBuilder.TryBuild()` converts sketch entities to a closed 2D contour for extrusion/revolve
- Constraints and dimensions are stored in the `SketchFeature`

## Compilation Pipeline

```
CadProject
    -> CadProjectCompiler.Compile()
    -> For each body:
         Feature tree -> SolidCompiler (produces Solid record tree)
         Solid record tree -> SolidMesher -> Mesh
    -> CadCompileResult (bodies, meshes, sketches, planes, diagnostics)
```

## Export Pipeline

```
Mesh (from compilation)
    -> STL Exporter (ASCII)
    -> OBJ Exporter (Wavefront)
```

Supported formats: STL, OBJ. STEP is V2.

## Agent Guidance

For engine implementation work, use `$cad-engineer` and read `.agents/skills/cad-engineer/SKILL.md`.
For UI and product design work, use `$ui-product-designer`.
