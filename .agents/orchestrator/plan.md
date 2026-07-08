# plan.md — P4 Core Exact Modeling Project Plan

## Architectural Decomposition
The P4 Core Exact Modeling project aims to integrate exact CAD modeling using OpenCASCADE (OCCT) and the ACL language. The layers are structured as:
```
ACL source (or UI operation)
  -> IAclSourceEditService (AST-aware patching)
  -> AclExactCompiler -> ExactFeatureGraph
  -> IExactCadKernel (OcctExactKernel wrapper)
  -> My3DApp.Occt C++/CLI Bridge -> Native OCCT Core
  -> Avalonia Viewport (StudioNativeViewport)
  -> STEP / STL Export
```

## Milestones

### Milestone 1: Native OCCT Bridge and Kernel Compilation
- **Goal**: Rebuild `My3DApp.Occt` C++/CLI bridge, restore green `dotnet build`.
- **Tasks**:
  1. Detect the native compiler and build environment (MSVC, C++/CLI, Windows SDK, OCCT 7.9).
  2. Rebuild the C++/CLI bridge (`My3DApp.Occt.vcxproj`).
  3. Fix the CS0535 compilation error in `OcctExactKernel.cs` by ensuring all `IExactCadKernel` members are correctly exposed and implemented.
  4. Perform initial verification of the bridge methods.
- **Subagent**: Worker A (Armed with `cad-engineer` skill)
- **Status**: PLANNED

### Milestone 2: Exact Feature Graph & Kernel Operations
- **Goal**: Implement exact feature nodes and robust profile geometry.
- **Tasks**:
  1. Implement and verify `ExactExtrudeNode`, `ExactHoleNode`, and `ExactLinearPatternNode` in `Engine/Exact/Graph/ExactFeatureGraph.cs`.
  2. Implement native or managed profile checks: planar points, circular wire, outer face with inner coplanar wires as holes.
  3. Support translation, Boolean subtraction, fusion, and compound handling.
  4. Add unit tests for rectangular/circular closed wires, open/non-coplanar wire rejection, prism volume/dimensions, and pattern count/spacing.
- **Subagent**: Worker B (Armed with `cad-engineer` skill)
- **Status**: PLANNED

### Milestone 3: ACL Source Editing Service
- **Goal**: Implement AST/source-map-aware patching in `AclSourceEditService.cs`.
- **Tasks**:
  1. Complete `IAclSourceEditService` to patch AST nodes, handle indentation, and replace arguments dynamically.
  2. Implement unique semantic identifier mappings to source spans.
  3. Bypassed `CadProjectStore` completely for ACL editing.
  4. Create `AclSourcePatchTests` to verify patch insertion, replacement, formatting, invalid patch rollback, and cancellation.
- **Subagent**: Worker C (Armed with `cad-engineer` skill)
- **Status**: PLANNED

### Milestone 4: UI Feature Dialogs and Exact Preview
- **Goal**: Connect UI feature dialogs with exact native preview and transactional regeneration.
- **Tasks**:
  1. Wire Extrude, Hole, and Linear Pattern dialogs in `MainWindow.axaml.cs` to use `IAclSourceEditService` and transactional regeneration.
  2. Implement exact viewport preview: generate native AIS shapes, keep them outside feature history, dispose on Cancel/update.
  3. Implement transactional regeneration: if build fails, retain last valid Body, show "STALE — BUILD FAILED" status, and block partial replacement.
- **Subagent**: Worker D (Armed with `cad-engineer` skill)
- **Status**: PLANNED

### Milestone 5: E2E and STEP Validation & Forensic Audit
- **Goal**: Ensure 100% test pass rate, verify STEP output, run forensic audit.
- **Tasks**:
  1. Execute all test suites (`dotnet test`).
  2. Verify STEP export output compatibility (FreeCAD validation).
  3. Perform a Forensic Audit check to verify strict implementation integrity (no hardcoded values, dummy stubs).
- **Subagent**: Worker E (Armed with `cad-engineer` skill)
- **Status**: PLANNED

## Verification Strategy
- Direct call of native build and dotnet build within subagent reports.
- Comprehensive unit and E2E test suites in `tests/EngineTests`.
- Manual verification checklist mapping UI elements to source patches.
