Done:
- Studio.Dark.axaml: added FeatureDialogButtonPrimary :pointerover/#294155, :pressed/#334f68; FeatureDialogButtonSecondary :pointerover/#2c3748, :pressed/#374455; SketchConstraintToolButton :disabled/opacity 0.45
- Studio.Light.axaml: same hover/press/disabled additions with light-theme colors (#d4e9f8, #c2def4, #edf3fb, #dce8f5, opacity 0.45)
- MainWindow.axaml: Fillet/Chamfer/Hole/Shell/LinearPattern/CircularPattern/Mirror buttons wrapped in ToolSplitGroup border with ToolbarDivider + "Modify" ToolbarGroupLabel; improved tooltips for Sweep, Loft, Fillet, Chamfer, Shell, LP, CP, Mirror; Recipe tab renamed Script (TabItem header + RecipeHeaderTitle TextBlock)
- ExtrudeFeatureDialog.axaml: "Recipe" label → "Expression"; monospace font on expression TextBlock
- RevolveFeatureDialog.axaml: same Recipe→Expression + monospace
- SweepFeatureDialog.axaml: same Recipe→Expression + monospace
- LoftFeatureDialog.axaml: same Recipe→Expression + monospace
- MirrorFeatureDialog.axaml: same Recipe→Expression + monospace

Not done:
- Build verification (dotnet SDK not available on this Linux host; project targets net10.0-windows)
- Live viewport preview bridging for feature dialogs (pre-existing gap, out of scope)

Broken:
- none expected; all x:Name bindings preserved (RecipeTextBlock kept in all dialogs)

Next:
- Verifier: open app, hover Cancel/Apply buttons in any feature dialog — should animate background on hover/click; check Modify group has pill border grouping in toolbar; check tooltips on Modify buttons; check Script tab label in right inspector panel
