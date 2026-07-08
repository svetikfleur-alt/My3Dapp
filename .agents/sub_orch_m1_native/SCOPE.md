# Scope: Milestone 1: Native OCCT Bridge

## Architecture
- `My3DApp.Occt` is a C++/CLI bridge compiled with MSVC. It wraps native OCCT operations.
- `FormaCore.Engine.Exact` (in C#) uses `My3DApp.Occt.OcctKernel` through `OcctExactKernel` to perform exact geometry modeling.
- The interface contracts are defined in C# `IExactCadKernel` and implemented by `OcctExactKernel`.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Environment Detection & Initial Diagnostics | Detect MSVC, C++/CLI, WinSDK, OCCT 7.9; run diagnostic build of bridge & C# to get errors | none | DONE |
| 2 | Resolve Interface Mismatch & Build Errors | Implement / fix native operations in C++/CLI bridge & C# to resolve CS0535 and compile successfully | M1 | IN_PROGRESS |
| 3 | Rebuild & Verify Tests | Build the entire solution and run `tests/EngineTests/OcctExactKernelTests.cs` | M2 | PLANNED |
| 4 | Forensic Audit | Verify authenticity of implementations, check for cheating, and confirm no hardcoded results | M3 | PLANNED |

## Interface Contracts
### `My3DApp.Occt` (C++/CLI) ↔ `FormaCore.Engine.Exact` (C#)
- `OcctKernel` provides:
  - `CreateWire(array<double>^ points2d, bool closed)`
  - `CreateCircleWire(double radius)`
  - `CreateFace(OcctBody^ wire)`
  - `CreatePrism(OcctBody^ face, double dx, double dy, double dz)`
  - `CreateCompound(array<OcctBody^>^ bodies)`
