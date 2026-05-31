# Solid Features MVP Truth Inventory

## 1. Box / Cuboid
- **Status:** WORKING
- **Implementation:** `MeshBuilder.CreateBox`
- **Parameters:** Dimensions via `AddPrimitive` command.
- **Validation:** None needed for primitive generation.
- **Behavior:** Creates real body, appears in tree/history, viewport updates.
- **Limitations:** Limited placement options currently.

## 2. Cylinder
- **Status:** WORKING
- **Implementation:** `MeshBuilder.CreateCylinder`
- **Parameters:** Radius, Height.
- **Validation:** Checks for non-zero radius/height.
- **Behavior:** Creates real body, appears in tree/history, viewport updates.
- **Limitations:** Basic primitives only.

## 3. Hole / Cut Cylinder
- **Status:** LIMITED MVP
- **Implementation:** `HoleFeature` compiles to a `CylinderSolid` which is then passed to a `BooleanSolid (Subtract)`.
- **Parameters:** Target body, diameter, depth (ThroughAll or specific), X/Y offsets from center.
- **Validation:** Verifies target body exists.
- **Behavior:** Performs a real boolean subtraction of a cylinder from the target body.
- **Limitations:** Uses boolean subtract under the hood without face/edge ID placement. Only supports centered or X/Y offset placement from the part origin.

## 4. Extrude
- **Status:** WORKING
- **Implementation:** `ExtrudeFeature` -> `ExtrudeSolid` -> `MeshBuilder.CreateExtrusion`.
- **Parameters:** Selected closed sketch profile, distance, operation.
- **Validation:** Verifies sketch is closed.
- **Behavior:** Creates real extruded body.
- **Limitations:** None for basic extrusion.

## 5. Revolve
- **Status:** EXPERIMENTAL MESH MVP
- **Implementation:** `RevolveFeature` -> `RevolveSolid` -> `MeshBuilder.CreateRevolution`.
- **Parameters:** Selected sketch, angle, revolve axis.
- **Validation:** Validates that the profile does not cross the selected axis (all points must be >= 0 or <= 0 relative to the axis).
- **Behavior:** Generates a real lathe-like mesh geometry from the sketch points.
- **Limitations:** Produces experimental mesh geometry rather than exact B-rep.

## 6. Sweep
- **Status:** EXPERIMENTAL MESH MVP
- **Implementation:** `SweepFeature` -> `SweepSolid` -> `MeshBuilder.CreateSweep`.
- **Parameters:** Sketch profile, sweep distance, twist degrees.
- **Validation:** Requires a valid closed sketch profile.
- **Behavior:** Sweeps the profile along a straight Z axis distance, with optional twist.
- **Limitations:** Straight path only (along Z). Does not support custom curved guide paths. Produces experimental mesh.

## 7. Loft
- **Status:** EXPERIMENTAL MESH LOFT
- **Implementation:** `LoftFeature` -> `LoftSolid` -> `MeshBuilder.CreateLoft`.
- **Parameters:** Two sketch profiles (ProfileA, ProfileB), distance.
- **Validation:** Validates both profiles exist and can be built.
- **Behavior:** Generates a mesh bridging the two 2D profiles over the specified Z distance.
- **Limitations:** Supports exactly two profiles. Produces experimental mesh, not exact B-rep surfaces.

## 8. Shell
- **Status:** LIMITED MVP
- **Implementation:** `ShellFeature` modifies the base `ExtrudeSolid`.
- **Parameters:** Target body, wall thickness.
- **Validation:** Verifies the base solid is an `ExtrudeSolid`.
- **Behavior:** Modifies the extrusion parameters to generate a hollow profile.
- **Limitations:** Only works on bodies that are driven by a simple `ExtrudeFeature`. Does not work on arbitrary 3D B-rep solids or booleans.

## 9. Fillet
- **Status:** LIMITED MVP
- **Implementation:** `FilletFeature` modifies the base `ExtrudeSolid` (`MeshBuilder.RoundPolygonCorners`).
- **Parameters:** Target body, radius.
- **Validation:** Verifies the base solid is an `ExtrudeSolid`.
- **Behavior:** Rounds the 2D polygon corners of the base sketch before extrusion.
- **Limitations:** Does not support 3D edge selection. Only rounds the 2D footprint of an extrusion.

## 10. Chamfer
- **Status:** LIMITED MVP
- **Implementation:** `ChamferFeature` modifies the base `ExtrudeSolid` (`MeshBuilder.ChamferPolygonCorners`).
- **Parameters:** Target body, distance.
- **Validation:** Verifies the base solid is an `ExtrudeSolid`.
- **Behavior:** Chamfers the 2D polygon corners of the base sketch before extrusion.
- **Limitations:** Does not support 3D edge selection. Only chamfers the 2D footprint of an extrusion.

## 11. Pattern
- **Status:** LIMITED MVP
- **Implementation:** `LinearPatternFeature` -> `LinearPatternSolid` -> `MeshBuilder.TessellateLinearPattern`.
- **Parameters:** Source body, count, spacing, axis.
- **Validation:** Verifies source body exists.
- **Behavior:** Duplicates the mesh output of the target body N times along the chosen axis.
- **Limitations:** Body-level pattern MVP only. Duplicates body outputs rather than parametrically duplicating features within the timeline.

## 12. Quick Export STL
- **Status:** WORKING
- **Implementation:** `_workspaceController.ExportStl(stlPath)` inside `StudioShellViewModel`.
- **Parameters:** Target path on desktop.
- **Validation:** Verifies part has geometry to export.
- **Behavior:** Writes real `.stl` to disk.
- **Limitations:** None for STL format.
