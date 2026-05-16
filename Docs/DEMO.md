# My3DApp Demo Guide

This guide walks through the core workflows of My3DApp to help new users get oriented quickly.

---

## First launch

1. Open My3DApp — you land in the **Part Studio** workspace.
2. The feature tree on the left shows three reference planes (Top, Front, Right) ready to sketch on.
3. The 3D viewport fills the center; the AI assistant dock is on the right.

---

## Workflow 1 — Template to printable part (2 minutes)

1. Click the **Templates** tab in the workspace tab bar.
2. Select **Box Enclosure** from the template list.
3. Adjust parameters:
   - Inner Width: `80 mm`
   - Inner Height: `40 mm`
   - Inner Depth: `60 mm`
   - Wall Thickness: `2 mm`
4. Click **Apply Template** — a shelled box appears in the viewport.
5. Switch to the **Prepare** tab.
6. Click **Export** (or Ctrl+E) → choose **STL** → save.
7. Open the STL in your slicer and print.

---

## Workflow 2 — Sketch and extrude

1. In the Part Studio, click **Sketch** on the right side of the toolbar (or press **S**).
2. Click the **Top** reference plane to select it as the sketch base.
3. Draw a rectangle: click two corners in the viewport.
4. Click **Finish Sketch** (✓) or press **F**.
5. The sketch appears in the feature tree.
6. Click **Extrude** in the feature tools panel — set depth to `15 mm`.
7. A solid box appears. Add a fillet with the **Fillet** tool.

---

## Workflow 3 — AI assistant command

1. Open the **AI Chat** tab or expand the assistant dock on the right.
2. Type: `make a bracket 60mm wide, 80mm tall, 3mm thick with 4mm holes`
3. The assistant expands this to a CAD command and executes it.
4. The part appears in the viewport. Inspect parameters in the feature tree.

---

## Workflow 4 — Save and recover

1. Build a part, then press **Ctrl+S**.
2. Choose a save location — the file is saved as `part.my3dapp`.
3. Close the app without saving again (to simulate a crash).
4. Reopen My3DApp — a dialog asks if you want to restore the autosave.
5. Click **Restore** — your last work is recovered.

---

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+N | New document |
| Ctrl+O | Open project |
| Ctrl+S | Save project |
| Ctrl+Shift+S | Save As |
| Ctrl+E | Export |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+D | Duplicate selected body |
| Ctrl+K | Command palette |
| S | Start sketch (3D mode) |
| F | Finish sketch (sketch mode) |
| Esc | Cancel current operation |
| M | Move tool |
| Delete | Delete selected |

---

## Tips

- **Unsaved changes** are shown as a `•` dot next to the document name in the title bar.
- **Autosave** fires every 2 minutes when the document is dirty — you can always recover from a crash.
- **Recent files** are accessible via the ▾ button next to the Open toolbar button.
- **Template parameters** match real-world units (mm). Defaults are chosen for FDM printing.
- **Export scope**: in the Prepare tab you can export all visible bodies or just the selected body.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| Viewport shows blank | Wait a moment for WebView2 to initialize; if it persists, restart the app |
| Template does not appear after Apply | Check that at least one parameter is set and rebuild the feature tree |
| Autosave dialog does not appear | Check `%APPDATA%\My3DApp\Autosave\` — the file may have been cleaned up |
| Export produces empty STL | Ensure there is at least one solid body in the scene (not just a sketch) |
