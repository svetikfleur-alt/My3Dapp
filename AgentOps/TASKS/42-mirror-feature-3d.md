# 42. 3D Mirror feature

## Goal
Implement a 3D Mirror operator: pick body / feature(s) + mirror plane (default geometry plane or sketch line on a plane), produces a mirrored copy.

## Scope
- New top-panel button "Mirror" in Features section.
- Dialog through ToolDialogWindow: Source (body / feature multi-select), Mirror plane (Origin/Top/Front/Right or selected face), Boolean op (New body / Join).
- Real geometry: reflect mesh across the chosen plane.
- Persists as `MirrorFeature` in tree, editable.
- No new NuGet.

## Out of scope
- Mirror across an arbitrary B-rep face (only standard planes in v1).
- Pattern-based mirror (covered by linear / circular pattern features).

## Files likely involved
- `Engine/MirrorFeature.cs` (new), `Engine/MeshBuilder.cs` (mesh reflection), `Engine/CadProjectStore.cs`.
- `AvaloniaApp/Dialogs/MirrorFeatureDialog.axaml(.cs)` (new).
- `AvaloniaApp/MainWindow.axaml(.cs)` (button) — minimal per MERGE_PROTOCOL.
- `AvaloniaApp/Services/StudioWorkspaceController.cs`.

## Expected behavior (acceptance)
1. Mirror button in Features.
2. Click → dialog with Source + Plane.
3. OK produces mirrored body in viewport, MirrorFeature in tree.
4. Edit feature → change plane → result updates.
5. Boolean Join unions mirror with source; New body keeps separate.
6. Build 0/0.

## Notes / hints
- Follow MERGE_PROTOCOL.md.
- Reflection: negate the appropriate coord (X for Right plane, Y for Front, Z for Top), invert tri winding to keep outward normals correct.
- Reference: existing `LinearPatternFeature` for pattern + tree wiring.

## Complexity
Moderate. Geometry is easy (reflection); plumbing is the work. Sonnet.

## Verifier checklist
- Build 0/0.
- Smoke-test: extrude a part, click Mirror, pick Right plane → mirrored copy appears.
- Theme parity Light/Dark.
- One commit `agentops: 42 — 3D Mirror feature`.
