# 03. Sketch dialog — wire remaining tools

## Goal
Extend the `ToolDialogWindow` scaffold (already built for Line) to Rectangle, Circle, Arc, and Point so every sketch tool has a consistent options dialog with OK / Cancel / Esc / Enter behavior.

## Scope
- Reusable options view per tool, mounted in `ToolDialogWindow`.
- Tool-specific options:
  - Rectangle: Corner-to-corner | Center-corner mode toggle.
  - Circle: Center-radius | 3-point mode toggle; show live radius readout.
  - Arc: Center-start-end | 3-point mode toggle.
  - Point: position numeric input (X, Y) for direct entry.
- Carry options through to the engine commit (modes already supported in `ProfileBuilder` where present; otherwise add minimal support).
- Consistent OK/Cancel + keyboard: Enter commits, Esc cancels, returns focus to viewport.
- Cancel does not leave behind preview state or partial entities.

## Out of scope
- Constraint authoring inside the dialog (separate task).
- Live numeric typing during draw (separate "type-to-dimension" enhancement).

## Files likely involved
- `AvaloniaApp/Dialogs/ToolDialogWindow.axaml(.cs)` — host.
- `AvaloniaApp/Dialogs/LineSketchToolOptionsView.axaml(.cs)` — reference shape.
- New: `RectangleSketchToolOptionsView`, `CircleSketchToolOptionsView`, `ArcSketchToolOptionsView`, `PointSketchToolOptionsView`.
- `AvaloniaApp/MainWindow.axaml.cs` — open-dialog wiring per tool.
- `Engine/CadProjectStore.cs` — accept tool-mode option from dialog.

## Expected behavior (acceptance)
1. Clicking each sketch tool button opens its dialog as a `ToolDialogWindow`.
2. Each dialog has a title matching the tool, a body with tool-specific options, OK + Cancel.
3. Enter commits if state is valid; Esc cancels and returns focus to the viewport.
4. Cancel from any tool clears any pending preview state and pending start anchors.
5. Visual style (density, fonts, button sizing) matches the Line dialog precedent in both Light and Dark themes.
6. Mode toggles (Corner-to-corner vs Center-corner, etc.) immediately change the in-flight preview shape on next pointer move; previously placed entities are unaffected.
7. Point dialog with X/Y numeric input commits a point at the typed coordinates without requiring a viewport click.
8. Switching tools while one dialog is open closes the current dialog cleanly (no overlap, no orphan preview).
9. Tab order inside each dialog is deterministic (mode toggles -> numeric inputs -> OK).
10. Tool dialog state resets to defaults each open; user-tweaked values are not carried across sessions.

## Notes / hints
- Pattern is already proven by Line; copy structure, keep options minimal.
- Do not introduce per-tool window code-behind beyond what Line already does.
- Reference: `References/onshape/blocks/03_left_feature_panel.jpg` for dense, tool-specific input clusters.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Each sketch tool opens its own ToolDialogWindow with the right title.
- [ ] Enter and Esc behave consistently across all 5 tools (Line + 4 new).
- [ ] No layout regression in Line dialog.
- [ ] Cancel does not leave orphan preview entities.
- [ ] Mode toggle on Rectangle / Circle / Arc visibly changes preview shape mid-draw.
- [ ] Point numeric entry commits a point with no viewport click.
- [ ] Tab order verified for at least one dialog.
- [ ] Light and Dark themes both render the new dialogs identically to the Line precedent.
