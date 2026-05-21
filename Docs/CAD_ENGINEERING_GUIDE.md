# CAD Engineering Guide

## Principles

1. **Build before every change** — run `dotnet build` first. If it fails, fix before proceeding.
2. **Single source of truth** — `CadProjectStore` owns all project state. Never mutate `CadProject` directly.
3. **Feature append-only** — features are appended to a body's feature list. Removal deletes from the insertion point forward.
4. **Validation first** — every handler validates preconditions before mutating state.
5. **No silent failure** — every failure path returns `CadActionResult.Failure(message)`.
6. **No UI in engine** — Engine, Core, Backends, and Export never import Avalonia, WPF, or WinForms namespaces.

## File Conventions

| File | Convention |
|---|---|
| `CadModel.cs` | Single file for all enums, feature classes, sketch entities, and model types |
| `CadProjectStore.cs` | Command action handler + save/load. Keep `Apply()` switch readable |
| `IGeometryBackend.cs` | Stable interface — add methods sparingly |
| `ProfileBuilder.cs` | Static utility — no state |
| `Solid*Compiler.cs` | Compiler pattern — stateless, produces output from input |

## Naming Conventions

- `Cad*Kind` — enums (e.g., `CadFeatureKind`, `CadSketchToolKind`)
- `Cad*Feature` — feature classes with `CadFeatureRole` (e.g., `ExtrudeFeature`, `BoxFeature`)
- `CadSketch*` — sketch entities (e.g., `CadSketchLine`, `CadSketchCircle`)
- `Handle*` — handler methods in `CadProjectStore` (e.g., `HandleExtrudeSelectedSketch`)
- `*Node` — `ShapeNode` subclasses in Core (e.g., `BoxNode`, `UnionNode`)
- `*Solid` — `Solid` record types in Core (e.g., `BoxSolid`, `SphereSolid`)

## Common Patterns

### Handler Pattern

```csharp
private CadActionResult HandleXxx(CadCommandAction action)
{
    // Validate
    var body = ResolveSelectedBody();
    if (body is null) return Failure("Select a body first.");

    // Create feature
    var feature = new XxxFeature { Name = $"Xxx{NextIndex(...)}", ... };

    // Mutate
    body.Features.Add(feature);
    Project.Selection = ...;

    // Respond
    return Success("Created Xxx.", mutated: true);
}
```

### NextIndex Pattern

Use `NextIndex()` for automatic naming:

```csharp
private int NextIndex(CadPrimitiveKind kind) =>
    Project.Scene.Bodies.Count(body =>
        body.Name.StartsWith(kind.ToString(), StringComparison.Ordinal));
```

## Compilation Checklist

When adding a feature that produces geometry:

1. Verify `SolidCompiler` handles the new feature type
2. Verify `SolidMesher` produces valid mesh from the solid
3. Verify the mesh appears in the viewport
4. Verify STL/OBJ export includes the new geometry
5. Verify save/load roundtrip preserves the feature

## Common Pitfalls

- **Missing `JsonDerivedTypeAttribute`**: Feature will fail to serialize/deserialize
- **No handler in `Apply()` switch**: Action will hit the default `Failure()` case
- **Direct project mutation outside store**: State will diverge from UI
- **Not calling `NextIndex()`**: Feature names will collide
- **Not returning `mutated: true`**: UI will not refresh
- **Avalonia import in Engine**: Build will fail or create unwanted dependency

## Command Action Parameters

`CadCommandAction` supports these payload fields:

| Field | Type | Purpose |
|---|---|---|
| `Kind` | enum | Action type discriminator |
| `PrimitiveKind` | `CadPrimitiveKind?` | Primitive to create |
| `SketchTool` | `CadSketchToolKind?` | Sketch tool to activate |
| `EntityId` | `Guid` | Target entity ID |
| `EntityName` | `string?` | Target entity name fallback |
| `PlaneKind` | `CadReferencePlaneKind?` | Reference plane selection |
| `Axis` | `CadAxis` | Direction axis |
| `Amount` | `double` | Primary numeric parameter |
| `U`, `V` | `double` | Secondary numeric parameters |
| `EntityIdB` | `Guid` | Second entity ID (booleans, loft) |
| `ExtrudeOp` | `CadExtrudeOperation` | Extrude operation mode |
| `TargetBodyId` | `Guid` | Target body for join/cut |

When a new action needs more fields, add them to the record. Keep the record flat — avoid nested parameter objects.

## Database / State Persistence

- Format: JSON via `System.Text.Json`
- Extension: `.umxproj`
- Location: User-selected path or `%APPDATA%/My3DApp/`
- Autosave: `%APPDATA%/My3DApp/Autosave/`
- Settings: `%APPDATA%/My3DApp/settings.json`

## Agent Guidance

For CAD engine/kernel tasks, use `$cad-engineer`.
For UI/product design tasks, use `$ui-product-designer`.
