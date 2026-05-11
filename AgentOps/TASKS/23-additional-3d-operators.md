# 23. Additional 3D operators (revolve / fillet / chamfer / shell / linear pattern)

## Goal
Expand the 3D feature set beyond just Extrude. User reports current 3D functionality is too thin and several operators don't work. Add the standard parametric primitives that make the studio feel like a real CAD tool. Keep scope inside Blueprint (no organic modeling).

## Scope
Add these feature operators, each going through the `ToolDialogWindow` scaffold:
- **Revolve** — sketch profile + axis (sketch line or coordinate axis) + angle (default 360°). Produces a solid of revolution.
- **Fillet (3D)** — pick edges, radius, produces rounded edges.
- **Chamfer (3D)** — pick edges, distance (and optional second distance), produces chamfered edges.
- **Shell** — pick face(s) to remove, wall thickness, produces a hollowed solid.
- **Linear pattern** — pick feature(s) + direction (sketch line or coordinate axis) + count + spacing, produces a linear array.

Each operator:
- Lives in the Features section of the top panel (or wherever feature operators currently live).
- Opens a dialog with the parameters above.
- Preview in viewport before commit (where feasible — fillet preview can be coarse).
- Commits to the feature tree as a parametric feature with editable parameters.
- Roundtrips through Save/Open (when task 08 lands; for now ensure persistence in CadProjectStore).

## Out of scope
- Boolean operators (union/subtract/intersect) — separate task if needed.
- Sweep / Loft — postponed (more complex; user can request later).
- Assembly-level operations.
- Variable-radius fillet.

## Files likely involved
- `Engine/` — feature operator implementations + parametric types.
- `AvaloniaApp/Dialogs/` — one new options view per operator (RevolveOptionsView, FilletOptionsView, etc.).
- `AvaloniaApp/MainWindow.axaml(.cs)` — feature buttons.
- `AvaloniaApp/Controls/WebViewportHost.cs` — preview rendering.
- Feature tree rendering — show new feature types with appropriate icons/names.

## Expected behavior (acceptance)
1. Revolve, Fillet, Chamfer, Shell, Linear Pattern all selectable from the top panel.
2. Each opens a dialog with the spec'd parameters.
3. Each produces a committed feature visible in the feature tree.
4. Each feature is editable (double-click in tree → reopens the dialog with current params).
5. Revolve default 360° produces a full revolution; partial angles produce partial revolutions.
6. Fillet radius 0 is rejected with a non-blocking error message (use task 15 surface if available, otherwise a status bar message).
7. Shell with valid face selection produces a hollowed body.
8. Linear pattern with count=1 is a no-op (or rejected); count >=2 produces N copies.
9. Build returns 0 errors.

## Notes / hints
- Reference: `References/onshape/blocks/04_viewport.jpg` — Onshape feature toolbar style.
- Blueprint: features are parametric and edit-able post-creation. Don't bake transformations; keep the parameter graph.
- Implementation can lean on whatever geometry kernel is in place; if there's no kernel, basic CSG / mesh operations are acceptable for v1, with a TODO note.
- If a primitive is genuinely too hard to implement well in this pass, ship a partial that at least scaffolds the dialog + tree node + parameter persistence; mark in HANDOFF that geometry is stubbed.

## Verifier checklist
- Build returns 0 errors.
- Smoke-test each operator: open dialog, fill params, OK, see feature in tree and viewport (or stubbed placeholder if geometry is deferred).
- Edit one feature via tree double-click; confirm dialog reopens with current params and changes apply.
- Cancel any dialog cleanly; no orphan feature nodes left in the tree.
- One commit per operator (or one consolidated commit, executor's call).

## User addendum (2026-04-26)
User flagged: **revolve currently broken** — must be actually functional, not just a dialog stub. Same applies to 3D fillet (скругление) — functional preview + commit, real geometry. Explicitly do not ship empty handlers.
