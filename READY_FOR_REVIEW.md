# MVP End-to-End Stabilization Report

## Status
partially ready — core pipeline confirmed headless; on-screen UI verification requires a Windows machine.

## What changed
The existing pipeline (ACL → parser → StudioWorkspaceController → CadProjectStore → compile → viewport payload → STL/OBJ export) was already real and working; no rewrite was needed. This task closed the two actual gaps:

1. **"Create your first part" empty state.** The viewport now shows a starter card whenever the CAD workspace has zero bodies (and no sketch/plane-selection is active), with two real actions:
   - **Create sample box** — runs the existing `AddPrimitive` command path.
   - **Run sample ACL** — executes a small ACL script (variables → sketch → rectangle → extrude → fillet) through the existing parser/executor, reporting per-step results in the assistant log.
   Both are classified **Working** (verified headless). The card also points to Templates (real) and states honestly that the AI assistant works with local commands whether or not a provider key is configured.
2. **Headless end-to-end smoke test.** `Tools/AclSmokeRunner` was extended from parse-only to a full 4-stage runner: ACL expansion, command parsing, sample-box execution (body in compile result + scene tree + feature history + real viewport mesh + undo + STL export), and sample-ACL execution (sketch/extrude/fillet + OBJ export). Exit code is non-zero on any failure.

The smoke test caught one real bug during development: the original sample ACL used `fillet` on a primitive box, which the engine honestly rejects (fillet is limited to sketch-extruded bodies). The sample script was changed to a sketch→extrude→fillet flow, which exercises more of the pipeline and passes.

## Files changed
- `AvaloniaApp/Services/CadScriptLibrary.cs` — added `SampleAclScript` constant (single source for UI + smoke test)
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — added `IsCreateFirstPartVisible`, `RunSampleAclAsync`, and change notifications at the existing refresh points
- `AvaloniaApp/MainWindow.axaml` — added the empty-state card to the viewport overlay area
- `AvaloniaApp/MainWindow.axaml.cs` — added `OnCreateSampleBoxClick` / `OnRunSampleAclClick` handlers (mirroring the existing primitive-menu handler)
- `Tools/AclSmokeRunner/Program.cs` — rewritten from reflection-based parse check to typed 4-stage end-to-end smoke test
- `READY_FOR_REVIEW.md` — this report

## Build evidence
Command: `dotnet build -p:EnableWindowsTargeting=true` (.NET SDK 10.0, Linux; `EnableWindowsTargeting` needed only because CI ran on Linux)
Result: **Build succeeded. 0 Warning(s), 0 Error(s).**

## Test evidence
No `dotnet test` projects exist in this solution (the only projects are the app and the smoke runner). The smoke runner is the test:

Command: `dotnet run` in `Tools/AclSmokeRunner` (after building the app)
Result: **All smoke stages passed** — 16/16 checks PASS across 4 stages (ACL expansion, parsing of the 70-step controller-box-kit template, sample-box end-to-end, sample-ACL end-to-end).

## Runtime evidence
Command: `dotnet run --project My3DApp.csproj`
Result: **BLOCKED by environment.** The app targets `net10.0-windows` with `UseWindowsForms` (the viewport is a WinForms `NativeControlHost` hosting WebView2 with a software-render fallback). On Linux the launch fails with: `Framework 'Microsoft.WindowsDesktop.App', version '10.0.0' — No frameworks were found.` This runtime exists only on Windows. There is no code-level launch blocker known; the same binary set is what a Windows machine would run.

## MVP flow evidence
- **Launch** — BLOCKED (Linux CI; Windows-only desktop runtime, see above)
- **Workspace** — REPORTED (workspace XAML/VM wiring inspected and builds; visual layout not verifiable headless)
- **Create sample body** — CONFIRMED (headless: box primitive and ACL sketch→extrude→fillet both produce one compiled body via the real command pipeline)
- **Viewport update** — CONFIRMED at the data level (viewport render payload contains the body with 24 positions / 36 indices of real mesh data; the same payload the WebView/software viewport draws). On-screen rendering: REPORTED.
- **Tree/history update** — CONFIRMED (scene tree contains the body; feature history entries present; undo stack recorded the mutation)
- **Export/Prepare** — CONFIRMED (STL export contains real `facet normal`/`vertex` triangles; OBJ contains `v `/`f ` lines; export dialog surfaces errors instead of hiding them; Prepare warnings honestly state "STEP and slicer handoff are not in this MVP yet")

## What is real now
- ACL expansion (variables, `${…}` interpolation, templates) through `CadScriptLibrary` — verified
- Command parsing of full sequences (70-step template parses cleanly) — verified
- Body creation via primitive command and via ACL sketch/extrude/fillet — verified
- Compile result, scene tree, feature history, and undo all reflect created bodies — verified
- Viewport render payload carries real tessellated mesh data — verified
- STL and OBJ export produce real geometry files — verified
- Empty-state actions are wired to these verified paths (no dead buttons added)
- Honest failure surfacing: fillet-on-primitive is rejected with a clear message rather than faked

## What is still incomplete
- On-screen verification (window opens, empty-state card visible, viewport draws, dialogs open) was not possible on Linux; needs one manual pass on Windows.
- The empty-state card follows the same overlay pattern as the existing sketch-prompt cards; if WebView2's native z-order covers overlays on some machines, it shares that pre-existing behavior.
- The `controller-box-kit` template parses fully but includes `fillet` after `create box` steps, which the engine rejects at execution time (same limitation the smoke test caught). Pre-existing; not fixed here to stay in scope.
- STEP export / slicer handoff do not exist (and the Prepare workspace already says so honestly).

## Known risks
- Empty-state overlay visibility on Windows depends on native-control z-ordering (shared with existing overlays).
- `SampleAclScript` must stay in sync with parser syntax; the smoke runner will fail loudly if it drifts.

## Next recommended task
Run the app once on Windows and confirm visually: empty-state card appears on a fresh document, both actions create a visible body, and the card disappears afterwards.
