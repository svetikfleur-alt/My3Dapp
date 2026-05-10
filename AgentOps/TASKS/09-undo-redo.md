# 09. Undo / Redo

## Goal
Add a real undo/redo stack on `CadProjectStore` with Ctrl+Z / Ctrl+Y wiring, so most user actions (sketch draw, feature commit, constraint apply, delete) can be reversed.

## Scope
- History stack of action records; each record stores a forward/inverse pair.
- Ctrl+Z = pop and apply inverse; Ctrl+Y (or Ctrl+Shift+Z) = re-apply forward.
- Visible Undo/Redo buttons in toolbar.
- History pruned to last 100 actions to bound memory.
- Clear history on Open / New.
- Sketch session: history scoped per session and merged into project history on Finish.

## Out of scope
- Branched history / non-linear undo.
- Per-feature edit-after-the-fact (parametric editing is a separate large effort).

## Files likely involved
- `Engine/CadProjectStore.cs` — record/apply/inverse plumbing.
- New: `Engine/HistoryStack.cs` — generic stack helper.
- `AvaloniaApp/MainWindow.axaml(.cs)` — toolbar buttons, key bindings.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — CanUndo/CanRedo, commands.

## Expected behavior (acceptance)
1. Draw a Line; Ctrl+Z removes it; Ctrl+Y re-adds it.
2. Commit Extrude; Ctrl+Z removes the feature; Ctrl+Y re-adds it.
3. Apply a constraint inside a sketch; Ctrl+Z removes it; Ctrl+Y re-applies.
4. Delete a feature; Ctrl+Z restores it (including children).
5. Toolbar Undo / Redo buttons enable/disable based on stack state; tooltip shows the action name ("Undo: Add Line").
6. Open project / New project clears both stacks.
7. Memory bounded: pushing past 100 actions evicts the oldest forward record.
8. Doing a fresh action after some undos clears the redo stack.
9. Sketch session has its own scoped stack; on Finish, sketch actions collapse into a single "Edit Sketch" entry on the project stack.
10. Cancel sketch discards the sketch-scoped stack; project stack is untouched.

## Notes / hints
- Keep inverse construction in the engine; UI must not reach into history.
- Many actions are pure state replacements; for those, snapshot / restore can be the inverse.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Draw Line, Ctrl+Z, Ctrl+Y round-trips.
- [ ] Extrude commit, Ctrl+Z, Ctrl+Y round-trips.
- [ ] Constraint apply + Ctrl+Z removes constraint glyph.
- [ ] Delete feature + Ctrl+Z restores feature with children.
- [ ] Buttons reflect stack state; tooltips show action name.
- [ ] New action after undos clears redo stack.
- [ ] Open / New clears both stacks.
- [ ] 100+ actions: memory does not unbounded-grow.
- [ ] Sketch session collapse to single project-level entry on Finish.
