Done:
- Datum plane Create/Toggle/Delete engine handlers added (CadProjectStore).
  All three actions were silently failing — fix committed 15eb909.
- Extrude button tooltip now shows (E) shortcut.
- Sweep tooltip and dialog subtitle corrected (honest about twist-extrude semantics).

Not done (needs Builder attention):
- Sweep is a twisted extrude, not a path-following sweep. No guide-path input.
  Rename or rebuild as proper sweep.
- Loft "distance" is artificial. Should derive separation from sketch plane
  WorldOrigins. Remove Distance dialog input when planes are distinct.
- Fillet/Chamfer/Shell silently no-op on non-ExtrudeSolid bodies. Needs better
  UX gating or notification.
- SketchRotateToolButton lacks IsChecked binding — will not reflect external deactivation.
- Sweep and Loft share icons with Extrude and Revolve (no dedicated SVGs exist).
- DatumPlaneDialog uses plain StackPanel styling, not FeatureDialogPanel pattern.
- Hole face-level picking still uses "first face of body" fallback.

Broken:
- None observed in code inspection. Runtime cannot be verified — .NET not in sandbox.

Next:
- Builder: address Sweep semantics (guide-path vs. twisted extrude)
- Builder: fix Loft distance to derive from sketch plane separation
- Builder: add SketchRotateToolButton IsChecked binding
- Verifier: re-test datum plane creation on Windows build to confirm fix
