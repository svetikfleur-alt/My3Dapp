# VERIFICATION REPORT
Date: 2026-05-14
Inspector: routine-inspector (CAD workflow inspector + debugger)
Branch: claude/inspiring-fermi-GIy9O

---

## BUILD STATUS

Build cannot be executed — .NET runtime not available in this sandbox environment.
All inspection conducted via static code analysis.

---

## CRITICAL BUG FIXED

### Datum Plane Create/Toggle/Delete — Silent Failure (FIXED)

`CadCommandActionKind.CreateDatumPlane`, `ToggleDatumPlaneVisibility`, and
`DeleteDatumPlane` were all defined in the enum (CadModel.cs) and dispatched
by `StudioWorkspaceController`, but had NO handlers in the
`CadProjectStore.Apply` switch statement. All three actions fell through to
the `_ => Failure("Unsupported CAD action: ...")` default.

**Visible effect:** User clicks "Create Datum Plane", fills in the dialog,
clicks Create — sees an error notification. No plane appears. Task 50 was
marked done in DONE.md but the core engine handler was absent.

**Fix applied:** Added `HandleCreateDatumPlane`, `HandleToggleDatumPlaneVisibility`,
`HandleDeleteDatumPlane` methods in CadProjectStore.cs, wired in the switch.

- `HandleCreateDatumPlane`: Creates a `CadReferencePlane` with `Kind = Datum`,
  sets `DatumSourceKind` and `DatumOffsetDistance`, adds to scene, selects it.
- `HandleToggleDatumPlaneVisibility`: Toggles `Visible` flag on the plane.
- `HandleDeleteDatumPlane`: Removes datum plane (protects non-Datum planes).

---

## SMALL FIXES APPLIED

### Extrude tooltip missing shortcut key
- Was: "Extrude selected profile"
- Now: "Extrude selected profile (E)"
- The `E` key shortcut was active but undiscoverable. Hole shows "(H)", 
  Sketch Start shows "(S)" — now Extrude is consistent.

### Sweep tooltip and dialog subtitle misleading
- Tooltip was: "Sweep selected profile"
  → Now: "Sweep — extrude profile along normal with optional twist"
- Dialog subtitle was: "Create a solid by sweeping a closed sketch profile
  along the sketch plane normal."
  → Now: "Extrude a closed sketch profile along the sketch plane normal.
  Optional twist rotates the profile as it travels."
- Reasoning: The SweepSolid implementation is a twisted linear extrusion,
  not a path-following sweep. The previous wording described plain extrusion
  without even mentioning the distinguishing Twist feature.

---

## CAD WORKFLOW INSPECTION

### What works (verified by code)

**Primitives:** All 16 primitive kinds (Box → Icosahedron) produce real geometry
via SolidMesher. Boolean operations (Union/Subtract/Intersect) produce real CSG.

**Sketch → Extrude pipeline:**
- Profile detection works (ProfileBuilder.TryBuild)
- Extrude Join/Cut modes use BooleanSolid for actual CSG
- Extrude Symmetric flag passed through

**Sketch → Revolve:** Produces RevolveSolid → real geometry. Proper axis
validation (profile must stay on one side of axis). Correct.

**Sketch → Sweep:** Produces SweepSolid → real twisted-extrusion geometry.
Works. Semantically "Sweep" is a misnomer (no guide path), but geometry is real.

**Sketch → Loft:** Produces LoftSolid between two profiles. Works.
The "distance" parameter is artificial (not derived from sketch plane separation)
but geometry is real.

**Fillet / Chamfer / Shell:** All work, but limited to ExtrudeSolid base bodies.
Correct diagnostics emitted when applied to other solid types.

**Mirror / LinearPattern / CircularPattern:** Produce real geometry. Correct.

**Hole:** Produces real boolean subtraction. Through-All uses height=10000.
Correct. Face-level picking is still a fallback.

**Boolean cross-body (Union/Subtract/Intersect):** Compiles by referencing
previously compiled bodies. Cross-body wiring correct.

**Constraints (Horizontal/Vertical/Coincident/Equal/Fixed/Tangent/
Parallel/Perpendicular/Concentric):** All wired. Geometry adjustment logic
for Round-2 constraints is present (not phantom).

**DOF tracker:** SketchDofValue computed via CadProjectStore.ComputeSketchDof,
wired to all three display surfaces (toolbar hint, viewport session card,
left panel task card). Consistent.

**Construction mode:** Q shortcut + toolbar toggle + IsConstruction flag on
entities + ProfileBuilder filters construction geometry from profiles. Correct.

**Grid toggle:** G key + toolbar toggle + viewport bridge. Correct.

**Undo/Redo:** JSON snapshot stack via PushUndoSnapshot. Present on all
state-mutating operations inspected.

**Edit Sketch from tree:** Double-click and context menu both dispatch
EditSketch action. HandleEditSketch sets ActiveSketchSession in edit-session
mode. Correct.

**Body color, body visibility, toggle-V shortcut:** All wired correctly.

**Export (Ctrl+E in 3D mode):** Keyboard handler at line 962 correctly
dispatches Ctrl+E to export. No conflict with E (no modifier) = Extrude.

---

## OUTSTANDING CAD CONVENTION CONCERNS (for Builder queue)

### 1. Sweep is Extrude with Twist — not a real Sweep

**Current:** SweepSolid sweeps along sketch plane normal at a given distance
with an optional twist angle. This is a parametric twisted extrusion.

**Expected CAD behavior:** Sweep takes a profile and follows a guide path (a
separate sketch, typically a spline or polyline). The shape travels along the
path, not along the normal.

**Why this matters:** Users who know CAD will look for a guide-path input in
the Sweep dialog and find only Distance+Twist. This breaks the conceptual
model. A twisted extrude is a valid feature — it just shouldn't be called Sweep.

**Recommendation:** Either rename to "Extrude (Twist)" and make Sweep a
proper path-following feature, or retain the name and clearly document the
limitation. Tooltip fix applied as short-term bandaid.

### 2. Loft distance parameter is artificial

**Current:** LoftSolid blends between ProfileA and ProfileB over a user-supplied
distance. Both profiles come from sketches that may be on the same plane.

**Expected CAD behavior:** Loft derives the transition distance from the actual
3D separation between the two sketch planes. The user picks two profiles on
different parallel planes; the loft volume spans the gap automatically.

**Why this matters:** Current Loft always adds a manual "distance" on top of
(or instead of) actual sketch plane separation, producing counterintuitive
geometry when both sketches are on the same plane.

**Recommendation:** Resolve loft distance from the actual WorldOrigin offset
between the two sketch planes, and remove the Distance input from the dialog.
Fall back to a user-supplied distance only if planes are co-planar.

### 3. Sweep and Loft share icons with Extrude and Revolve

**Current:** Sweep button uses `extrude.svg`; Loft button uses `revolve.svg`.
No dedicated `sweep.svg` or `loft.svg` icons exist.

**Visible effect:** Four buttons in the Features group look like two pairs of
identical icons. Users cannot visually distinguish Sweep from Extrude or
Loft from Revolve without reading the tooltip.

**Recommendation:** Create minimal 16×16 SVG icons distinguishing the four
operations. Low priority but creates real UX friction.

### 4. Fillet/Chamfer/Shell limited to ExtrudeSolid

These three modify-features only produce geometry when the body's base solid
is an ExtrudeSolid. Applied to Box, Cylinder, Revolve, Sweep, etc., they
silently produce a diagnostic and leave the solid unchanged — but the toolbar
does not communicate this restriction. The user gets a success dialog from the
feature dialog but no visible geometry change.

**Recommendation:** Either gate the Fillet/Chamfer/Shell buttons with a check
for ExtrudeSolid (stricter IsEnabled logic), or emit a visible notification
when the feature cannot apply.

### 5. Hole uses "first face of body" fallback

Face-level hit-test for curved surfaces is not implemented. The HANDOFF notes
this. When the user clicks a curved face to select the hole center, the
feature silently applies to the body centroid. This is confusing.

No code fix possible without face-level picking infrastructure.

---

## UI CONSISTENCY NOTES

- Mode badge appears in both the top-right chip AND the viewport header tag —
  duplicated but not wrong.
- SketchWorkflowHint appears in both the toolbar SketchHintPanel AND the
  viewport ViewportSketchPrompt. Same text in two places. Minor visual noise.
- The "Commands - Ctrl+K" command palette button uses a text label instead of
  an icon. Unique among toolbar buttons but functional.
- DatumPlaneDialog does not use the `FeatureDialogPanel` styling class — it
  has plain StackPanel layout vs. the Grid-based pattern in all other feature
  dialogs. Visual inconsistency with other dialogs.
- SketchRotateToolButton (`Rotate`) in the toolbar is missing an
  `IsChecked` binding. Other toggle tools bind IsChecked to ViewModel state;
  the Rotate toggle does not, so it won't reflect state on external deactivation.
  Minor but inconsistent with the rest of the sketch toolbar.

---

## COMMIT

Pushed to: claude/inspiring-fermi-GIy9O
Commit: 15eb909 — routine-inspector: fix datum plane create/toggle/delete silently failing
