Done:
- HoleFeatureDialog.axaml: Diameter NumericUpDown (min 0.01), CenterX/Y offsets, Through All/Blind radio, DepthValue (disabled when Through All), Cancel/OK
- HoleFeatureDialog.axaml.cs: validation (diameter>0, depthValue>0 when blind), Close(result), OnDepthKindChanged toggles DepthValueInput.IsEnabled
- Engine/CadModel.cs: HoleFeature, HoleDepthKind, CadFeatureKind.Hole, CadCommandActionKind.HoleSelectedBody (pre-existing)
- Engine/CadProjectStore.cs: HandleHoleSelectedBody (diameter/centerX/Y/depthKind/depthValue; CSG subtract stubbed with Trace) (pre-existing)
- StudioWorkspaceController.cs: FindHoleParams, EditHoleFeature, CadViewportCommandKind.HoleBody wired (pre-existing)
- StudioShellViewModel.cs: HoleSelectedBodyAsync, GetHoleParams, UpdateHoleAsync; BuildFeatureSummary HoleFeature case (pre-existing)
- MainWindow.axaml: Hole button (CanEdgeTools, ToolTip "Hole (H)", Click="OnHoleClick") after CP button
- MainWindow.axaml.cs: OnHoleClick handler (opens dialog, calls HoleSelectedBodyAsync); H key shortcut (3D mode only, CanEdgeTools guard); OnFeatureTreeDoubleTapped updated to handle HoleFeature

Inspector fixes (2026-05-16):
- MainWindow.axaml.cs:1030 — Added IsSketchMode == false guard to M key (move tool). Was triggering 3D move gizmo in sketch mode.
- MainWindow.axaml.cs:1868 — Changed new Hole dialog default from "Blind" to "ThroughAll". CAD convention: ThroughAll is the expected default.

Not done:
- Live viewport preview of cylinder during dialog open (no dialog→viewport bridge; same status as other ops)
- Face-level hit-test (curved face rejection requires face-level picking not yet in codebase; dialog uses "first face of body" fallback per spec notes)

Open issues (see VERIFICATION.md for details):
- Sweep feature is "extrude with twist" architecturally, not a true path sweep
- Sweep (toolbar) uses extrude.svg icon; Loft uses revolve.svg — no dedicated icons exist
- Sketch plane selection waiting state has no visual feedback for the user

Next:
- Builder: create sweep.svg and loft.svg icons, or assign placeholder icons from existing set
- Builder: add status bar text "Click a reference plane to place sketch" during IsSelectingSketchPlane
- Verifier (next run): confirm build with dotnet if available; test Hole dialog ThroughAll default; confirm M key blocked in sketch mode

2026-05-16 publishable MVP pass:
- Synced local repo to GitHub main after clean branch merge.
- Fixed launch/build mismatch caused by nested temp snapshot XAML files being included by Avalonia globs.
- Added maker-product layer on top of the existing CAD shell:
  - `AvaloniaApp/Services/MakerTemplateLibrary.cs`
  - bottom workspace tabs for Part Studio / Sketch / Templates / Prepare / AI Chat / +
  - template workspace overlay and left-side starter template list
  - prepare workspace overlay with export entry point
  - root README and PartLibrary contributor docs
- Build currently passes: `dotnet build .\\My3DApp.csproj -c Debug -p:StudioUiHost=Avalonia`
- Remaining biggest gaps:
  - template generation is command-driven and some parts are simplified MVP solids
  - feature tree does not yet show template/export sections as first-class nodes
  - prepare workspace is useful but not yet a full manufacturing review panel

2026-05-16 MVP publishable proposal pass (this session):
- Fixed 2 build errors that were blocking compilation:
  - StudioShellViewModel.cs:211 — isAddButton → IsAddButton (C# record constructor param case)
  - StudioShellViewModel.cs:393 — added NotificationSeverity.Success value to enum
- Build now: 0 errors, 0 warnings
- Rewrote README.md to publishable open-source standard:
  - feature table, workflow guide, keyboard shortcuts, contribution guide, limitations, roadmap
- Rewrote PartLibrary/README.md with template table and contributor workflow
- Rewrote PartLibrary/TEMPLATE_GUIDE.md with full step-by-step contributor instructions
- Updated all 6 template.md files with full parameter tables and example presets
- All 6 templates remain in MakerTemplateLibrary.cs: mounting-plate, washer, spacer, l-bracket, fan-adapter, cable-clip
- Committed as: mvp: publishable AI maker CAD studio proposal
