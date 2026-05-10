# 36. Sketch Mirror — proper implementation

## Goal
Replace the stub `CreatePlacedMirror` in `CadProjectStore.cs` (line ~1183) with a real Mirror tool that lets the user select entities and specify an arbitrary axis line, mirroring the selected geometry across it.

## Scope
- **Axis selection**: first click defines axis start point, second click defines axis end (a full line through both points).
- **Entity selection**: mirror is applied to all committed entities in the current sketch session (not just DraftEntities). Optionally keep originals (default: keep both sides, matching standard CAD behavior).
- **Remove vertical-axis stub**: replace the current `mirrorX` shortcut with the generalized reflection formula across an arbitrary line.
- **Preview**: live preview during axis placement showing mirrored outlines.
- **Commit**: on second click, mirrored entities are added to `session.Entities` as real `CadSketchEntity` objects.
- Support at minimum: Line, Circle, Arc, Rectangle (decomposed into lines).

## Out of scope
- Mirror across an existing sketch entity (select-an-entity-as-axis) — defer.
- Mirror in 3D / body mirror (handled elsewhere).
- Slot, Polygon, Spline mirror — stub them to a warning message for now.

## Files likely involved
- `Engine/CadProjectStore.cs` — replace `CreatePlacedMirror`; add two-click axis state to `CadSketchSession`.
- `Engine/CadModel.cs` — `CadSketchSession` may need `MirrorAnchor` state field.
- `AvaloniaApp/Controls/WebViewportHost.cs` — ensure preview message reaches JS during axis placement.

## Expected behavior (acceptance)
1. Activate Mirror tool; click once → axis start locked; click again → mirrored copies appear and commit.
2. Mirrored Lines, Circles, Arcs visible in viewport alongside originals.
3. Removing the axis-point stub: clicking mirror no longer silently mirrors only draft entities across `x=clicked.X`.
4. Preview shows mirror during second-point hover.
5. Build: 0 errors, 0 warnings.

## Notes / hints
- Reflection of point P across a line through A→B: standard linear algebra formula.
  `P' = P + 2 * ((A-P) - ((A-P)·d̂)d̂)` where `d̂ = normalize(B-A)`.
- Arc reflection: swap StartAngle / EndAngle and negate Y-component of the arc's center relative to axis; keep Radius.
- The current stub lives at `CadProjectStore.cs ~1181`. The TODO comment explicitly flags the limitation.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Two-click Mirror: Line reflected correctly across a diagonal axis.
- [ ] Circle reflected correctly.
- [ ] Arc reflected (start/end angles swapped appropriately).
- [ ] Old vertical-axis shortcut behavior absent.
- [ ] Preview visible during second-point hover.

## Complexity
Medium — geometry math is straightforward; main effort is sketch session state + preview messaging.
