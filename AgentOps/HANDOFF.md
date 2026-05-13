Done:
- BooleanBodyDialog.axaml: converted from raw brush/border bindings to shared FeatureDialog style classes
  (FeatureDialogSectionCard, FeatureDialogFieldLabel, FeatureDialogInput, FeatureDialogCheckBox);
  added FEATURE tag badge in header; added ShowInTaskbar="False"; renamed OK → Apply
- ExtrudeFeatureDialog.axaml: removed Recipe row (Profile/Plane/Status only); Height 470→430; RecipeTextBlock gone
- ExtrudeFeatureDialog.axaml.cs: removed RecipeTextBlock.Text assignments and BuildRecipeText() method
- RevolveFeatureDialog.axaml: removed Recipe row; Height 410→370; RecipeTextBlock gone
- RevolveFeatureDialog.axaml.cs: removed RecipeTextBlock.Text assignment and BuildRecipeText() method
- SweepFeatureDialog.axaml: removed Recipe row; Height 430→395
- LoftFeatureDialog.axaml: removed Recipe row; Height 440→405
- MirrorFeatureDialog.axaml: removed Recipe row; Height 340→300

Not done:
- dotnet not installed in agent environment; build could not be run locally.
  All changes are AXAML style-class substitutions and dead code removal with no
  structural logic changes — backend bindings are untouched.

Broken:
- none expected

Next:
- Verifier: run dotnet build; confirm 0 errors 0 warnings
- Open BooleanBodyDialog → should show FEATURE badge, consistent field labels, radio buttons styled, Apply button
- Open ExtrudeFeatureDialog → profile card should show Profile/Plane/Status only (no Recipe row)
- Same check for Revolve, Sweep, Loft, Mirror dialogs
- Task 66 (sketch solver behavior pass) is next in queue
