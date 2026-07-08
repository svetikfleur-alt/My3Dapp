# Original User Request

## Initial Request — 2026-07-08T15:12:45Z

# Teamwork Project Prompt — P4 Core Exact Modeling

Status: Ready for launch
Goal: Delegate bounded workstreams to teamwork_preview / invoke_subagent and
integrate them into one coherent P4 implementation.

Working directory:

d:/My3DApp/My3DApp

Branch:

feature/cad-mvp-ui-truth-repair

Integrity mode:

STRICT IMPLEMENTATION / NO FAKES

The team must implement real exact CAD functionality.
Compilation-only stubs, successful no-ops, placeholder bodies, primitive
substitution, hidden mesh state, and fake UI success are forbidden.

======================================================================
PROJECT CONTEXT
======================================================================

My3DApp is a local-first, ACL-driven, exact parametric CAD application.

Accepted architecture:

ACL source
-> Lexer / Parser / AST / source map
-> semantic and unit validation
-> exact feature graph
-> IExactCadKernel
-> OCCT exact Solid / B-Rep
-> native OCCT viewport

Product invariants:

- one executable;
- one real MainWindow;
- one native OCCT viewport;
- ACL is the authoritative editable source;
- exact Solid / B-Rep is the internal model;
- no UMXProject or hidden JSON project state;
- no WebView2 / Three.js in the active CAD path;
- mesh may enter or leave, but may not live inside the model;
- UI commands must modify ACL through source-aware patches;
- CadProjectStore must not become the ACL editing path;
- incomplete features must remain disabled or hidden;
- no fake success.

Read before changing code:

- Docs/CAD_CANON.md
- Docs/REPO_MAP.md
- Docs/CURRENT_WORK_PACKET.md
- Docs/INTEGRATION_STATUS.md
- graphify-out/graph.json
- graphify-out/GRAPH_REPORT.md

======================================================================
CURRENT CRITICAL STATE
======================================================================

The repository currently has an interface/implementation mismatch.

`IExactCadKernel` declares:

- CreateWire(double[], bool)
- CreateCircleWire(double)
- CreateFace(IExactBodyHandle)
- CreatePrism(IExactBodyHandle, double, double, double)
- CreateCompound(IReadOnlyList<IExactBodyHandle>)

but `OcctExactKernel` does not yet implement these members.

The current build fails with CS0535.

Before adding further P4 UI or feature work:

1. inspect the current Git diff;
2. identify all `IExactCadKernel` implementations and test doubles;
3. implement the native operations end-to-end;
4. rebuild the C++/CLI bridge;
5. restore a green `dotnet build`;
6. restore a green full `dotnet test`.

Do not continue stacking feature code on a broken kernel boundary.

Forbidden compilation fixes:

- default interface implementations;
- NotSupportedException for required P4 operations;
- null or dummy handles;
- empty placeholder shapes;
- removing interface methods;
- replacing Prism with CreateBox;
- replacing profile Extrude with primitive creation.

======================================================================
GRAPHIFY — REQUIRED OPERATING SYSTEM
======================================================================

Use `graphify-out/graph.json` as the primary repository navigation and
dependency map.

Graphify is not decorative output.

Before any subagent edits files, it must use Graphify to identify:

- owning components;
- direct callers and consumers;
- interface implementations;
- affected tests;
- high-degree hubs;
- UI-to-engine paths;
- managed-to-native bridge paths;
- possible duplicate services;
- possible architecture violations.

Each subagent must receive a bounded Graphify dependency neighborhood.

Do not rescan the whole repository when Graphify already identifies the relevant
files.

Use:

- `graph.json` for deterministic dependency navigation;
- `GRAPH_REPORT.md` for architecture overview;
- `graph.html` only when visual cluster inspection is useful.

Before modifying any of these hubs, inspect all Graphify consumers:

- IExactCadKernel;
- OcctExactKernel;
- My3DApp.Occt bridge;
- AclExactCompiler;
- ExactFeatureGraph;
- ExactModelSession;
- ExactSceneController;
- AclWorkspaceViewModel;
- StudioShellViewModel;
- MainWindow;
- CadProjectStore;
- command catalog;
- feature tree;
- viewport host contracts.

After P4 integration, run `/graphify --update` and verify:

- UI feature commands reach `IAclSourceEditService`;
- `IAclSourceEditService` updates ACL source;
- ACL source reaches one parser/compiler;
- the compiler reaches `ExactFeatureGraph`;
- the graph reaches `IExactCadKernel`;
- UI does not call OCCT implementation internals directly;
- CadProjectStore is absent from the ACL editing path;
- no parallel feature graph exists;
- no primitive-macro substitute exists for Extrude;
- no mesh scene enters the exact model path;
- no new god-class concentration was introduced.

Update only changed facts in:

- Docs/REPO_MAP.md
- Docs/CURRENT_WORK_PACKET.md
- Docs/INTEGRATION_STATUS.md

======================================================================
TEAMWORK STRUCTURE
======================================================================

Launch focused subagents with non-overlapping ownership.

The main agent remains responsible for:

- architecture;
- sequencing;
- integration;
- conflict resolution;
- Git safety;
- runtime verification;
- final P4 quality.

Subagents must not independently redesign shared architecture.

----------------------------------------------------------------------
SUBAGENT A — NATIVE OCCT BRIDGE
----------------------------------------------------------------------

Scope:

- My3DApp.Occt C++/CLI bridge;
- OcctExactKernel;
- native shape lifetime;
- profile, face, prism, compound operations;
- native bridge tests.

Responsibilities:

1. Detect the exact native build environment.
2. Verify MSVC, C++/CLI, Windows SDK, OCCT headers, libraries, and `.vcxproj`
   build support.
3. Install or configure legitimate free build tools where possible.
4. If administrator action is required, report the exact missing component and
   installer command.
5. Extend the narrow bridge with real OCCT operations.

Required native operations:

- create polygonal wire from planar points;
- create circular wire;
- create face from one outer closed wire;
- add zero or more inner closed wires as holes;
- create prism from face and extrusion vector;
- translate shape;
- Boolean cut;
- Boolean fuse;
- create compound;
- expose operation diagnostics;
- preserve correct native ownership and disposal.

Likely OCCT equivalents may include appropriate use of:

- BRepBuilderAPI_MakePolygon;
- BRepBuilderAPI_MakeEdge;
- BRepBuilderAPI_MakeWire;
- BRepBuilderAPI_MakeFace;
- BRepPrimAPI_MakePrism;
- BRepBuilderAPI_Transform;
- BRepAlgoAPI_Cut;
- BRepAlgoAPI_Fuse;
- TopoDS_Compound / BRep_Builder.

Use the correct OCCT APIs after inspecting the installed version.

Do not copy core implementation code blindly from third-party projects.

----------------------------------------------------------------------
SUBAGENT B — EXACT FEATURE GRAPH
----------------------------------------------------------------------

Scope:

- exact feature nodes;
- typed parameter evaluation;
- dependency regeneration;
- topology provenance;
- feature tests.

Implement:

- ExactExtrudeNode;
- ExactHoleNode;
- ExactLinearPatternNode.

ExactExtrudeNode inputs:

- Sketch/Profile reference;
- closed planar profile;
- direction;
- depth;
- operation mode when supported.

ExactExtrudeNode behavior:

Sketch/Profile
-> exact wire
-> exact planar face
-> real OCCT prism
-> exact Solid

It must not convert rectangle dimensions directly into CreateBox.

ExactHoleNode inputs:

- target Body;
- diameter;
- position or sketch point;
- direction;
- explicit depth or through_all.

Behavior:

- create exact cylindrical tool;
- Boolean subtract from the target;
- preserve target and operation provenance.

ExactLinearPatternNode inputs:

- source feature or Body;
- explicit direction vector;
- count;
- spacing;
- result mode.

Behavior:

- create translated exact instances;
- preserve instance identity;
- combine with the target only when required;
- do not blindly copy and fuse the entire final Body for every case.

Every node must contain:

- semantic feature ID;
- ACL source span;
- dependencies;
- typed evaluated parameters;
- build status;
- diagnostics;
- operation provenance;
- exact result handle.

----------------------------------------------------------------------
SUBAGENT C — ACL SOURCE EDITING
----------------------------------------------------------------------

Scope:

- `IAclSourceEditService`;
- AST/source-map-aware patching;
- ACL formatting;
- source-patch tests.

Do not modify CadProjectStore for ACL editing.

Required path:

feature dialog
-> IAclSourceEditService
-> candidate ACL source patch
-> parse and validate
-> transactional regeneration

The source-edit service must understand:

- current AST;
- target `part` block;
- selected feature;
- source spans;
- insertion location;
- replacement of named arguments;
- formatting and indentation;
- unique semantic identifiers;
- parse validation;
- rollback of invalid candidate patches.

Do not expose a naive production API equivalent to:

`AppendCommand(string text)`

A low-level append helper may exist only behind validated AST-aware insertion.

Required ACL semantics should resemble:

let body = extrude(
    profile: base,
    depth: 18mm
);

let hole1 = hole(
    target: body,
    diameter: 22mm,
    position: [30mm, 20mm],
    depth: through_all
);

let holes = linear_pattern(
    source: hole1,
    direction: [1, 0, 0],
    count: 4,
    spacing: 25mm
);

Adapt exact syntax to the existing parser grammar, but retain feature references
and typed parameters.

----------------------------------------------------------------------
SUBAGENT D — UI FEATURE DIALOGS
----------------------------------------------------------------------

Scope:

- Extrude dialog;
- Hole dialog;
- Linear Pattern dialog;
- exact preview;
- Confirm/Cancel behavior;
- UI tests where practical.

Do not modify kernel internals.

Extrude dialog:

- selected Sketch/Profile;
- depth;
- direction or side;
- exact preview;
- validation;
- Confirm;
- Cancel.

Hole dialog:

- target Body;
- position/reference;
- diameter;
- depth or through_all;
- exact preview;
- Confirm;
- Cancel.

Linear Pattern dialog:

- source feature or Body;
- direction;
- count;
- spacing;
- exact preview;
- Confirm;
- Cancel.

Confirm:

- calls `IAclSourceEditService`;
- applies a validated ACL patch;
- triggers transactional rebuild.

Cancel:

- does not change ACL;
- does not change feature history;
- disposes preview geometry;
- leaves the authoritative model unchanged.

Preview:

- must be exact native preview geometry;
- must remain outside feature history;
- must not become the current Body;
- must be completely cleared on cancel or dialog replacement.

----------------------------------------------------------------------
SUBAGENT E — TESTS AND RUNTIME QA
----------------------------------------------------------------------

Scope:

- existing `tests/EngineTests`;
- native bridge verification;
- end-to-end tests;
- runtime verification;
- independent STEP validation.

Integrate tests into the existing test infrastructure.

Use focused classes:

- ExactProfileTests;
- ExactExtrudeTests;
- ExactHoleTests;
- ExactLinearPatternTests;
- ExactRegenerationTests;
- AclSourcePatchTests.

Create a separate native test target only if the C++/CLI bridge cannot be tested
reliably from the existing .NET test project.

======================================================================
REQUIREMENTS
======================================================================

----------------------------------------------------------------------
R1. Native Bridge Extension
----------------------------------------------------------------------

Implement real support for:

- one outer planar wire;
- zero or more inner coplanar closed wires;
- planar face creation;
- prism/extrude;
- translation;
- Boolean cut;
- Boolean fuse;
- compound handling.

Inner wires must:

- be closed;
- be coplanar with the outer wire;
- lie inside the outer profile;
- produce actual holes in the resulting face and prism.

The separate Hole feature remains required even when profile inner wires work.

----------------------------------------------------------------------
R2. ACL Source Editing Service
----------------------------------------------------------------------

Create a focused AST-aware:

- `IAclSourceEditService`;
- `AclSourceEditService`.

CadProjectStore must be bypassed completely for ACL feature editing.

UI edits must patch ACL source.
ACL source remains authoritative.

----------------------------------------------------------------------
R3. Exact Feature Nodes
----------------------------------------------------------------------

Implement:

- `ExactExtrudeNode`;
- `ExactHoleNode`;
- `ExactLinearPatternNode`.

These nodes must delegate to `IExactCadKernel`.

No UI code may mutate native Bodies directly.

----------------------------------------------------------------------
R4. Transactional Regeneration
----------------------------------------------------------------------

Required transaction:

ACL/UI edit
-> create candidate ACL source
-> parse
-> semantic and unit validation
-> build candidate feature graph
-> build candidate exact Bodies
-> if all required operations succeed:
      atomically replace current runtime model
      update viewport
      update feature history
      dispose superseded handles
   else:
      preserve source text
      preserve last valid model
      display STALE — BUILD FAILED
      identify failing feature
      do not partially replace runtime state

No partial model commits.

----------------------------------------------------------------------
R5. Profile-Based Extrude
----------------------------------------------------------------------

Extrude must use a real Sketch/Profile reference.

Required initial supported profiles:

- closed rectangle;
- closed circle;
- one outer closed profile;
- optional inner closed wires.

Do not implement:

`extrude(shape: "rect", width: ..., height: ...)`

as a substitute for a sketch-based feature.

----------------------------------------------------------------------
R6. Hole
----------------------------------------------------------------------

Hole must:

- reference a target Body;
- use diameter;
- use position/reference;
- support finite depth;
- support through_all;
- produce a real Boolean subtraction.

----------------------------------------------------------------------
R7. Linear Pattern
----------------------------------------------------------------------

Linear Pattern must:

- reference a source feature or Body;
- accept direction;
- count;
- spacing;
- preserve semantic instance identity;
- regenerate when source or parameters change.

----------------------------------------------------------------------
R8. Exact Preview
----------------------------------------------------------------------

Feature previews must:

- use exact OCCT geometry;
- remain temporary;
- not enter feature history;
- not modify ACL before Confirm;
- be disposed on Cancel;
- be replaced safely when parameters change.

======================================================================
TEST REQUIREMENTS
======================================================================

Run the full build and complete test suite.

Add tests for:

- rectangular closed wire;
- circular closed wire;
- profile with one inner wire;
- profile with multiple inner wires;
- face creation from valid profile;
- rejection of open wire for face;
- rejection of non-coplanar wires;
- prism dimensions;
- prism volume;
- Extrude depth edit;
- Hole diameter;
- finite-depth Hole;
- through_all Hole;
- Boolean subtraction volume change;
- Linear Pattern count;
- Linear Pattern spacing;
- direction validation;
- invalid profile;
- missing target;
- failed Boolean;
- transactional rollback;
- last-valid Body retention;
- STALE state;
- source-span mapping;
- feature-ID mapping;
- ACL patch insertion;
- ACL patch replacement;
- invalid ACL patch rollback;
- Cancel leaves ACL unchanged;
- preview handle disposal;
- old Body disposal after regeneration;
- disposed-handle rejection;
- compound validation.

Required commands:

- native bridge build;
- `dotnet build`;
- full `dotnet test`;
- focused P4 tests.

======================================================================
RUNTIME VERIFICATION
======================================================================

Launch the real My3DApp application.

Verify:

1. Exactly one window opens.
2. Zero WebView2 processes.
3. No ExactSpineWindow or demo application.
4. Create/open an ACL model.
5. Create a rectangle Sketch/Profile.
6. Extrude it into an exact OCCT Solid.
7. Confirm the feature tree maps to ACL source.
8. Create a Hole.
9. Confirm material is actually removed.
10. Create a Linear Pattern.
11. Edit Extrude depth.
12. Confirm Hole and Pattern regenerate downstream.
13. Edit Hole diameter.
14. Confirm exact geometry changes.
15. Enter an invalid parameter.
16. Confirm STALE — BUILD FAILED.
17. Confirm the last valid exact Body remains visible.
18. Fix the source and rebuild successfully.
19. Cancel each dialog and verify no ACL/model mutation.
20. Save raw ACL.
21. Close and reopen ACL.
22. Confirm the same exact model is reconstructed.
23. Confirm no `.umxproj`, JSON model, or Body snapshot is written.
24. Export STEP.
25. Open STEP independently in FreeCAD or equivalent.
26. Verify it contains a real Solid with correct dimensions and topology.
27. Resize and close the application.
28. Confirm native resources are released without crash.

Capture screenshots or precise evidence for:

- Sketch/Profile;
- Extrude preview;
- final Extrude;
- Hole preview;
- final Hole;
- Pattern preview;
- final Pattern;
- STALE state;
- reopened ACL;
- independent STEP verification.

======================================================================
INTEGRITY RULES
======================================================================

Allowed:

- extending the existing OCCT C++/CLI bridge;
- installing/configuring the legitimate build toolchain;
- using compatible open-source libraries after license verification;
- using prebuilt libraries where architecturally appropriate;
- running external scripts and validation tools;
- reading existing tests;
- using Graphify for repository navigation;
- using FreeCAD or other external validators.

Forbidden:

- fake feature implementations;
- placeholder Bodies;
- null success results;
- successful no-ops;
- primitive substitution for real Extrude;
- hidden mesh-backed model state;
- naive string append as production ACL editing;
- routing ACL editing through CadProjectStore;
- copying third-party core logic without license and architecture review;
- creating a second viewport, engine, parser, feature graph, or source of truth;
- reporting success before real runtime verification.

======================================================================
GIT AND INTEGRATION
======================================================================

Before work:

- inspect current uncommitted diff;
- preserve all user work;
- do not reset or discard unrelated changes.

Subagents must not edit the same shared hubs concurrently.

The main agent must sequence integration for:

- `IExactCadKernel`;
- `AclExactCompiler`;
- `ExactFeatureGraph`;
- `ExactModelSession`;
- `StudioShellViewModel`;
- MainWindow;
- command catalog.

After integration:

1. rebuild native bridge;
2. build .NET solution;
3. run complete tests;
4. launch and verify the real application;
5. update Graphify;
6. inspect final dependency paths;
7. leave final P4 changes uncommitted for user review;
8. show the complete Git diff.

Do not commit P4 until the user explicitly says:

COMMIT

======================================================================
ACCEPTANCE CRITERIA
======================================================================

P4 passes only when:

- the repository builds;
- all existing and new tests pass;
- real profile wires and faces are created;
- inner wires produce holes;
- Extrude is a true profile-based prism;
- Hole performs a real Boolean subtraction;
- Linear Pattern creates exact translated instances;
- UI Confirm patches ACL source;
- Cancel changes nothing;
- CadProjectStore is bypassed;
- regeneration is transactional;
- failed build retains the last valid model and shows STALE;
- exact previews are temporary and correctly disposed;
- STEP export is independently verified;
- Graphify confirms one intended architecture path;
- no fake or parallel implementation exists.

======================================================================
FINAL TEAM HANDOFF
======================================================================

Return:

- subagents launched and their scopes;
- Graphify neighborhoods used;
- root cause of the initial CS0535 break;
- native bridge operations added;
- files changed by each subagent;
- integration conflicts and resolutions;
- final ACL syntax;
- exact feature graph path;
- test results;
- runtime verification;
- STEP validation;
- updated Graphify findings;
- remaining limitations;
- complete Git diff;
- whether the state is safe to commit.

Do not begin Circular Pattern or other MVP+ features until P4 passes.

Launch the teamwork project now.
