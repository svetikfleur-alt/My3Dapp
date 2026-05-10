# 13. Sketch dimensions

## Goal
Add basic dimensioning inside the sketch session: linear distance between two points or along a line, and radial / diameter on a circle / arc. Dimensions render in the viewport, persist with the sketch, and edit numerically through the dialog scaffold.

## Scope
- Dimension tool button in sketch toolbar.
- Linear distance: pick 2 points OR a single line -> dimension placed; opens a small numeric dialog (reuse ToolDialogWindow) for the value.
- Radius dimension: pick a circle / arc -> dimension placed; numeric dialog for radius.
- Diameter dimension: same as radius but presented as diameter on circles.
- Dimensions render with leader line + value text, in viewport.
- Edit by clicking the dimension value -> opens dialog to change.
- Persist with sketch; round-trip through Save/Open.
- Driving (default): change dimension value re-solves involved entities.

## Out of scope
- Angle dimension (next round).
- Driven (read-only) dimensions.
- Dimension styles / format presets.

## Files likely involved
- `Engine/CadProjectStore.cs` — sketch dimension model + solver hook.
- `Engine/ProfileBuilder.cs` — re-solve helper.
- `AvaloniaApp/MainWindow.axaml(.cs)` — Dimension button.
- `AvaloniaApp/Dialogs/NumericFeatureDialog.axaml(.cs)` — reuse for value entry.
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — viewport dimension overlay messages.

## Expected behavior (acceptance)
1. Dimension button toggles a single-shot dimension tool.
2. Picking two points produces a linear dimension with leader + value.
3. Picking a circle prompts radius/diameter; selection persists with chosen mode.
4. Editing a value re-solves geometry (e.g., changing rectangle width moves the right edge).
5. Dimensions visually distinct from sketch geometry (lighter weight + label).
6. Save / Open round-trip preserves dimensions and their values.

## Notes / hints
- Solver is the hard part — start with directly applied rigid transforms for a single dimension; full constraint solver can come later.
- Reference: `References/onshape/blocks/04_viewport.jpg` for dimension styling.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Linear, Radius, Diameter dimensions all placeable.
- [ ] Editing a value updates geometry.
- [ ] Round-trip Save/Open preserves dimensions.
- [ ] No regression in plain sketch tools.
