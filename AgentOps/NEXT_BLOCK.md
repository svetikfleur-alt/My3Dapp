# Next Block — 2026-05-15

## Selected task: 69 — Sketch preview rendering fixes (Point / Line / Rectangle)

**Why this block:**
Priority ladder item 2 (finish Sketch foundation). The inner Codex agent identified three
runtime rendering gaps in sketch mode. These are the highest-impact unresolved issues:
a working sketch tool that shows no preview feedback feels broken even if the commit path
is correct. Task file: TASKS/69-sketch-preview-rendering-fixes.md.

---

## Builder instructions

### Goal
Make sure the three sketch preview paths render correctly at runtime:
1. Point entity — dot visible in viewport after placement.
2. Line tool rubber-band — line ghost visible while dragging from first click.
3. Rectangle preview — 4-sided ghost visible while dragging from first corner.

### Step 1 — Confirm the preview pipeline is connected
File: `Engine/CadProjectStore.cs`
- `BuildPreviewEntities` returns entities for Line (via `BuildLinePreview`) and Rectangle
  (via `BuildRectanglePreview`). Trace that these entities end up in `session.PreviewEntities`.
- Action handler path: `HandleSketchPreviewMove` (or equivalent) must call
  `session.PreviewEntities = BuildPreviewEntities(session, point)` and then signal a state
  change so the workspace controller rebuilds the render state.

### Step 2 — Confirm rebuildSketches fires on cursor move
File: `AvaloniaApp/Controls/WebViewportHost.cs`
- Find the JS `sketch-preview` message handler (around line 874). This fires from C# when
  the cursor moves in sketch mode. Confirm it calls `rebuildSketches` with updated sketches
  that include the preview sketch.
- If there is a throttle or debounce on cursor messages that drops preview updates, loosen it.

### Step 3 — Confirm Point entity reaches the JS correctly
File: `AvaloniaApp/Controls/ViewportSceneSnapshot.cs` + `StudioWorkspaceController.cs`
- `BuildSketchCurves` maps `CadSketchPoint` → kind "point", 3 floats.
- JS at line 2068 checks `kind === 'point'` and renders via `THREE.Points`.
- If the point is not showing: add a `console.log` temporarily to confirm the curve arrives,
  then check `createSketchPointTexture` returns a non-null texture.

### Step 4 — Confirm Rectangle preview lines are routed
- `BuildRectanglePreview` calls `CreatePlacedRectangleLines` with a **temporary session copy**
  that has `PendingShapeAnchor = anchor`. Verify the returned lines are non-null and non-empty
  when the cursor is not at the anchor (distance check at line 3588 must pass).

### Step 5 — Software viewport fallback
File: `AvaloniaApp/Controls/SoftwareViewportControl.cs`
- `DrawSketchPoint` (around line 1009): confirm the ellipse is drawn correctly for preview
  vs committed points.
- Line preview: check the `IsPreview` branch at line 536 handles the single-line case.

### Acceptance before marking done
- [ ] Line rubber-band visible in both WebView and software fallback.
- [ ] Rectangle ghost visible in both viewports.
- [ ] Point dot visible after commit in both viewports.
- [ ] No regression in Circle/Arc/Polygon preview.
- [ ] Build: 0 errors, 0 warnings.

---

## After this block
Next recommended task: **66 — Sketch solver and definition behavior pass**
(makes constrained sketches hold shape; improves fully-defined / under-defined status accuracy)
