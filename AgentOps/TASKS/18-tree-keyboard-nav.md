# 18. Keyboard navigation in feature tree

## Goal
Make the feature tree fully usable from the keyboard: arrow keys to traverse, Enter to expand or activate, Delete to remove, F2 to rename, Tab to move focus to viewport.

## Scope
- Up / Down move selection in the visible tree.
- Left collapses or moves to parent; Right expands or moves to first child.
- Enter activates default action (Edit Sketch / Open Feature dialog / Toggle plane visibility — pick the natural one per node type).
- Delete removes the node (with confirm dialog for features).
- F2 starts inline rename (or opens a small rename dialog).
- Tab leaves the tree and focuses the viewport.
- Focus ring visible and theme-consistent.

## Out of scope
- Drag/drop reorder.
- Multi-select with keyboard (Shift+arrow).

## Files likely involved
- `AvaloniaApp/MainWindow.axaml(.cs)` — TreeView KeyBindings, focus management.
- `AvaloniaApp/ViewModels/FeatureNodeViewModel.cs` — default-action command per node type.
- `AvaloniaApp/Themes/Studio.Light.axaml`, `Studio.Dark.axaml` — focus ring brush.

## Expected behavior (acceptance)
1. Tree gains focus on click or Tab.
2. Arrow keys move selection visually.
3. Enter on a Sketch node opens it for editing; on a Plane node enters Sketch mode after plane selection.
4. Delete on a feature prompts confirm; on cancel keeps state.
5. F2 enters rename mode; Esc cancels rename; Enter commits.
6. Tab returns focus to viewport.

## Notes / hints
- Coordinate with task 05 (feature tree presentation) — same templates.
- Avoid swallowing global shortcuts (e.g. Ctrl+S) when tree has focus.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Arrows / Enter / Delete / F2 all behave.
- [ ] Tab moves to viewport.
- [ ] Focus ring visible in both themes.
- [ ] Global shortcuts still work while tree has focus.
