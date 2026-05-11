# 34. Sketch entity right-click context menu

## Goal
AUDIT.md flags: "No right-click context menu on sketch entities (delete/edit)." Currently there is no way to right-click a sketch entity (line, circle, arc, rectangle) to delete it or edit its values during a sketch session. This is the most basic sketch-mode interaction gap — every CAD tool has it.

## Scope
Right-click on any sketch entity while in an active sketch session → context menu with these items:

- **Delete** — removes the entity (and any constraints that reference it) from the sketch immediately.
- **Edit value…** — opens an inline numeric entry (or the existing `ToolDialogWindow` options view for that entity type) pre-filled with the entity's current defining values. For example:
  - Line: length and angle, or explicit endpoint coordinates.
  - Circle: radius.
  - Arc: radius and sweep angle.
  - Rectangle: width and height.
  - Point: X/Y position.
- **Set as Construction** — toggles the entity between real and construction-line mode (drawn dashed; not used as a profile boundary for extrude). If construction mode is not yet modeled in the data, add a `IsConstruction` bool flag to the sketch entity record and render it differently (dashed/lighter color) in the JS layer.

Context menu behavior:
- Appears at the cursor position on right-click.
- Dismissed by pressing Esc, clicking elsewhere, or selecting an item.
- Only shown when hovering/clicking a sketch entity (not blank space).
- Does not interfere with the existing 3D-mode right-click context menu (Delete, Focus Camera) — sketch mode and 3D mode have separate context menus.

The right-click hit-test: the JS viewport layer already receives `pointermove` to drive sketch preview. Extend it to handle `contextmenu` events — compute the nearest sketch entity within a click radius (use the same snap-radius logic as task 27), send a `sketch-entity-context` message to C# with the entity ID and screen coordinates. C# opens the appropriate context menu at those coordinates.

## Out of scope
- Right-click on 3D bodies/faces in sketch mode (only sketch entities in scope).
- Multi-select context menu (right-click on a multi-selection) — future task.
- Rename entity (entities are not named in v1).
- Context menu on constraints (constraint deletion uses the constraint list panel from task 07).

## Files likely involved
- `AvaloniaApp/Controls/WebViewportHost.cs` — intercept `contextmenu` JS event on the WebView2; compute nearest sketch entity; post `sketch-entity-context` message to C# with `{ entityId, screenX, screenY }`. Add a `HandleSketchEntityContextMenu(entityId, x, y)` C# method receiving this message.
- `AvaloniaApp/MainWindow.axaml(.cs)` (or the sketch session view) — show an Avalonia `ContextMenu` positioned at the given screen coordinates with the three items; wire Delete and Edit value commands.
- `Engine/CadProjectStore.cs` — `HandleDeleteSketchEntity(entityId)`: remove the entity by ID from `Project.ActiveSketch.Entities`; remove any constraints referencing it. `HandleEditSketchEntityValue` already exists — confirm it can be driven by entity ID alone.
- `Engine/CadModel.cs` — add `IsConstruction` bool property to `CadSketchEntity` (or whichever class represents sketch entities). Add `SetSketchEntityConstruction` to `CadCommandActionKind`.
- `AvaloniaApp/Controls/WebViewportHost.cs` (JS side) — render entities with `IsConstruction == true` as dashed/lighter; wire a `set-construction` message handler.

## Expected behavior (acceptance)
1. In sketch mode, right-click on a line → context menu appears with Delete / Edit value… / Set as Construction.
2. **Delete**: entity disappears from sketch; any attached constraints are also removed; sketch re-renders without the entity.
3. **Edit value… (Line)**: dialog/popover opens pre-filled with current length and angle; editing and confirming updates the line in-place.
4. **Edit value… (Circle)**: opens with current radius; editing updates the circle.
5. **Set as Construction**: entity renders dashed/lighter; toggling again restores it to solid.
6. Construction-mode entities are excluded from the closed-profile check during Extrude (they don't count as profile boundaries).
7. Right-click on blank viewport space in sketch mode → no context menu (or the existing 3D context menu does not appear either).
8. Right-click in 3D mode → existing 3D context menu (Delete, Focus Camera) is unaffected.
9. Build: 0 errors, 0 warnings.

## Notes / hints
- Avalonia `ContextMenu.Open()` can be triggered programmatically at a screen position — use this from the C# `HandleSketchEntityContextMenu` handler rather than relying on XAML-bound right-click, since the right-click originates in the WebView2 layer.
- WebView2 intercepts `contextmenu` on the HTML canvas: call `event.preventDefault()` to suppress the browser's default context menu, then post the C# message via `WebView.CoreWebView2.PostWebMessageAsString`.
- `HandleEditSketchEntityValue` already exists in `CadProjectStore` — check its current call signature; it may already accept an entity ID. If so, hook "Edit value…" directly to it.
- `IsConstruction` rendering: simply change the Three.js `LineBasicMaterial` `color` and optionally set `lineDashOffset`/`dashSize` (requires `LineDashedMaterial`). Pass `isConstruction` in the entity data already sent to JS.
- For hit-testing in JS: maintain a list of sketch entity screen bounding segments; on `contextmenu`, find the closest entity within 8 px screen-space (same as snap radius). If no entity found within radius, do not post the message.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Right-click on line in sketch mode → 3-item context menu appears.
- [ ] Delete removes the entity and its constraints; no orphan constraint glyphs.
- [ ] Edit value (Line): dialog pre-filled; editing updates entity.
- [ ] Edit value (Circle): dialog pre-filled; editing updates circle radius.
- [ ] Set as Construction: entity renders dashed; excluded from Extrude profile check.
- [ ] Toggle construction off: entity returns to solid rendering.
- [ ] Right-click on blank space: no context menu.
- [ ] Right-click in 3D mode: existing 3D context menu unaffected.
- [ ] One commit.

## Complexity
Moderate. Main complexity is cross-layer communication (JS contextmenu event → C# message → Avalonia ContextMenu shown programmatically). The actual Delete/Edit/Construction logic is straightforward once the message arrives. Sonnet is fine.
