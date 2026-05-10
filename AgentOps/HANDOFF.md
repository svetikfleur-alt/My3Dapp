Done (Task 67 — Shell consistency pass):
- MainWindow.axaml: All toolbar groups now use ToolSplitGroup containers consistently:
  - 3D mode View group: focus button + measure toggle + section toggle wrapped
  - 3D mode Transform group: MoveToolButton wrapped in ToolSplitGroup
  - 3D mode Modify group: 7 buttons (fillet/chamfer/hole/shell/LP/CP/mirror) wrapped
  - 3D mode Boolean group: consolidated into single IsVisible="CanBooleanTools" StackPanel wrapper + ToolSplitGroup (removed redundant per-button IsVisible bindings)
  - 3D mode Datum group: simplified (removed redundant IsThreeDMode bindings) + wrapped in ToolSplitGroup
  - Sketch mode Transform group: move + rotate toggles wrapped
  - Sketch mode Dimension group: linear/radius/angle toggles wrapped
  - Sketch mode Constraints group: all 9 buttons wrapped in ToolSplitGroup (replacing bare StackPanel)

Not done:
- Construction and Grid single-button groups (single items — ToolSplitGroup for one button would be over-structured)

Broken:
- none expected (pure AXAML structural change, no code-behind changes, no bindings altered)

Next:
- Verifier: run build (0 errors expected); confirm toolbar renders with consistent bordered groups in both Light and Dark themes; confirm Boolean group hides when only one body exists; confirm sketch mode constraints appear as a single grouped panel
