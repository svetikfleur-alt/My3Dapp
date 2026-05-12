# Next Builder Block — 2026-05-12

## Task: 66 — Sketch solver and definition behavior pass

**Spec file:** `AgentOps/TASKS/66-sketch-solver-definition-behavior.md`

## Why this block
- All tasks 01-64 are complete.
- Priority order: stabilize build → **finish sketch foundation** → UI polish.
- The sketch constraint system exists but constraints often fail to hold after edits.
  A real CAD tool requires constraints to reliably drive geometry.
- This is the last core sketch-foundation gap before only UI polish remains.

## What to do (scope summary)

1. **Constraint re-assertion after drag**: After a vertex is dragged, re-run the
   lightweight solver so existing constraints (Coincident, Horizontal, Vertical,
   EqualRadius, Fix) re-settle rather than drifting.

2. **Color state accuracy**: Under-defined entities must stay blue and remain
   draggable. Fully-defined entities must turn dark/black and resist drag.
   Check that `SketchDefinitionState` enum values drive the viewport color and
   drag-enable correctly in `WebViewportHost.cs` and the JS side.

3. **Tree / status accuracy**: Ensure `SketchDefinitionHeadline` in
   `StudioShellViewModel.cs` shows the right message:
   - `"Sketch not fully defined."` when DOF > 0
   - `"Sketch fully defined."` when DOF == 0
   - `"Sketch overdefined."` when DOF < 0
   Fix any mismatch between the visual and the solver's DOF count.

4. **Supported constraint enforcement**: Each applied constraint should produce a
   visible geometry adjustment: Coincident snaps endpoints, Horizontal zeroes Y-delta,
   Vertical zeroes X-delta, EqualRadius equalises radii. If a constraint handler is
   stubbed (no-op), implement the minimum geometry correction.

5. **No new solver kernel**: This is a heuristic tightening pass — do NOT introduce
   a full symbolic solver.

## Key files
- `Engine/CadProjectStore.cs` — constraint handlers, DOF calculation.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — `SketchDefinitionHeadline`,
  `SketchDefinitionState`, DOF display.
- `AvaloniaApp/Controls/WebViewportHost.cs` — drag-enable / color messages.
- `AvaloniaApp/Controls/SoftwareViewportControl.cs` — software fallback color.

## Acceptance criteria (from spec)
1. Build: 0 errors, 0 warnings.
2. Simple constrained sketch (e.g. two lines with Horizontal + Coincident) holds
   its shape after dragging a nearby unconstrained vertex.
3. Under-defined geometry is blue and draggable.
4. Fully-defined geometry is dark and stable.
5. Tree status text matches actual DOF count.
6. No regression in sketch preview / commit flow.

## After this block → Task 65 (CAD command dialog standardization)
