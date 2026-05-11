Done (task 66 — Sketch solver + definition behavior, 2026-05-11):
- Engine/CadProjectStore.cs: Added Tangent case to ApplyConstraintToGeometry switch — calls new
  ReapplyTangentConstraint(entities) which re-asserts line-arc tangency (snaps arc endpoint to
  nearest line endpoint) and arc-arc tangency (snaps arc1 start to arc2 end) on every solver pass.
- Engine/CadProjectStore.cs: Increased SolveSketchSession default iterations 4→8 for better
  convergence on constraint chains involving multiple linked entities.
- Engine/CadProjectStore.cs: Fixed HandleEditSketchEntityValue committed-sketch path — changed
  sketch.Dimensions = InferDimensions(...) and sketch.Constraints = InferConstraints(...) to
  MergeSketchDimensions/MergeSketchConstraints calls, so manually-added constraints survive a
  parameter edit on a committed (outside-session) sketch.

Not done:
- Full symbolic solver (matrix-based Gauss-Newton etc.) — out of scope for this task.
- Conflict detection / overdefined diagnosis — no automatic resolution of contradictory constraints.

Broken:
- none expected; only CadProjectStore.cs touched, no new types or interfaces

Next:
- Verifier: open sketch, draw a line + arc, apply Tangent constraint, edit the line length — arc
  should re-snap to maintain tangency. Apply EqualLength + Horizontal to a simple rectangle,
  edit one entity — constraints should reassert. Fully-defined status (● Fully constrained) should
  appear when enough constraints are applied; underdefined geometry should remain blue.
- Remaining backlog tasks: 65 (dialog standardization), 67 (shell consistency), 68 (copilot assist).
