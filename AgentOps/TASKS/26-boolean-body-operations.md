# 26. Boolean body operations (Union / Subtract / Intersect)

## Goal
Wire the Boolean body operations into the UI and engine. `Core/Operations.cs` already defines `UnionNode` / `SubtractNode`; `Core/Solids.cs` has `BooleanOperation` and `BooleanSolid`; `StudioShellViewModel` even has a `CanBooleanTools` property — but it is hardcoded to `false` (line ~1294) and there are no `CadCommandActionKind` entries for any Boolean action. Unlock and wire Union, Subtract, and Intersect so the user can combine two solid bodies in the viewport.

## Scope
- Add `CadCommandActionKind` entries: `BooleanUnion`, `BooleanSubtract`, `BooleanIntersect`.
- Add handler methods in `CadProjectStore.Apply()` for each; each picks the two most-recently selected (or currently selected) bodies and applies the operation via the geometry backend.
- Expose three toolbar buttons (or a "Boolean" dropdown) in the top panel under the 3D group.
- Enable `CanBooleanTools` when ≥ 2 solid bodies are in the scene; disable (grey out) otherwise.
- Each result lands in the feature tree as a parametric Boolean node with the two source body references and the operation type. Source bodies become children (hidden from top-level tree, still browseable).
- Dialog: a compact two-field picker — "Body A" and "Body B" dropdowns (pre-filled from selection) + operation radio (Union / Subtract / Intersect) + Preview + OK / Cancel.
- Committed Boolean is editable: double-click in tree reopens dialog with current params.

## Out of scope
- n-ary (more than 2 bodies) Boolean — keep it binary for now.
- Boolean with sketch entities or reference planes.
- Loft / Sweep (unrelated).
- Assembly-level operations.

## Files likely involved
- `Engine/CadModel.cs` — add 3 entries to `CadCommandActionKind` enum.
- `Engine/CadProjectStore.cs` — `HandleBooleanUnion`, `HandleBooleanSubtract`, `HandleBooleanIntersect` methods; wire into `Apply()` switch.
- `Core/Operations.cs`, `Core/Solids.cs` — `UnionNode`, `SubtractNode`, `BooleanSolid` already present; check if `IntersectNode` needs to be added.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — flip `CanBooleanTools` logic to enable when ≥ 2 bodies exist; add three `ICommand` properties.
- `AvaloniaApp/Dialogs/BooleanBodyDialog.axaml(.cs)` — new dialog (model `ToolDialogWindow` pattern).
- `AvaloniaApp/MainWindow.axaml(.cs)` — hook buttons; bind `IsEnabled` to `CanBooleanTools`.

## Expected behavior (acceptance)
1. Scene with a single body: Boolean buttons are greyed out.
2. Scene with ≥ 2 bodies: Boolean buttons become active.
3. Click Union → dialog opens, Body A and Body B pre-filled from current selection (or first two bodies if nothing selected); click OK → merged body appears, both source bodies hidden, feature tree shows "Union (Box + Sphere)" node.
4. Subtract removes Body B volume from Body A; Intersect keeps only the overlapping volume.
5. Double-click the feature tree node → dialog reopens with current params; changing Body A/B or operation re-applies.
6. Cancel leaves scene unchanged.
7. Undo (when task 09 lands) should reverse the Boolean.
8. Build returns 0 errors, 0 warnings.

## Notes / hints
- `CanBooleanTools` setter is `private`; it is updated inside the method that refreshes tool availability (near line 1294 in `StudioShellViewModel`). Find the method that recomputes toolbar state and hook `CanBooleanTools = Project.Scene.Bodies.Count >= 2` there.
- `Core/Solids.cs` `BooleanOperation` only has `Union` and `Subtract` — add `Intersect` to the enum and `IntersectNode` to `Core/Operations.cs` if not present.
- The geometry backend interface (`Engine/IGeometryBackend.cs`) has a `Union(object a, object b)` method; check if Subtract and Intersect are defined and add stubs if not.
- If the backend cannot produce real Intersect geometry, stub it as a TODO with a status-bar message; tree node still records the intent.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Single-body scene: Boolean buttons disabled.
- [ ] Two-body scene: Boolean buttons enabled.
- [ ] Union of two overlapping boxes → single merged mesh in viewport.
- [ ] Subtract: smaller box cut from larger box → notch visible.
- [ ] Intersect: overlap region visible (or stubbed with message if geometry deferred).
- [ ] Feature tree node shows operation type and source body names.
- [ ] Double-click reopens dialog; change operation, OK → result updates.
- [ ] Cancel is clean (no orphan nodes).
- [ ] One commit.

## Complexity
Moderate-high. Three new action kinds, engine plumbing, dialog, ViewModel gate logic, geometry backend extension. Sonnet should manage; bump to Opus if the geometry backend intersection proves complex.
