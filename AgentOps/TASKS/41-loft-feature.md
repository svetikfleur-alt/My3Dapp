# 41. Loft feature

## Goal
Implement a Loft operator: a solid blended between two or more profile sketches. Standard CAD primitive in Onshape / Fusion / SolidWorks Features menu.

## Scope
- New top-panel button "Loft" in Features section.
- Dialog through ToolDialogWindow: Profiles (ordered list — pick 2+ sketch profiles), End conditions (Tangent / Normal / Free), Boolean op (New body / Join / Cut).
- Real geometry: linearly interpolate between profile cross-sections, tessellate to mesh.
- Persists as parametric `LoftFeature`, editable via tree.
- No new NuGet.

## Out of scope
- Guide curves.
- Centerline loft.
- Surface loft (solid only in v1).

## Files likely involved
- `Engine/LoftFeature.cs` (new), `Engine/MeshBuilder.cs` (loft tessellation), `Engine/CadProjectStore.cs`.
- `AvaloniaApp/Dialogs/LoftFeatureDialog.axaml(.cs)` (new) — must support ordered multi-selection of profiles.
- `AvaloniaApp/MainWindow.axaml(.cs)` (button) — minimal addition per MERGE_PROTOCOL.
- `AvaloniaApp/Services/StudioWorkspaceController.cs`.

## Expected behavior (acceptance)
1. Loft button in Features section.
2. Click Loft → dialog with ordered Profiles list (can add multiple).
3. With ≥2 profiles selected, OK produces a blended solid.
4. Tree shows `Loft1` feature; double-click reopens dialog.
5. Profile order matters — reordering changes the result.
6. Boolean op variants work.
7. Build 0/0.

## Notes / hints
- Follow MERGE_PROTOCOL.md.
- Tessellation: for each adjacent profile pair, interpolate corresponding vertices linearly, generate side tris. End caps closed.
- Reference: `SweepFeature` (task 40) for pattern; Loft is similar but no path — just profile-to-profile interpolation.

## Complexity
Moderate-high. Same as Sweep. Sonnet should handle wiring; bump to Opus if geometry stubs.

## Verifier checklist
- Build 0/0.
- Smoke-test: two sketch profiles on parallel planes → Loft → solid blends them.
- Edit feature, change profile order, result updates.
- One commit `agentops: 41 — Loft feature`.
