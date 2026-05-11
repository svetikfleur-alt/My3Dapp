# 01. Extrude — full operator

## Goal
Promote Extrude from a "happy path" command into a real CAD operator: pick a closed sketch profile, choose distance / direction / operation in the existing `ToolDialogWindow` scaffold, see a live preview in the viewport, commit a feature node to the tree, surface clear errors when the profile is invalid.

## Scope
- Closed-profile detection on the active sketch (single closed loop or multiple disjoint closed loops).
- Distance numeric input via `ToolDialogWindow`-hosted options view (reuse the dialog scaffold landed for Line).
- Direction options: Normal (default), Reverse, Symmetric.
- Operation options: New Body, Join, Cut, Symmetric (the enum already supports them — wire the UI).
- Live in-viewport extrude preview while the dialog is open (semi-transparent).
- On OK: commit a feature node into the feature tree with the chosen parameters and the source sketch reference.
- Invalid input handling: open profile, zero amount, or no sketch selected -> dialog stays open with inline error text, OK disabled.

## Out of scope
- Sweep / Loft / Hole / Boolean ops between bodies (those are separate tasks).
- Multi-profile selection across sketches.
- Asymmetric symmetric (split distances).
- Reordering Extrude in the feature tree timeline.

## Files likely involved
- `Engine/CadProjectStore.cs` — `HandleExtrudeSelectedSketch`, profile validity check, history append.
- `Engine/ProfileBuilder.cs` — closed-loop detection helpers.
- `AvaloniaApp/Dialogs/ExtrudeFeatureDialog.axaml(.cs)` — replace inline window with `ToolDialogWindow`-hosted options view OR refactor to match new pattern.
- `AvaloniaApp/Dialogs/ToolDialogWindow.axaml(.cs)` — confirm OK/Cancel/Esc/Enter contract holds.
- `AvaloniaApp/MainWindow.axaml(.cs)` — Extrude button command wiring.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — extrude action plumbing, preview state.
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — viewport preview message.

## Expected behavior (acceptance)
1. With a closed sketch active, clicking Extrude opens the dialog as a real modal hosted by `ToolDialogWindow`.
2. The dialog shows: distance numeric input, direction radio (Normal / Reverse / Symmetric), operation radio (New Body / Join / Cut / Symmetric), OK + Cancel.
3. While the dialog is open and inputs are valid, the viewport shows a translucent preview of the extruded body that updates on input change.
4. OK commits the feature: feature tree gains a new "Extrude" node referencing the sketch, viewport shows the solid body, dialog closes.
5. Cancel or Esc: no feature added, preview cleared, focus returns to viewport.
6. With an open or self-intersecting profile: dialog opens but shows inline error "Profile is not closed"; OK disabled.
7. With distance 0 or empty: OK disabled, inline error "Distance must be greater than 0".
8. Reverse flips the extrude direction along the plane normal; Symmetric extrudes equally both sides.
9. Cut / Join operate on the topmost overlapping body; New Body creates a fresh node.

## Notes / hints
- Recent `HandleExtrudeSelectedSketch` patch added `Math.Abs(action.Amount)` clamp — reuse, do not regress.
- Use `References/onshape/blocks/04_viewport.jpg` and the Onshape feature dialog UX as a baseline for layout.
- Blueprint principle: viewport real, no SaaS chrome. Keep the dialog dense and technical.

## Verifier checklist
- [ ] Build via runner: `build_request.txt` -> `build_result.json` exit 0.
- [ ] Open closed Rectangle sketch on Top plane, click Extrude, verify dialog scaffold matches ToolDialogWindow style.
- [ ] Distance 10, Normal, New Body -> body appears, feature tree gains "Extrude 1".
- [ ] Re-open with same sketch, Cut, distance 5 -> material removed where bodies overlap.
- [ ] Open profile (single Line) -> dialog disables OK and shows error text.
- [ ] Esc closes dialog with no feature added; preview cleared.
- [ ] No regression in Line preview (chain mode still works).
