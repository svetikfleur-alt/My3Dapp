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
