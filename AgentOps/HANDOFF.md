Done:
- Engine/CadProjectStore.cs: Fixed SketchConstraintDofCost — EqualRadius and EqualLength with
  a single entity now cost 0 DOF (previously 1), removing false DOF reduction from inferred
  self-constraints on circles and arcs.
- Engine/CadProjectStore.cs: Added public ComputeConstrainedEntityIds(CadSketchSession) —
  per-entity DOF tracker using a dofMap; only entities with remaining DOF == 0 are returned
  as "constrained" (dark colored). Each constraint kind allocates DOF cost to the correct
  driven entity.
- AvaloniaApp/Services/StudioWorkspaceController.cs: Updated BuildConstrainedEntityIds to
  accept entities and delegate to CadProjectStore.ComputeConstrainedEntityIds. Now entities
  only color dark when truly fully constrained, not just because they appear in any constraint.
- Engine/CadProjectStore.cs: Increased solver iterations from 4 to 8 for better convergence
  on multi-constraint chains.

Not done:
- Build not verified (dotnet not installed in agent environment). Logic changes only.
- No interactive drag-to-constrain; editing is still via parameter dialogs.

Next:
- Verifier: dotnet build → 0 errors expected.
- Check: place a circle in sketch — should remain blue until Fixed or fully dimensioned.
- Check: apply horizontal constraint to a line — line should remain blue (only 1 of 4 DOF removed).
- Check: apply Fixed constraint to a circle — circle should turn dark (fully constrained).
- Check: tree tooltip shows "Sketch fully defined." only when DOF == 0.
- Remaining backlog: tasks 65, 67, 68.
