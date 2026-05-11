# 40. Sweep feature

## Goal
Implement a Sweep operator: a sketch profile is swept along a path (sketch line / spline / edge) producing a solid. Standard CAD primitive — Onshape, Fusion, SolidWorks all have it as a top-level Features command.

## Scope
- New top-panel button "Sweep" in Features section (next to Extrude / Revolve).
- Dialog through ToolDialogWindow scaffold: Profile (selection), Path (selection), End conditions (closed / open path), Twist (degrees, default 0), Boolean op (New body / Join / Cut).
- Real geometry: extrude profile along path with the given twist. Tessellated mesh for viewport.
- Persists as parametric `SweepFeature` in feature tree, double-click reopens dialog with current params.
- No NuGet packages added.

## Out of scope
- Sweep along helical path with variable section.
- Multi-rail sweep / lofted sweep.
- Sweep along an arbitrary B-rep edge (only sketch-curve paths in v1).

## Files likely involved
- `Engine/SweepFeature.cs` (new), `Engine/MeshBuilder.cs` (sweep tessellation), `Engine/CadProjectStore.cs` (feature dispatch).
- `AvaloniaApp/Dialogs/SweepFeatureDialog.axaml(.cs)` (new).
- `AvaloniaApp/MainWindow.axaml(.cs)` (button) — minimal addition per MERGE_PROTOCOL.
- `AvaloniaApp/Services/StudioWorkspaceController.cs` (handler).

## Expected behavior (acceptance)
1. Sweep button visible in top panel Features section.
2. Click Sweep → dialog opens with Profile/Path/Twist/Boolean fields.
3. OK with valid profile + path → solid appears in viewport, feature added to tree.
4. Cancel cleans up; no orphan tree node.
5. Double-click feature in tree reopens dialog with current parameters.
6. Boolean op `Cut` subtracts the swept volume from existing body; `Join` unions.
7. Build 0 errors, 0 warnings.

## Notes / hints
- Follow MERGE_PROTOCOL.md — additive changes only on shared files, marker comments around your block.
- Reference: how `RevolveFeature` was wired in task 23. Mirror that pattern.
- For tessellation: discretize path into N segments, transform profile copies along each segment, stitch tris between adjacent profile copies.

## Complexity
Moderate-high. Sonnet should handle the wiring; the geometry math is the trickier bit. Bump to Opus if first pass produces stub geometry.

## Verifier checklist
- Build 0/0.
- Open app, draw sketch profile, switch to 3D, draw path on another sketch (or use existing line), click Sweep, verify dialog and result.
- Test Boolean Join + Cut variants.
- Edit feature via tree double-click — params reload, change applies.
- One commit `agentops: 40 — Sweep feature`.
