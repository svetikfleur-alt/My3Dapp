## 2026-07-08T15:18:23Z
You are the Native Bridge Implementer worker agent.
Your working directory is: d:\My3DApp\My3DApp\.agents\worker_m1_fix
You are armed with the `cad-engineer` domain skill located at `d:\My3DApp\My3DApp\.agents\skills\cad-engineer`.

Your tasks:
1. Modify `d:\My3DApp\My3DApp\My3DApp.Occt\OcctCore.cpp` to resolve the compiler errors. Specifically, add:
   `#include <TopoDS.hxx>`
   at an appropriate location (e.g., after `#include <TopoDS_Shape.hxx>`).
2. Build the C++/CLI bridge project `My3DApp.Occt/My3DApp.Occt.vcxproj` using MSBuild. Note that you may need to set the `MSBuildSDKsPath` environment variable:
   `$env:MSBuildSDKsPath="C:\Program Files\dotnet\sdk\10.0.204\Sdks"`
   and then invoke MSBuild:
   `& "D:\VSBuildTools\MSBuild\Current\Bin\MSBuild.exe" "d:\My3DApp\My3DApp\My3DApp.Occt\My3DApp.Occt.vcxproj" /p:Configuration=Release /p:Platform=x64`
   Verify that it builds successfully and outputs `My3DApp.Occt.dll`.
3. Build the C# project `d:\My3DApp\My3DApp\My3DApp.csproj` using `dotnet build` to ensure the managed compilation errors are completely resolved.
4. Run the unit/integration tests in `tests/EngineTests/OcctExactKernelTests.cs` using `dotnet test` or a relevant command, and verify that they compile and pass successfully.
5. Write your findings, code changes, and build/test verification output to `d:\My3DApp\My3DApp\.agents\worker_m1_fix\handoff.md`.
6. Send a message back to Native OCCT Bridge Sub-Orchestrator (conv ID: 983be25f-4dd8-456a-87cc-dc8ca2d9f443) when you are done.

DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.
