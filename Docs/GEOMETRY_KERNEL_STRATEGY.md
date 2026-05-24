# Geometry Kernel Strategy

## Goal

Support multiple geometry backends without letting UI, dialogs, or workspace state couple directly to any one kernel implementation.

## Strategic Pipeline

```text
Natural language / ACL / Templates / Recipe
-> validated CAD recipe
-> CAD command executor
-> GeometryKernelAdapter
-> current solid/mesh kernel or PicoGK-inspired experimental kernel
-> body state / history / viewport
-> export / prepare
```

## Current App Reality

The current app already has a stable local-first path:

```text
UI
-> CadCommandAction
-> CadProjectStore.Apply(...)
-> CadProject / CadProjectCompiler
-> FormaCore.Core.Solid
-> SolidMesher
-> Mesh
-> Viewport / STL export
```

That path stays the default.

## Kernel Abstraction Boundary

New abstraction:

- `IGeometryKernelAdapter`
- `GeometryKernelBody`
- `GeometryKernelCapabilities`

Implementations:

- `CurrentGeometryKernelAdapter`
- `PicoGkExperimentalGeometryKernelAdapter`

## Current Adapter

`CurrentGeometryKernelAdapter` wraps:

- `BoxSolid`
- `CylinderSolid`
- `BooleanSolid`
- `TransformedSolid`
- `SolidMesher`
- `STLExporter`

Use case:
- stable fallback
- parity with the current app behavior

## PicoGK Experimental Adapter

`PicoGkExperimentalGeometryKernelAdapter` wraps:

- `PicoGK.Library`
- `PicoGK.Utils.mshCreateCube`
- `PicoGK.Utils.mshCreateCylinder`
- `PicoGK.Voxels(mesh)`
- voxel booleans
- `mshAsMesh()`
- `SaveToStlFile(...)`

Use case:
- experimental boolean-heavy geometry
- mesh-first printable bodies
- future implicit / lattice / shell exploration

Operational note:

- the currently installed PicoGK package expects geometry work to happen inside a `PicoGK.Library.Go(...)` session
- that means the PicoGK adapter is currently a **session-scoped experimental backend**, not a drop-in always-on runtime replacement

## Recipe / ACL Relation

The UI should never call PicoGK directly.

Instead:

1. ACL / recipe expresses *intent*
2. command executor validates the intent
3. executor chooses a geometry adapter
4. adapter returns backend-owned body objects
5. those bodies are tessellated/exported through a controlled boundary

## Body Representation Boundary

UI, tree, and properties panels should see:

- body id
- display name
- source feature/template
- derived preview mesh / diagnostics

They should **not** know:

- whether the body is a `Solid`
- whether the body is a `PicoGK.Voxels`
- whether booleans were voxel or mesh based

## Viewport Boundary

Viewport should consume:

- neutral render meshes
- selection metadata
- transform / highlight state

Viewport must never depend on kernel-native objects.

## Export Boundary

Export should consume:

- neutral mesh
- or adapter-owned STL/OBJ emitters

The export panel must not care whether geometry came from the current mesher or PicoGK.

## UI Rule

UI must never know kernel internals.

That includes:

- dialogs
- toolbars
- properties panels
- tree nodes
- assistant suggestions

The kernel choice belongs below command execution and above tessellation/export.

## Hybrid Roadmap

### Near-term

- keep current kernel default
- add experimental PicoGK-backed prototype commands

### Mid-term

- add recipe execution branch that can choose `current` vs `picogk-experimental`
- support create-box / cylinder / subtract / union through adapter

### Long-term

- hybrid CAD + procedural + implicit + lattice geometry
- printable mesh-first bodies for maker workflows
- optional stronger procedural feature authoring through ACL
