# 10. Status bar + keyboard shortcuts pass

## Goal
Add a bottom status bar that surfaces mode / active plane / cursor position / selection count / commit hints, and wire a coherent set of keyboard shortcuts across the app.

## Scope
- Bottom status bar control hosted in `MainWindow`.
- Status bar segments: Mode (Modeling / Sketch on Plane / ...), Active Plane (when in sketch), Cursor (X, Y, Z; sketch-local during sketch), Selection count, Commit hint ("Click to set start point" / "Click to commit" / "Esc to cancel" depending on tool state).
- Shortcuts:
  - Esc — cancel current tool / close dialog / exit sketch.
  - Enter — commit current dialog/operation.
  - Space — repeat last command.
  - L — Line, R — Rectangle, C — Circle, A — Arc, P — Point.
  - S — Start Sketch, E — Extrude.
  - Ctrl+S — Save, Ctrl+O — Open, Ctrl+Z — Undo, Ctrl+Y — Redo.
- Conflict-free with text input (shortcut active only when no editable focus).

## Out of scope
- Configurable shortcuts (later).
- Touch gesture mapping.

## Files likely involved
- New: `AvaloniaApp/Controls/StatusBarControl.axaml(.cs)` (or inline in MainWindow.axaml).
- `AvaloniaApp/MainWindow.axaml(.cs)` — KeyBindings collection, status bar markup.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — status segment props.
- `AvaloniaApp/Themes/Studio.Light.axaml`, `Studio.Dark.axaml` — status bar styles.

## Expected behavior (acceptance)
1. Status bar visible at bottom in all modes.
2. Cursor position updates while in sketch.
3. Selection count reflects current selection.
4. Commit hint changes with tool state ("Click to set start" -> "Click to commit").
5. All listed shortcuts work; no double-trigger when typing in text fields.
6. Esc cancels tool or closes top-most dialog.

## Notes / hints
- Reference: `References/zoo/blocks/07_bottom_status_bar.jpg`.
- Status bar height should be small (~22 px) and dense.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Status bar visible Light + Dark.
- [ ] All sketch tool shortcuts trigger correct tool.
- [ ] Ctrl+S triggers Save; Esc cancels Line tool mid-draw.
- [ ] Typing in a numeric input does not re-trigger shortcuts.
