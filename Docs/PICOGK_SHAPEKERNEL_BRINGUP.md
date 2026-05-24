# PicoGK / ShapeKernel Bring-Up Report

## Summary

This pass focused on using the **already present local sources** rather than downloading anything new. The key result is:

- Zoo is locally available as a reference tree.
- ShapeKernel is locally available only as an **incomplete snapshot**.
- PicoGK is locally available as a zip archive and also already present in the app via the cached `PicoGK` NuGet package.
- A **minimal experimental PicoGK adapter** and a **standalone bring-up demo** were added safely without replacing the current CAD kernel.

## Local Sources Inspected

- `ThirdParty/KittyCAD.Zoo/upstream/modeling-app-1.2.6`
- `ThirdParty/LEAP71.ShapeKernel/upstream/LEAP71_ShapeKernel-main`
- `PicoGK-main.zip`

## Source Inventory

### Zoo v1.2.6

- Contains UI/workspace source, KCL samples, e2e tests, and Rust/TypeScript modules.
- Best use here: architecture and workflow reference.
- No direct code import was attempted in this pass.

### ShapeKernel local snapshot

- Contains:
  - `Examples/`
  - empty folder structure under `ShapeKernel/`
- Missing:
  - actual `ShapeKernel` library `.cs` sources
  - any `.csproj` for the kernel itself
- Example files reference namespaces like `ShapeKernel.BaseBox`, `ShapeKernel.LocalFrame`, and `Sh.PreviewVoxels(...)`, but those implementations are not present locally.

### PicoGK local source

- `PicoGK.csproj` found inside the local archive.
- Core local source files inspected:
  - `PicoGK_Utils.cs`
  - `PicoGK_Mesh.cs`
  - `PicoGK_MeshIo.cs`
  - `PicoGK_Voxels.cs`
  - `PicoGK_Library.cs`
- Core operations confirmed:
  - mesh primitive generation
  - mesh -> voxels
  - voxel booleans
  - voxels -> mesh
  - STL save

## Build Commands Attempted

### 1. Local PicoGK source build

```powershell
dotnet build .\temp_picogk_source\PicoGK-main\PicoGK.csproj --nologo -v minimal
```

Result:
- failed with `NU1301`
- restore tried to access `https://api.nuget.org/v3/index.json`

### 2. Reduced-scope restore attempt

```powershell
dotnet build .\temp_picogk_source\PicoGK-main\PicoGK.csproj --nologo -v minimal -p:RestoreIgnoreFailedSources=true
```

Result:
- failed with `NU1101`
- missing packages/framework refs:
  - `Microsoft.SourceLink.GitHub`
  - `Microsoft.NETCore.App.Ref`
  - `Microsoft.WindowsDesktop.App.Ref`
  - `Microsoft.AspNetCore.App.Ref`

Interpretation:
- the upstream PicoGK source tree is **not buildable as-is in this offline environment**
- this is primarily an environment / restore problem, not the first compile error in PicoGK itself

### 3. Experimental adapter prototype

```powershell
dotnet build .\Tools\PicoGKBringup\PicoGKBringup.csproj --nologo -v minimal
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Run-PicoGKBringup.ps1
```

Result:
- build succeeded
- runtime succeeded through `PicoGK.Library.Go(...)`
- created:
  - `temp_verify_picogk\picogk_enclosure_demo.stl`
- mesh stats printed by the tool:
  - `Vertices: 661416`
  - `Triangles: 220472`

## What Worked

- Local source discovery
- License inspection
- API inspection from local source and cached NuGet XML docs
- Installed PicoGK package reality check: the cached `2.0.0` package exposes `mshCreateCube`, but not the `mshCreateCylinder` helper seen in the older local source snapshot
- Installed PicoGK helper reality check: even `mshCreateCube` assumes the heavier `Library.Go(...)` startup path, so the safe bring-up route here is `current mesh builder -> PicoGK voxels -> PicoGK booleans/export`
- Experimental adapter implementation against the already cached `PicoGK 2.0.0` package
- Runnable standalone bring-up console app:
  - `Tools/PicoGKBringup`
- Successful STL output from the bring-up console app

## What Failed

- Full local upstream PicoGK source build in-place
- Full ShapeKernel bring-up, because the local ShapeKernel snapshot is incomplete

## Minimal Usable Subset

The minimal practical subset used successfully is:

1. `PicoGK.Library.Go(...)`
2. current app mesh primitives (`MeshBuilder.CreateBox`, `MeshBuilder.CreateCylinder`)
3. `PicoGK.Mesh`
4. `PicoGK.Voxels(mesh)`
5. `voxBoolAdd / voxBoolSubtract / voxBoolIntersect`
6. `mshAsMesh()`
7. `SaveToStlFile(...)`

That subset is now wrapped by:

- `Engine/GeometryKernelAdapter.cs`
- `Engine/CurrentGeometryKernelAdapter.cs`
- `Engine/PicoGkExperimentalGeometryKernelAdapter.cs`

## Extracted Kernel Principles

- Separate **recipe/command intent** from actual geometry backend.
- Use a strict adapter boundary so UI and feature workflows do not know PicoGK internals.
- Treat voxel/implicit backends as **experimental kernel plugins**, not direct app truth.
- Convert backend-native geometry into viewport/export meshes at a controlled boundary.

## License Findings

- PicoGK local source: Apache-2.0
- ShapeKernel local root: Apache-2.0
- Zoo upstream tree already documented locally as MIT-compatible reference
- ShapeKernel example files include permissive example headers (CC0 in inspected example)

No GPL/copyleft risk was identified in the inspected local materials.

## Integration Options

### Safe now

- Keep current kernel as default
- Use PicoGK behind an experimental adapter
- Use explicit PicoGK sessions for prototype / batch geometry work
- Prototype boolean-heavy or implicit-friendly part generation through ACL / recipe execution later

### Unsafe right now

- replacing the current kernel wholesale
- importing ShapeKernel blindly despite missing source
- binding UI directly to PicoGK object types

## Recommended Next Steps

1. Keep the adapter layer experimental.
2. Add one recipe-executor experiment that targets `IGeometryKernelAdapter`.
3. Use PicoGK first for:
   - robust subtractive templates
   - shell/offset experiments
   - mesh-first printable bodies
4. Treat ShapeKernel as **recoverable only after a complete local source drop exists**.
