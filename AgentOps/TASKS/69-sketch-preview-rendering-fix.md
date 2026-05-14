# 69. Sketch preview rendering diagnostic & fix

## Goal
A sub-agent queue (`AgentOps/AgentOps/TASK_QUEUE.md`) recorded three unresolved
sketch rendering bugs: "Fix Point rendering", "Fix Line preview",
"Fix Rectangle preview." These have no build evidence, no DONE.md entry, and no
outer-queue task. Verify each path works; fix any broken path.

## Scope

### Point rendering
- `CadSketchPoint` entities should appear as visible filled dots after being
  committed in the Point sketch tool.
- Path: `BuildPointPoints` → `ViewportRenderSketchCurve.Kind = "point"` →
  JS `createSketchPointTexture` → `THREE.Points` with size 4px.
- Confirm the buffer has ≥ 1 vertex and the JS `length < 3` guard does not
  accidentally discard a single-vertex buffer.

### Line preview
- After the first click in the Line tool, a rubber-band line should track the
  cursor to its current position until the second click.
- Path: `BuildLinePreview` → `session.PreviewEntities` → serialized as a
  `ViewportRenderSketch { IsPreview = true }` → sent to JS → rendered as dashed
  or semi-transparent polyline.
- Confirm `PendingLineStart` is set before the preview fires.

### Rectangle preview
- After the first click (anchor) in the Rectangle tool, a ghost rectangle with
  four sides should track the cursor.
- Path: `BuildRectanglePreview` → `CreatePlacedRectangleLines` returning four
  `CadSketchLine` entities → serialized as `IsPreview = true` sketch → JS renders.
- Confirm `PendingShapeAnchor` is set after the anchor click.

## Out of scope
- New sketch tools.
- Constraint or solver behavior.
- Any 3D feature code.

## Likely files
- `Engine/CadProjectStore.cs` — `BuildLinePreview`, `BuildRectanglePreview`,
  `BuildPreviewEntities`, anchor-setting in `HandleSketchPlacement`
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — `BuildSketchCurves`,
  `BuildPointPoints`, preview sketch serialization
- `AvaloniaApp/Controls/WebViewportHost.cs` — JS point/line rendering branches

## Acceptance
1. Build: 0 errors, 0 warnings.
2. Line tool: rubber-band line visible from first click to cursor.
3. Rectangle tool: ghost rectangle visible from anchor to cursor.
4. Point tool: committed point appears as visible dot.
5. No regression in committed circle/arc/polygon sketch rendering.

## Verifier checklist
- [ ] Build passes cleanly.
- [ ] Line preview renders during draw.
- [ ] Rectangle preview renders during draw.
- [ ] Committed sketch point is visible in viewport.
- [ ] No other sketch type regresses.

## Complexity
Low–Medium — read + verify existing path; small targeted fix if broken.
