# 55. Polyline sketch tool

## Goal
AUDIT.md lists "No Polyline (multi-segment connected lines)" as a sketch gap. A polyline lets the user click successive points that are auto-joined at shared endpoints — a fundamental productivity shortcut over drawing Line segments one at a time. This is distinct from Spline (smooth curve through points); Polyline is straight segments connected at vertices.

## Scope
- New `CadSketchPolyline` entity (or represent as an ordered list of `CadSketchLine` segments sharing endpoints — preferred: the latter avoids a new type and reuses existing constraint/dim infrastructure).
- **Placement flow**: Click = add vertex; double-click or press Enter/Esc = close/finish the polyline. Optional: clicking back on the start point closes the loop.
- **Preview**: rubber-band line from last committed vertex to cursor position (same pattern as Line tool preview).
- **Coincident snap**: each new vertex automatically snaps coincident to the previous vertex endpoint — no manual coincident constraint needed.
- Toolbar button (polyline icon, shortcut `Y` or `P` if not taken in sketch mode).
- ISketchToolOptionsView for Polyline (can be minimal — just "Close loop" checkbox).
- Works with existing Trim, Mirror, Transform, and Construction-line toggle.

## Out of scope
- Smooth / Bezier vertex types within the polyline.
- Editing mid-vertices after commit (the segments are individual `CadSketchLine` entities, so existing per-line edit applies).
- Polyline as a distinct serialization type — represent as linked Lines.

## Files likely involved
- `Engine/CadProjectStore.cs` — placement handler for polyline tool (multi-click state machine).
- `Engine/CadModel.cs` / sketch entity types — possibly add `CadSketchPolyline` wrapper or just produce linked `CadSketchLine` instances.
- `AvaloniaApp/MainWindow.axaml(.cs)` — toolbar button wiring.
- `AvaloniaApp/ViewModels/MainWindowViewModel.cs` — `IsPolylineToolActive`, `OnPolylineClick`.
- Viewport JS — rubber-band preview, vertex dots, close-loop detection.
- `AvaloniaApp/Views/SketchTools/PolylineSketchToolOptionsView.axaml(.cs)` — minimal options view.

## Expected behavior (acceptance)
1. Click Polyline button (or press shortcut) → enters polyline mode.
2. Click 3+ points → segments drawn between each consecutive pair.
3. Double-click (or Enter) finishes; segments committed as individual linked `CadSketchLine` entities sharing endpoint coordinates.
4. Clicking back on start point closes the loop with a final segment.
5. Preview rubber-band follows cursor between clicks.
6. Esc cancels and discards uncommitted segments.
7. Resulting line segments work with Trim, Mirror, Transform, Construction toggle.
8. Build: 0 errors, 0 warnings.

## Notes / hints
- State machine: `PolylineVertices: List<(double X, double Y)>` on the sketch session. Each click appends; Enter/double-click flushes all segments.
- Shared endpoint coincidence: when flushing, create `CadSketchLine` for each consecutive pair. The coincident constraint may be implicit (same coordinate) rather than an explicit constraint entity — acceptable for this pass.
- Toolbar shortcut: check existing shortcuts in `Blueprint/14_COMMANDS_AND_SHORTCUTS.md` before assigning.

## Verifier checklist
- [ ] Can draw a triangle (3 clicks + Enter) as a polyline; 3 line segments appear.
- [ ] Double-click or Enter commits; Esc cancels.
- [ ] Close-loop click creates final closing segment.
- [ ] Rubber-band preview visible between clicks.
- [ ] Resulting segments are individually selectable/trimmable.
- [ ] Build clean.

## Complexity
Medium — multi-click state machine is the main challenge; entity representation reuses existing Lines.
