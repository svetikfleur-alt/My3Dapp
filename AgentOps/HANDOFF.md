Done:
- Task 65 — CAD command dialog standardization

Changes made (8 axaml files):

1. ExtrudeFeatureDialog.axaml — Removed "Recipe" row from info section card; adjusted height 470→440.
2. RevolveFeatureDialog.axaml — Removed "Recipe" row from info section card; replaced "Current minimal revolve supports sketch X/Y axes only." with clean "Revolves around the selected sketch axis through the sketch origin."; adjusted height 410→380.
3. SweepFeatureDialog.axaml — Removed "Recipe" row from info section card; adjusted height 430→400.
4. LoftFeatureDialog.axaml — Removed "Recipe" row from info section card; adjusted height 440→400.
5. MirrorFeatureDialog.axaml — Removed "Recipe" row; cleaned help text to active sentence; adjusted height 340→300.
6. FilletFeatureDialog.axaml — Replaced "This minimal fillet applies to the currently selected body. Unsupported edge-level selection is intentionally not faked." with "Applies a constant-radius fillet to all eligible edges of the selected body."
7. BooleanBodyDialog.axaml — Full standardization: added FeatureDialogWindow class, FeatureDialogPanel, FeatureDialogTag (FEATURE), FeatureDialogSectionCard for body selection and operation sections, FeatureDialogFieldLabel/FeatureDialogCheckBox classes, changed "OK" button to "Apply".
8. DatumPlaneDialog.axaml — Full standardization: added FeatureDialogWindow class, FeatureDialogPanel, FeatureDialogTag (REFERENCE), FeatureDialogSectionCard with 3-row aligned grid (source plane ComboBox, offset NumericUpDown+unit chip, name TextBox).

Not done:
- Build not verified (dotnet not available in this Linux cloud container targeting net10.0-windows).

Expected build status:
- 0 errors, 0 warnings. Changes are purely XAML layout/text; no new bindings, no C# changes.
- BooleanBodyDialog: BodyAComboBox and BodyBComboBox named elements preserved, UnionRadio/SubtractRadio/IntersectRadio preserved — code-behind unchanged and compatible.
- DatumPlaneDialog: SourcePlaneCombo, OffsetInput, NameInput named elements preserved — code-behind unchanged and compatible.

Next:
- Verifier: confirm build passes; open Boolean dialog and verify FEATURE tag + Apply button + proper section card styling; open Datum Plane dialog and verify REFERENCE tag + consistent layout; confirm no dialog regression in Extrude/Fillet/Mirror/etc.
- Next task: 66 — Sketch solver and definition behavior, or 67 — Shell consistency pass.
