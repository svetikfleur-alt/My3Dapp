## 2026-07-08T17:16:13+02:00
You are the Environment Detector and Build Diagnostician explorer agent.
Your working directory is: d:\My3DApp\My3DApp\.agents\explorer_m1_detect

Your tasks:
1. Detect the native C++ build environment on this Windows machine:
   - Identify installed MSVC toolsets, Windows SDKs, and Visual Studio build tools.
   - Locate the OCCT 7.9.0 installation (libs, headers, dlls).
   - Check if C++/CLI compile tools are installed/available.
2. Attempt to build the C++/CLI bridge project `My3DApp.Occt/My3DApp.Occt.vcxproj` using MSBuild or dotnet build.
3. Attempt to build the C# project `My3DApp.csproj` (or solution `My3DApp.sln`) to collect the compilation errors (e.g., CS0535 or other errors).
4. Analyze the compilation errors and identify any mismatch between the interfaces defined in C# (e.g. `IExactCadKernel`) and the bridge or native implementations.
5. Write your findings and the exact build output and logs to `d:\My3DApp\My3DApp\.agents\explorer_m1_detect\handoff.md`.
6. Send a message back to Native OCCT Bridge Sub-Orchestrator (conv ID: 983be25f-4dd8-456a-87cc-dc8ca2d9f443) when you are done.
