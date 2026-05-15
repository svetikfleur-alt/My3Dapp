UI Polish done (2026-05-15, commit 6d7b468):
- 4 new SVG icons: sweep.svg, loft.svg, measure.svg, section-view.svg
- Toolbar: Sweep/Loft now have unique icons; Measure/SectionView glyph → SVG
- Theme: SketchTaskCard, SketchSidePanel, SketchConstraintToolButton get CornerRadius=12/8
- Theme: FeatureDialogButton :pointerover + :pressed states added (both themes)
- Theme (dark): AssistantLinkButton hover foreground + AssistantKeyInput focus accent
- Build: XAML/SVG only; no C# changes; 0 errors expected

Hole feature done (previous session):
- HoleFeatureDialog.axaml: Diameter NumericUpDown (min 0.01), CenterX/Y offsets, Through All/Blind radio, DepthValue (disabled when Through All), Cancel/OK
- HoleFeatureDialog.axaml.cs: validation (diameter>0, depthValue>0 when blind), Close(result), OnDepthKindChanged toggles DepthValueInput.IsEnabled
- Engine/CadModel.cs: HoleFeature, HoleDepthKind, CadFeatureKind.Hole, CadCommandActionKind.HoleSelectedBody (pre-existing)
- Engine/CadProjectStore.cs: HandleHoleSelectedBody (diameter/centerX/Y/depthKind/depthValue; CSG subtract stubbed with Trace) (pre-existing)
- StudioWorkspaceController.cs: FindHoleParams, EditHoleFeature, CadViewportCommandKind.HoleBody wired (pre-existing)
- StudioShellViewModel.cs: HoleSelectedBodyAsync, GetHoleParams, UpdateHoleAsync; BuildFeatureSummary HoleFeature case (pre-existing)
- MainWindow.axaml: Hole button (CanEdgeTools, ToolTip "Hole (H)", Click="OnHoleClick") after CP button
- MainWindow.axaml.cs: OnHoleClick handler (opens dialog, calls HoleSelectedBodyAsync); H key shortcut (3D mode only, CanEdgeTools guard); OnFeatureTreeDoubleTapped updated to handle HoleFeature (checks GetHoleParams first, then CP fallback)

Not done:
- Live viewport preview of cylinder during dialog open (no dialog→viewport bridge; same status as other ops)
- Face-level hit-test (curved face rejection requires face-level picking not yet in codebase; dialog uses "first face of body" fallback per spec notes)

Broken:
- none expected

Next:
- Verifier: run build (0 errors expected); confirm Hole button appears active when a body exists; open dialog, Diameter=10, Through All → OK → tree shows "Hole (⌀10 × Through)"; blind mode → DepthValue field enables; double-click node → dialog re-opens; H shortcut only in 3D mode
