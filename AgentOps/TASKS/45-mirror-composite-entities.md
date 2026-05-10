# 45. Mirror / Transform for composite sketch entities (Slot, Polygon, Spline)

## Goal
Tasks 36 (Mirror) and 35 (2D Transform) both have confirmed stubs for Slot, Polygon, and Spline: `"Mirror: no supported entities (Line, Circle, Arc) found — Slot, Polygon, Spline not yet supported."` These three entity types were added in task 22 and are in daily use; leaving them unmirrored makes the Mirror tool partial. This task adds full support for all three in both Mirror and the 2D translate-transform from task 35.

## Scope
**Mirror (`ReflectEntities` in `CadProjectStore.cs`)**:
- `CadSketchSlot` — has two center points (`X1/Y1`, `X2/Y2`) and `Radius`. Reflect both centers; keep Radius unchanged.
- `CadSketchPolygon` — has `CenterX/Y`, `Radius`, `Sides`, `RotationDeg`. Reflect center; negate the rotational component across the axis (reflect the rotation angle so the polygon orientation mirrors correctly).
- `CadSketchSpline` — has `ControlPointsXY` (flat list: x0,y0,x1,y1,...). Reflect each control point pair.

**2D Transform / Translate (`TranslateSketchEntities` in `CadProjectStore.cs`)**:
- Same three types: add delta-translation for Slot (both centers), Polygon (center), Spline (all control points).
- Confirm the existing handler already processes these or add the missing branches.

**Viewport preview** (`BuildMirrorPreview` in JS / C# side):
- If the active session contains Slot/Polygon/Spline, the mirror preview (ghost geometry during second-point hover) should also render a mirrored preview of those entities. If preview rendering is complex, skip preview for these types and just apply on commit — note the limitation in the status bar message.

The "skipped" count message should be updated to only mention entity types that are genuinely unsupported after this task lands (e.g. exotic future types), not Slot/Polygon/Spline.

## Out of scope
- Rotation transform of sketch entities (not yet in any task).
- Scale transform.
- Mirror of construction entities (follow-up when construction entity refactor lands).

## Files likely involved
- `Engine/CadProjectStore.cs` — `ReflectEntities()` (~line 1290 area): add `CadSketchSlot`, `CadSketchPolygon`, `CadSketchSpline` cases. `TranslateSketchEntities()`: confirm/add the same three cases.
- `Engine/CadModel.cs` — confirm field names on `CadSketchSlot` (`X1`, `Y1`, `X2`, `Y2`, `Radius`), `CadSketchPolygon` (`CenterX`, `CenterY`, `Radius`, `Sides`, `RotationDeg`), `CadSketchSpline` (`ControlPointsXY`).
- `AvaloniaApp/Controls/WebViewportHost.cs` / viewport JS — `BuildMirrorPreview`: optionally extend to render Slot/Polygon/Spline ghost geometry.

## Expected behavior (acceptance)
1. Sketch contains a slot. Activate Mirror; draw axis. Slot is reflected — both center points mirrored, Radius unchanged.
2. Sketch contains a regular hexagon (Polygon). Activate Mirror; draw axis. Mirrored hexagon appears at the reflected center with appropriately mirrored rotation angle.
3. Sketch contains a spline. Activate Mirror; draw axis. All control points reflected; spline shape mirrors correctly.
4. 2D Transform (translate drag): Slot, Polygon, and Spline all move with the drag — no entities left behind at origin.
5. The "N unsupported type(s) skipped" message does not fire for Slot/Polygon/Spline.
6. Mirror of Line/Circle/Arc entities unchanged (no regression).
7. Build: 0 errors, 0 warnings.

## Notes / hints
- **Polygon rotation reflection**: the rotation angle of a polygon across a mirror axis changes sign relative to the axis direction. Simple approximation: `newRotationDeg = 2 * axisAngleDeg - RotationDeg`. `axisAngleDeg = atan2(dy, dx) * 180/π` for the axis vector.
- **Slot**: treat it as a pair of points — reflect each center using the existing `ReflectPoint` helper.
- **Spline**: iterate `ControlPointsXY` two elements at a time; apply `ReflectPoint` to each (x, y) pair; write back.
- The `skipped` count variable already gates the message — just ensure `CadSketchSlot`, `CadSketchPolygon`, `CadSketchSpline` no longer fall into the `is not CadSketchLine and not CadSketchCircle and not CadSketchArc` guard.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Slot mirrors correctly (center positions reflected, radius unchanged).
- [ ] Polygon mirrors correctly (center reflected, rotation adjusted).
- [ ] Spline mirrors correctly (all control points reflected).
- [ ] 2D Transform moves Slot/Polygon/Spline correctly.
- [ ] "unsupported type(s) skipped" message absent for these three types.
- [ ] Existing Line/Arc/Circle mirror unaffected.

## Complexity
Low-medium. Pure geometry/data manipulation — no new UI, no new engine types. Main risk is getting polygon rotation reflection angle-arithmetic right.
