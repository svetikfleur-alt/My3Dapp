# 07. Constraints in sketch dialog

## Goal
Add a visible, working constraint toggle group inside the active sketch session UI for the basic set: Coincident, Horizontal, Vertical, Equal, with persistence in the sketch.

## Scope
- Constraint toolbar inside sketch session card (Sketch panel already has Constraints area placeholder).
- Toggle buttons for: Coincident, Horizontal, Vertical, Equal.
- Selecting two/more sketch entities and clicking a constraint applies it.
- Active constraints are listed under their owning entity (or in the constraints list panel) and can be deleted.
- Constraint glyphs render in viewport on the involved entities (small icons near the entity).
- Persist with sketch on Finish.

## Out of scope
- Tangent, Parallel, Perpendicular, Concentric, Symmetric (next round).
- Driving / driven distinction.
- Auto-inferred constraints during draw.

## Files likely involved
- `Engine/CadProjectStore.cs` — sketch constraints model + apply/remove/serialize.
- `AvaloniaApp/MainWindow.axaml(.cs)` — constraint toolbar in sketch session card.
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — viewport glyph messages.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — selection-aware constraint commands.

## Expected behavior (acceptance)
1. While in a sketch session, the constraint toolbar shows 4 toggle buttons.
2. Selecting 2 endpoints + Coincident merges them; viewport shows a small coincident glyph.
3. Selecting a line + Horizontal aligns it horizontally; glyph shows "H".
4. Selecting a line + Vertical aligns vertically; glyph shows "V".
5. Selecting two lines (or two circles) + Equal makes their lengths/radii equal.
6. Constraints list shows applied constraints; deleting one removes it and the glyph.
7. Finish sketch persists constraints; re-opening the sketch shows them.

## Notes / hints
- Existing engine has Coincident / Horizontal / Vertical / EqualRadius — reuse, do not invent parallel constraint kinds.
- Glyphs are intentionally small: ~10 px. Don't clutter the viewport.

## Verifier checklist
- [ ] Build runner ok.
- [ ] All 4 constraints apply to a simple Rectangle profile.
- [ ] Glyphs visible at involved entity locations.
- [ ] Re-entering sketch preserves constraints.
- [ ] No regression in sketch tool draw flows.
