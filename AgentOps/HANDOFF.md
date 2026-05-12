Done:
- Inspection pass 2026-05-12 (commit a3a3685)
- Fixed SketchRotateToolButton missing IsChecked binding (IsRotateSketchToolSelected property added to VM)
- Fixed datum plane store handlers: CreateDatumPlane / ToggleDatumPlaneVisibility / DeleteDatumPlane
  were all unhandled in CadProjectStore.Apply() — added three handler methods + switch cases

Not done:
- Sweep / Loft have no dedicated SVG icons; both reuse Extrude/Revolve icons
- FinishSketch undo integration not verified (needs deeper trace into WorkspaceController)

Broken:
- Nothing new broken

Next:
- Builder: add sweep.svg / loft.svg icons (see VERIFICATION.md TASK-NEW-A)
- Builder or verifier: trace FinishSketch through WorkspaceController to confirm undo is gated
- Verifier: confirm datum plane Create/Toggle/Delete now work in running app (create, see in tree,
  right-click toggle visibility, right-click delete)
