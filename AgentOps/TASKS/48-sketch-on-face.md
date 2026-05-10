# 48. Sketch on existing face

## Goal
Task 04 explicitly defers: _"Sketch-on-face (later; current MVP is sketch-on-plane)."_ The current implementation only allows sketching on the three reference planes (Top/Front/Right). Real CAD work requires the ability to select a flat face of an existing body and start a sketch on it — the face becomes the sketch plane. This is one of the most fundamental modeling workflows in Onshape, Fusion, and SolidWorks.

## Scope
- In 3D mode, when a flat body face is hovered/clicked, it gets a pre-select highlight like other geometry (task 12's selection model).
- When a flat face is selected and the user clicks "Start Sketch", the sketch session begins with that face as the active plane (no SelectPlaneDialog shown).
- The sketch coordinate system (origin, U/V axes) is derived from the face's local frame: origin at face centroid, U along the longest edge direction, V from cross product.
- The viewport camera normalises to look directly at the face normal (the same `normalizeViewToPlane` behaviour used for reference planes).
- Profile builder must accept entities placed on this face plane — the sketch extrude/revolve path will use the face's world transform.
- A `FacePlane` variant is added to `CadSketchPlane` (or equivalent discriminated union) capturing the source body id + face index.
- The active plane highlight in the viewport and feature tree works the same as for reference planes (task 04 behaviour reused).

## Out of scope
- Curved / cylindrical face as sketch plane (only flat/planar faces in v1).
- Automatic face detection on imported meshes without explicit plane info.
- Assembly-context face selection.
- "Convert entities" (projecting face edges as sketch construction lines) — future task.

## Files likely involved
- `Engine/CadModel.cs` — extend `CadSketchPlane` / sketch session to support face-derived plane; `FacePlane` type with `bodyId` + `faceIndex` + computed `WorldOrigin`/`WorldNormal`.
- `Engine/CadProjectStore.cs` — `StartSketchOnFace(bodyId, faceIndex)` handler; derive plane from tessellated face normal.
- `AvaloniaApp/Controls/WebViewportHost.cs` — pass `face-selected` message from JS raycaster to C# for face pre-select; expose `startSketchOnFace` bridge call.
- Viewport JS — on flat-face hover, highlight face fill with accent colour; report face pick to C# via `postMessage({ type: 'face-click', bodyId, faceIndex })`.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — `SelectedFace` reactive property; gate "Start Sketch" to enter face sketch if `SelectedFace != null`.
- `AvaloniaApp/MainWindow.axaml(.cs)` — "Start Sketch" already wired; add face path.

## Expected behavior (acceptance)
1. Hover a flat face of any body → face fills with translucent accent highlight (pre-select).
2. Click the face → face stays highlighted (selected); feature tree shows body highlighted; status bar shows "1 face selected".
3. Click "Start Sketch" with a face selected → sketch session starts, camera normalises to face normal, viewport shows sketch grid on the face plane, feature tree shows active sketch under that body.
4. Drawing a closed profile on the face → Extrude operates in the face's coordinate frame, producing a solid correctly attached to the face.
5. Non-flat (curved) faces do not trigger sketch-on-face — click on a curved face selects the body, not a face plane.
6. Finish / Cancel exits sketch; camera returns to previous position; face highlight clears.
7. Build: 0 errors, 0 warnings.

## Notes / hints
- For planar face detection: a face is flat if all its triangle normals are within ~1° of each other. Compute the average normal from the tessellation returned by `TessellateBody`.
- Face centroid = average of all face vertex positions.
- U axis: project the longest edge direction onto the face plane; V = Normal × U.
- Keep `FacePlane` serialisable so it round-trips through the parametric store.
- This task does NOT require a full B-rep — it only needs the tessellated face geometry from the existing mesh pipeline.
- Reference: Onshape "Select plane or planar face to sketch on" tooltip at sketch entry.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] Hover flat face of a Box → face highlights in accent colour.
- [ ] Click face → selected, status bar shows face count.
- [ ] Start Sketch → sketch opens on face plane; camera looks at face.
- [ ] Draw + extrude profile on face → solid geometry correct.
- [ ] Curved face (sphere/cylinder) does NOT trigger face-plane sketch.
- [ ] Cancel exits cleanly; no orphan sketch session.
- [ ] No regression on reference-plane sketch entry (task 04 acceptance still passes).

## Complexity
Medium-high. Raycaster face picking + normal/plane derivation from tessellation data is new plumbing. Most of the sketch session wiring already exists — the main work is the face→plane conversion and the JS face-highlight pathway.
