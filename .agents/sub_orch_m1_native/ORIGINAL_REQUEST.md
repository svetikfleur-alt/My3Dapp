# Original User Request

## Initial Request — 2026-07-08T17:15:36+02:00

Your role: Native OCCT Bridge Sub-Orchestrator
Your working directory: d:\My3DApp\My3DApp\.agents\sub_orch_m1_native
Your parent: 6e8fee5a-4618-4f3e-a71f-12c73f6b9bb3

Task:
You are the Sub-Orchestrator for Milestone 1: Native OCCT Bridge.
Your scope is to:
1. Initialize your working directory: d:\My3DApp\My3DApp\.agents\sub_orch_m1_native
2. Create BRIEFING.md, progress.md, and SCOPE.md.
3. Coordinate the native bridge compilation, link, and initial tests.
4. Detect the native build environment (MSVC, C++/CLI, Windows SDK, OCCT 7.9).
5. Resolve interface/implementation mismatch: implement the required native operations in My3DApp.Occt C++/CLI bridge (Bridge.cpp, OcctCore.cpp, OcctCore.h) and OcctExactKernel.cs:
   - CreateWire(double[], bool)
   - CreateCircleWire(double)
   - CreateFace(IExactBodyHandle)
   - CreatePrism(IExactBodyHandle, double, double, double)
   - CreateCompound(IReadOnlyList<IExactBodyHandle>)
6. Rebuild the bridge and restore a green C# build (resolve CS0535).
7. Run the existing tests and ensure they pass.
8. Delegate tasks (explorer, worker, reviewer) to subagents.
9. Report status back to parent conversation 6e8fee5a-4618-4f3e-a71f-12c73f6b9bb3.
