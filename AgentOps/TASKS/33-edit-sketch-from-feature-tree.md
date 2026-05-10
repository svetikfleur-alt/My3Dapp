# 33. Edit sketch from feature tree

## Goal
AUDIT.md flags: "Editing an existing sketch from the feature tree path unclear." Currently there is no way for the user to re-enter a sketch to add/remove/edit entities after it has been finished. Double-clicking or right-clicking a Sketch node in the feature tree should re-enter sketch mode with the existing sketch loaded and fully editable.

## Scope
- **Double-click Sketch node** in the feature tree → re-enters sketch mode on that sketch's plane; all existing sketch entities are rendered and editable; the sketch toolbar and panel appear as they do during initial sketch creation.
- **Right-click Sketch node** → context menu with "Edit Sketch" option (same action as double-click) and "Delete" option (existing behavior, if any).
- While editing: the user can add new entities, delete existing ones, apply/remove constraints and dimensions, then Finish or Cancel as normal.
  - Finish re-commits the sketch; any dependent features (Extrude, Revolve) referencing this sketch are re-compiled.
  - Cancel discards all changes to the sketch and returns the model to its pre-edit state.
- The feature tree marks the node as "editing" while the edit session is open (e.g. bold text or pencil icon).
- Dependent features downstream of the edited sketch are temporarily hidden/suppressed in the viewport while editing (consistent with standard CAD behavior); they reappear on Finish.

## Out of scope
- Editing a Sketch that is currently referenced by a Boolean operation (permit editing, but note recompile may produce a different result).
- Rolling the timeline back to the sketch (full rollback bar is a larger future feature).
- Editing multiple sketches simultaneously.
- Sketch-in-place editing from the right-click context menu on the 3D body face (future; requires face → sketch reverse lookup).

## Files likely involved
- `Engine/CadModel.cs` — add `EditSketch` to `CadCommandActionKind`; add a `SketchId` payload field to `CadCommandAction` (or reuse existing entity-id field).
- `Engine/CadProjectStore.cs` — `HandleEditSketch(action)`: find the sketch by ID, set the project's active sketch to it, switch mode to sketch, re-emit all existing entities to the viewport. On `HandleFinishSketch()`: detect if this was an edit session, re-run compilation of dependent features.
- `AvaloniaApp/ViewModels/FeatureNodeViewModel.cs` — expose `EditSketchCommand` on nodes where `Feature.Kind == CadFeatureKind.Sketch`; bind to double-click gesture and right-click menu item.
- `AvaloniaApp/MainWindow.axaml(.cs)` — wire double-click gesture on feature tree items; add right-click `ContextMenu` with "Edit Sketch" and "Delete" items (or extend existing context menu if one exists).
- `AvaloniaApp/Controls/WebViewportHost.cs` — on entering edit-sketch mode, send a message to the JS layer to render all existing sketch entities as editable (same render path as initial sketch mode); suppress dependent feature meshes.
- `Engine/CadProjectStore.cs` — on Finish: detect if the finished sketch has downstream features (by scanning feature list for features whose `SketchId` matches); re-run `Compile()` for each.

## Expected behavior (acceptance)
1. User finishes a sketch, extrudes it → two nodes in tree: "Sketch 1", "Extrude 1".
2. User double-clicks "Sketch 1" → enters sketch mode; existing lines/circles are visible and highlighted as editable; Extrude 1 body disappears from viewport.
3. User adds a new circle to the sketch → clicks Finish → sketch re-commits; Extrude 1 recompiles with the new profile; body updates in viewport.
4. User double-clicks "Sketch 1" → makes a change → clicks Cancel → sketch reverts to its prior state; Extrude 1 body is unchanged.
5. Right-click "Sketch 1" → context menu shows "Edit Sketch" and "Delete"; clicking "Edit Sketch" enters the same edit mode as double-click.
6. Feature tree node shows a visual indicator ("editing…") while the sketch session is open.
7. Attempting to edit a sketch while another sketch is already being edited: show a non-blocking message "Finish the current sketch first."
8. Build: 0 errors, 0 warnings.

## Notes / hints
- The edit session is fundamentally the same as a new sketch session from the store's perspective — `Project.ActiveSketch` is set to the existing sketch instead of a new one. The key difference: on `HandleFinishSketch`, the store must check for `Project.Scene.Features` that reference this sketch's ID and re-compile them.
- `CadProjectStore.HandleStartSketch` already sets `Project.ActiveSketch`. Look at how it initializes the sketch and replicate for the edit path — with the distinction that existing entities are preserved, not cleared.
- The JS side needs to receive the existing entity list when re-entering a sketch. Either send all entities via a `sketch-load` message, or replay the `PlaceSketchEntity` messages from the stored sketch data. The former is simpler.
- Dependent-feature suppression in viewport: send a `hide-features` message listing body IDs that depend on this sketch; restore them on Finish/Cancel.
- Double-click gesture on Avalonia `TreeView` item: use `DoubleTapped` routed event on the `TreeViewItem` container, or bind a `DoubleTapGesture` on the item template.
- If feature tree right-click context menu doesn't exist yet, create a minimal one (Edit Sketch + Delete) — the full right-click menu spec can be extended in task 05 or task 18.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Double-click Sketch node → enters sketch edit mode; existing entities visible.
- [ ] Dependent bodies suppressed in viewport during edit.
- [ ] Add entity → Finish → dependent feature recompiles correctly.
- [ ] Cancel → sketch and dependent features unchanged.
- [ ] Right-click menu: "Edit Sketch" and "Delete" items present.
- [ ] "Edit Sketch" from right-click → same edit session as double-click.
- [ ] Feature tree node visually indicates edit-in-progress.
- [ ] Attempting to edit while another sketch is open: non-blocking message.
- [ ] One commit.

## Complexity
Moderate. Store logic is the main work (detecting edit-vs-new session, re-compiling downstream). Viewport replay of existing entities is a minor JS message. Avalonia gesture binding is straightforward. Sonnet is appropriate; bump to Opus if downstream recompile reveals cascading dependency issues.
