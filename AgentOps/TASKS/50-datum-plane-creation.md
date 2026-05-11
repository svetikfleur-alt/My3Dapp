# 50. Datum plane creation (custom reference planes)

## Goal
Task 04 explicitly defers: _"Custom plane creation (separate feature: Datum Plane)."_ The current implementation only provides the three fixed reference planes (Top/Front/Right). Real parametric CAD work — angled cuts, revolved features on non-standard axes, mirroring at arbitrary angles — requires user-created reference planes. This task adds the Datum Plane feature, matching the Onshape / Fusion baseline.

## Scope
Three creation methods (cover the most common cases):

1. **Offset from plane** — pick an existing plane (or reference plane) + enter an offset distance. The datum plane is parallel to the source at the given offset. Example: "50 mm above Top plane."
2. **Through three points** — pick three sketch points or body vertices (from the viewport). The plane passes through all three.
3. **Angle from plane + edge** — pick a reference plane and a straight edge; specify an angle. The datum plane rotates about that edge.

UI flow:
- New "Datum Plane" button in the 3D Features section of the top panel (or in a "Reference" sub-group).
- Opens a `DatumPlaneDialog` (via existing `ToolDialogWindow` scaffold) with a method selector (radio/segmented control) and the relevant input fields.
- Preview: while the dialog is open, a translucent plane ghost is shown in the viewport that updates live as parameters change.
- On OK: a `DatumPlaneFeature` is added to the feature tree as a reference entry (not a solid body). It appears under "Default Geometry / Reference Planes" or as its own tree section.
- The datum plane is available as a sketch target (task 48 face-selection pathway, or existing task 04 plane-selector dialog which should be updated to list user datum planes).
- On double-click of the tree node: dialog reopens with current params; editing propagates downstream.
- Datum planes can be hidden/shown via right-click context in the feature tree.

## Out of scope
- Plane through mid-point of two faces (edge-bisector plane) — future.
- Tangent plane to a curved surface — future.
- Plane at centroid of a body — future.
- Named coordinate systems / frames (separate concept).

## Files likely involved
- `Engine/CadModel.cs` — new `DatumPlaneFeature : CadFeature` with `DatumPlaneMethod` enum (Offset / ThreePoints / AngleFromEdge) and method-specific parameters; `WorldOrigin` / `WorldNormal` computed properties.
- `Engine/CadProjectStore.cs` — `HandleCreateDatumPlane` action; integrate datum plane list into `GetAvailablePlanes()` (so task 04 selector sees it).
- `AvaloniaApp/Dialogs/DatumPlaneDialog.axaml(.cs)` — new dialog with method switcher, parameter fields (offset distance / point pickers / angle slider), live-preview trigger.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — expose `DatumPlanes` observable; route dialog open/close; update feature tree.
- `AvaloniaApp/MainWindow.axaml(.cs)` — "Datum Plane" button in 3D toolbar; datum plane nodes in feature tree with reference-plane styling.
- Viewport JS — `drawDatumPlanePreview(origin, normal, size)` ghosted translucent quad; call from `WebViewportHost` while dialog is open.
- `AvaloniaApp/Themes/Studio.Dark.axaml`, `Studio.Light.axaml` — datum plane node icon / style (reuse existing reference plane visual, or add a distinct "user reference" look).

## Expected behavior (acceptance)
1. "Datum Plane" button is visible in the 3D Features toolbar.
2. Click → DatumPlaneDialog opens; Offset is the default method.
3. **Offset method**: pick "Top" reference plane, set offset 50 mm → live preview shows a plane 50 mm above Top. OK → datum plane appears in feature tree, visible in viewport as a translucent quad.
4. **Three-points method**: pick 3 sketch points → preview plane appears through all three. OK → persists.
5. **Angle method**: pick Right plane + a horizontal edge + 45° → preview shows tilted plane. OK → persists.
6. Datum plane appears in the SelectPlaneDialog list (task 04) as a sketch target.
7. Start Sketch with the datum plane selected → sketch opens on the user-defined plane.
8. Double-click datum plane node → dialog reopens with current parameters.
9. Right-click → Hide/Show toggles visibility in viewport.
10. Build: 0 errors, 0 warnings.

## Notes / hints
- Keep geometry pure: `DatumPlaneFeature` stores only parameters and exposes computed `WorldOrigin`/`WorldNormal`. No tessellation needed — it is a reference, not a solid.
- For "Through three points": use the cross product of (P2−P1) × (P3−P1) for the normal; origin = P1.
- For "Angle from edge": rotate the source plane's normal about the edge direction vector by the given angle; origin = any point on the edge.
- Viewport ghost: a simple `PlaneGeometry(size, size)` in Three.js with `MeshBasicMaterial({ color, transparent: true, opacity: 0.25, side: DoubleSide })`. Size = max scene bound or fixed 200 mm.
- Follow MERGE_PROTOCOL.md for MainWindow.axaml edits.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Datum Plane button in 3D toolbar.
- [ ] Offset method: live preview + persists correctly.
- [ ] Three-points method: correct plane through all three picks.
- [ ] Angle method: plane rotates by specified angle about edge.
- [ ] Datum plane listed in SelectPlaneDialog; sketch can be started on it.
- [ ] Tree node present; double-click reopens dialog.
- [ ] Hide/Show via right-click works.
- [ ] No regression on reference-plane sketch entry.

## Complexity
Medium. The geometry math is straightforward (plane from normal + origin). Main effort is the dialog UI with three-mode switching, the live preview pipeline, and integrating datum planes into the existing plane-selector.
