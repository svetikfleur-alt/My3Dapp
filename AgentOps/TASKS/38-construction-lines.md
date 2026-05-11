# 38. Construction lines / centerline toggle

## Goal
Add a Construction Line toggle to the sketch toolbar so that any sketch entity (line, circle, arc) can be marked as a construction entity — displayed as dashed/lighter in the viewport, excluded from profile building, but participating in constraints and dimensions.

## Scope
- **Toggle button** in the sketch toolbar ("Construction" or a dashed-line icon), active when in sketch mode.
- **Per-entity flag**: `CadSketchEntity` gains a `bool IsConstruction` property (default false).
- When toggled on, newly placed entities are created as construction entities.
- **Retroactive toggle**: selecting an existing entity and clicking Construction flips its flag.
- **Profile exclusion**: `ProfileBuilder.cs` skips construction entities when building closed profiles for Extrude / Revolve.
- **Viewport rendering**: construction entities drawn dashed/lighter — pass `isConstruction: true` in the sketch-entity JSON payload; JS renders them with a dashed stroke.
- **Persistence**: `IsConstruction` serializes with the entity (when task 08 is done).

## Out of scope
- Infinite construction lines (xline) — out of MVP.
- Centerline inferring axis of revolution automatically.
- Separate "Centerline" tool — this flag covers that use case.

## Files likely involved
- `Engine/CadModel.cs` — add `IsConstruction` to `CadSketchEntity` base (or as interface).
- `Engine/ProfileBuilder.cs` — filter out construction entities in profile loop.
- `Engine/CadProjectStore.cs` — read/apply the flag when placing entities; handle toggle action (new `CadCommandActionKind.ToggleConstructionMode`).
- `AvaloniaApp/MainWindow.axaml(.cs)` — Construction toggle button in sketch toolbar.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — `IsConstructionModeActive` bool property.
- `AvaloniaApp/Controls/WebViewportHost.cs` — pass `isConstruction` field in entity render payloads.
- Viewport JS — dashed stroke / lighter color for construction entities.

## Expected behavior (acceptance)
1. Construction toggle button visible in sketch mode toolbar.
2. Placing a line with Construction active → line renders dashed in viewport.
3. Construction line does not form part of a closed profile: Extrude from a rectangle with a construction centerline still extrudes only the rectangle.
4. Selecting a committed entity and pressing Construction button flips it (solid → dashed or vice-versa); viewport updates.
5. Build: 0 errors, 0 warnings.

## Notes / hints
- Minimal JS change: in the `draw-sketch-entity` handler, check the `isConstruction` field; if true, set `line.material.dashSize / gapSize` and use `LineDashedMaterial`.
- Construction entities should still participate in constraints — don't exclude them from `session.Entities`, only from profile building.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Toggle button present in sketch mode.
- [ ] Construction line renders dashed.
- [ ] Extrude with mixed normal + construction entities: only normal entities form the profile.
- [ ] Retroactive flip works (committed entity → toggle → re-renders dashed).

## Complexity
Small-medium — isolated flag + render change; no complex geometry.
