# Next Block — 2026-05-14

## Chosen task: 69 — Sketch preview rendering diagnostic & fix

### Why this block

Priority rule 2 is "finish Sketch foundation." The nested sub-agent queue
(`AgentOps/AgentOps/TASK_QUEUE.md`) recorded three unresolved sketch rendering
bugs before stopping:

1. **Fix Point rendering** — `CadSketchPoint` entities may not appear in the
   viewport during or after sketch sessions.
2. **Fix Line preview** — the rubber-band preview line (from first click to
   cursor) may not render while drawing a Line.
3. **Fix Rectangle preview** — the ghost rectangle may not render while dragging
   from the anchor.

These are foundational sketch usability issues. A broken line or rectangle
preview means the user cannot see what they are placing. Task 66 (sketch solver)
sits on top of a working sketch draw loop and should not be attempted until
rendering is confirmed correct.

Tasks 65/67/68 are polish and come last per priority order.

### Scope

- Verify `BuildPointPoints` → JS `createSketchPointTexture` path is correct and
  that a committed `CadSketchPoint` appears as a visible dot in the viewport.
- Verify `BuildLinePreview` → that `PreviewEntities` containing a `CadSketchLine`
  renders a rubber-band line while the cursor moves after the first click.
- Verify `BuildRectanglePreview` → that the preview renders as four edge segments
  while dragging from anchor to cursor.
- If any path is broken, fix the minimum code needed to make it work.
- Do NOT alter any feature or constraint code.

### Likely files

- `Engine/CadProjectStore.cs` — `BuildLinePreview`, `BuildRectanglePreview`,
  `BuildPreviewEntities`
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — `BuildSketchCurves`,
  `BuildPointPoints`, serialization of preview sketches to viewport render model
- `AvaloniaApp/Controls/WebViewportHost.cs` — JS `createSketchPointTexture`,
  point/polyline rendering branches

### Acceptance

1. Build: 0 errors, 0 warnings.
2. Clicking once in Line tool shows a rubber-band line tracking the cursor.
3. Clicking once in Rectangle tool shows a ghost rectangle tracking the cursor.
4. A committed sketch point (Point tool) appears as a visible filled dot.
5. No regression in committed line/rect/circle sketch rendering.

### After this block

Proceed to task 66 — Sketch solver and definition behavior pass.
