# Project: P4 Core Exact Modeling

## Architecture
This project implements the Core Exact Modeling pipeline for My3DApp:
1. **ACL Lexing/Parsing/AST**: Lexes and parses the ACL source code (with sketch support, parameter values, shape commands).
2. **AST-aware Edit Service (`IAclSourceEditService`)**: Modifies the ACL source text in an AST-aware manner (inserting/modifying shape nodes) based on UI dialog configurations.
3. **Exact Feature Graph (`ExactFeatureGraph`)**: Evaluates a parametric network of modeling feature nodes.
4. **CAD Kernel Bridge (`IExactCadKernel` / `OcctExactKernel`)**: A managed wrapper calling the native C++/CLI bridge (`My3DApp.Occt`) to perform solid modeling using OpenCASCADE Technology (OCCT).
5. **UI Feature Dialogs**: Extrude, Hole, and Linear Pattern dialogs allowing users to configure features, preview native geometry, and commit changes via ACL source patches.

```
+------------------+     patches      +------------------------+
| UI Dialog        | ---------------> | IAclSourceEditService  |
| (Confirm/Cancel) |                  +------------------------+
+------------------+                               |
        ^                                          v
        | preview geometry                   +-----------+
        |                                    |    ACL    |
+--------------------------+  rebuilds graph |  Source   |
| ExactSceneController /   | <-------------- +-----------+
| StudioNativeViewport     |                       |
+--------------------------+                       v
        ^                                    +-----------+
        | builds shapes                      | AclParser |
        |                                    +-----------+
+--------------------------+                       |
| ExactFeatureGraph        | <---------------------+
| (nodes evaluate logic)   |
+--------------------------+
        |
        v
+--------------------------+
| IExactCadKernel          |
+--------------------------+
        |
        v
+--------------------------+
| My3DApp.Occt Bridge      |
+--------------------------+
        |
        v
+--------------------------+
| Native OCCT C++ Kernel   |
+--------------------------+
```

## Milestones
| # | Name | Scope | Dependencies | Status | Conv ID |
|---|------|-------|-------------|--------|---------|
| 1 | Native OCCT Bridge | My3DApp.Occt, build tools check, C++/CLI shape operations, CS0535 resolve | None | IN_PROGRESS | 983be25f-4dd8-456a-87cc-dc8ca2d9f443 |
| 2 | Exact Feature Graph | Feature nodes implementation (`ExactExtrudeNode`, `ExactHoleNode`, `ExactLinearPatternNode`), unit tests | M1 | PLANNED | TBD |
| 3 | ACL Source Editing | `IAclSourceEditService`, AclSourcePatchTests, AST-aware formatting | M2 | PLANNED | TBD |
| 4 | UI Feature Dialogs | Wire Extrude/Hole/Pattern dialogs, exact previews, cancel disposal, STALE build display | M3 | PLANNED | TBD |
| 5 | E2E Testing & Audit | Tier 1-4 tests, STEP export validation, Forensic Audit | M4 | IN_PROGRESS | 1bd18f69-6429-41fa-a246-c8e0d5676255 |

## Interface Contracts
### `IExactCadKernel`
- `CreateWire(double[] points2d, bool closed)`: Creates a native polygonal wire.
- `CreateCircleWire(double radius)`: Creates a native circular wire.
- `CreateFace(IExactBodyHandle wire)`: Creates a planar face from a closed wire.
- `CreatePrism(IExactBodyHandle face, double dx, double dy, double dz)`: Sweeps a face along a vector to create a solid.
- `CreateCompound(IReadOnlyList<IExactBodyHandle> bodies)`: Combines multiple bodies into one compound shape.

### `IAclSourceEditService`
- `AppendFeatureToPart(string source, string partName, string featureCode)`: Inserts a feature statement inside a part block.
- `AppendFeatureToSketch(string source, string sketchName, string featureCode)`: Inserts a sketch entity/operation inside a sketch block.
- `AppendBlock(string source, string blockCode)`: Appends a top-level block.

## Code Layout
- `Engine/Exact/IExactCadKernel.cs`: CAD kernel interface.
- `Engine/Exact/OcctExactKernel.cs`: C# adapter for OCCT kernel.
- `My3DApp.Occt/Bridge.cpp` & `OcctCore.cpp`: C++/CLI bridge implementation.
- `Engine/Exact/Graph/ExactFeatureGraph.cs`: Feature nodes evaluation.
- `Engine/Acl/AclSourceEditService.cs`: AST-aware ACL modification service.
- `AvaloniaApp/Dialogs/`: Dialog axaml and code-behind files.
- `tests/EngineTests/`: Unit tests and mock verification.
