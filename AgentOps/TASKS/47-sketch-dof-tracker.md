# 47. Sketch DOF tracker — fully/under/overconstrained status indicator

## Goal
Task 32 explicitly defers: _"Constraint solver / DOF tracking UI (overconstrained detection is a future task)."_ With constraints round 2 (task 32) landing, the sketch has no feedback about whether it is under-, fully-, or over-constrained. Real CAD tools (Onshape, SolidWorks) show this at all times — it drives the user's decision to add or remove constraints. This task adds a lightweight DOF counter and a clear status chip in the sketch session card.

## Scope
**DOF computation (Engine side):**
- Each entity type contributes a known number of free parameters (degrees of freedom):
  - Point: 2 DOF
  - Line: 4 DOF (2 endpoints × 2)
  - Arc: 5 DOF (center x/y + radius + start angle + end angle)
  - Circle: 3 DOF (center x/y + radius)
  - Slot: 5 DOF (2 centers × 2 + radius)
  - Polygon: 4 DOF (center x/y + radius + rotationDeg + sides is fixed/integer = 0)
  - Spline: 2 × control point count DOF
- Each applied constraint removes a known number of DOF:
  - Coincident: −2 (fixes 2 coords)
  - Horizontal: −1
  - Vertical: −1
  - EqualRadius: −1
  - Tangent: −1
  - Parallel: −1
  - Perpendicular: −1
  - Concentric: −2
  - Fix: removes all DOF for the entity (sum of its contribution)
  - Distance/Angle dimension: −1 each
- `CadProjectStore` exposes `ComputeSketchDof(CadSketchSession session) → int`:
  - Returns `totalEntityDof - totalConstraintDof`.
  - Positive = underconstrained (N DOF remaining).
  - Zero = fully constrained.
  - Negative = overconstrained.

**UI side (sketch session card):**
- Below or beside the existing constraint/dimension button row, add a compact read-only status row:
  - `●  Fully constrained` (green accent) — when DOF = 0
  - `○  N DOF remaining` (neutral/muted) — when DOF > 0
  - `⚠  Overconstrained` (warning amber) — when DOF < 0
- The status updates live as the user adds entities and constraints (reactive, bound to `SketchDofStatus` on the ViewModel).
- DOF is recomputed after every entity placement, constraint application, and entity deletion.
- No solver engine is changed — this is purely a counting/display feature.

## Out of scope
- Visual highlighting of which entities are unconstrained (entity coloring by constraint state — future).
- Automatic suggestion of missing constraints.
- Per-entity DOF breakdown display.
- Full geometric constraint solver (none of this task modifies how geometry is actually constrained — it only counts).

## Files likely involved
- `Engine/CadProjectStore.cs` — add `ComputeSketchDof(CadSketchSession session) → int`. Must be called and surfaced after each mutation.
- `Engine/CadModel.cs` — no new types; read `DraftEntities` and `Constraints` + `Dimensions` from `CadSketchSession`.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — add `SketchDofValue` (int) and `SketchDofStatus` (enum: FullyConstrained / Under / Over) reactive properties; update them after store mutations that touch the sketch.
- `AvaloniaApp/MainWindow.axaml` — add the status row to the sketch session card (reuse existing `ConstraintsPanel` area or add below it).
- `AvaloniaApp/Themes/Studio.Dark.axaml`, `Studio.Light.axaml` — add color tokens for the three states (green/neutral/amber; must pass WCAG AA contrast on both themes).

## Expected behavior (acceptance)
1. Open a blank sketch → status shows "4 DOF remaining" (2 free lines = 8 DOF; or for a single free point = "2 DOF remaining").
2. Place a line → DOF count increases by 4.
3. Apply Horizontal constraint → DOF decreases by 1; status updates immediately.
4. Fully constrain a simple shape (e.g., rectangle with 4 distances + fixed corner) → status shows "Fully constrained" in green.
5. Apply one redundant constraint → status shows "Overconstrained" in amber.
6. Remove the redundant constraint → returns to Fully constrained.
7. Status is not shown in 3D mode (hidden when no active sketch session).
8. Build: 0 errors, 0 warnings.

## Notes / hints
- DOF counting for dimensions: each linear/radial/angle dimension is −1 DOF (driving mode only; driven dims are already excluded in task 13's scope).
- For Fix constraint, look up the entity by its referenced ID and sum its base DOF; be careful not to double-count if Fix is applied to an already-coincident endpoint.
- Overconstrained detection at this level is approximate (counting, not geometric redundancy detection) — that's intentional and appropriate for Stage 1/2.
- Keep `ComputeSketchDof` pure (no side effects); call it from the ViewModel after every relevant store operation.
- Reference: Onshape sketch session shows "Fully Defined" or "Under Defined" in the lower-left of the sketch panel.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Blank sketch shows correct initial DOF for opened entities.
- [ ] Adding each entity increases DOF by expected amount.
- [ ] Each constraint applied decreases DOF by expected amount.
- [ ] DOF = 0 shows green "Fully constrained".
- [ ] DOF > 0 shows neutral "N DOF remaining".
- [ ] DOF < 0 shows amber "Overconstrained".
- [ ] Status absent in 3D mode.
- [ ] No regression in constraint application or entity placement.

## Complexity
Low-medium. No solver engine changes; pure counting + reactive UI binding. The DOF table is mechanical. Main risk is keeping the ViewModel update paths complete (adding entity, removing entity, adding constraint, removing dimension, etc.).
