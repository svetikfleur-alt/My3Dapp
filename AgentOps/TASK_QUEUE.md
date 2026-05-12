# Active queue (in priority order)

- [x] 20 — Fix Avalonia/WinForms namespace clash       → TASKS/20-fix-winforms-namespace-clash.md
- [x] 21 — Viewport navigation (orbit/pan/zoom/walk)   → TASKS/21-viewport-navigation.md
- [x] 22 — Sketch tool expansion                       → TASKS/22-sketch-tool-expansion.md
- [x] 23 — Additional 3D operators                     → TASKS/23-additional-3d-operators.md
- [x] 24 — Move tool (not always-on gizmo)             → TASKS/24-move-tool-not-always-on-gizmo.md
- [x] 25 — Assistant panel fix (key state + layout)    → TASKS/25-assistant-panel-fix.md
- [x] 35 — 2D sketch transform tool                    → TASKS/35-2d-sketch-transform-tool.md
- [x] 36 — Sketch Mirror — proper implementation       → TASKS/36-sketch-mirror-proper.md
- [x] 37 — Sketch Trim — proper implementation         → TASKS/37-sketch-trim-proper.md
- [x] 38 — Construction lines / centerline toggle      → TASKS/38-construction-lines.md
- [x] 39 — Angle dimension between two lines           → TASKS/39-angle-dimension.md
- [x] 26 — Boolean body operations (Union/Subtract/Intersect) → TASKS/26-boolean-body-operations.md
- [x] 27 — Sketch grid and snap markers                → TASKS/27-sketch-grid-and-snap.md
- [x] 28 — Circular Pattern feature                    → TASKS/28-circular-pattern-feature.md
- [x] 29 — Hole feature                                → TASKS/29-hole-feature.md
- [x] 30 — Export panel (STL / OBJ)                    → TASKS/30-export-panel-stl-obj.md
- [x] 31 — Extrude Join / Cut / Symmetric modes        → TASKS/31-extrude-join-cut-symmetric.md
- [x] 32 — Sketch constraints round 2 (Tangent/Parallel/Perpendicular/Concentric/Fix) → TASKS/32-sketch-constraints-round-2.md
- [x] 33 — Edit sketch from feature tree               → TASKS/33-edit-sketch-from-feature-tree.md
- [x] 34 — Sketch entity right-click context menu      → TASKS/34-sketch-entity-context-menu.md
- [x] 40 — Sweep feature                               → TASKS/40-sweep-feature.md
- [x] 41 — Loft feature                                → TASKS/41-loft-feature.md
- [x] 42 — 3D Mirror feature                           → TASKS/42-mirror-feature-3d.md
- [x] 44 — Circle trim — split full circle at intersections        → TASKS/44-circle-trim-split.md
- [x] 45 — Mirror / Transform for Slot, Polygon, Spline entities   → TASKS/45-mirror-composite-entities.md
- [x] 46 — Assistant context injection (action log → system prompt) → TASKS/46-assistant-context-injection.md
- [x] 47 — Sketch DOF tracker + constrained status indicator       → TASKS/47-sketch-dof-tracker.md
- [x] 48 — Sketch on existing face                                 → TASKS/48-sketch-on-face.md
- [x] 49 — Box-select (rubber-band selection) in 3D mode           → TASKS/49-box-select-3d.md
- [x] 50 — Datum plane creation (custom reference planes)          → TASKS/50-datum-plane-creation.md
- [x] 51 — Driving vs driven sketch dimensions                     → TASKS/51-driving-vs-driven-dimensions.md
- [x] 52 — Sketch 2D rotate tool                                   → TASKS/52-sketch-2d-rotate-tool.md
- [x] 01 — Extrude — full operator                     → TASKS/01-extrude-full-operator.md
- [x] 02 — Sketch tools — preview + commit             → TASKS/02-sketch-tools-preview-commit.md
- [x] 03 — Sketch dialog — wire remaining tools        → TASKS/03-sketch-dialog-wire-tools.md
- [x] 04 — Plane selection + sketch entry              → TASKS/04-plane-selection-sketch-entry.md
- [x] 05 — Feature tree presentation (icons per type)  → TASKS/05-feature-tree-presentation.md
- [x] 06 — Top panel polish round 2                    → TASKS/06-top-panel-polish-round-2.md
- [x] 07 — Constraints in sketch dialog                → TASKS/07-constraints-in-sketch-dialog.md
- [x] 08 — Save / Open project                         → TASKS/08-save-open-project.md
- [x] 09 — Undo / Redo                                 → TASKS/09-undo-redo.md
- [x] 10 — Status bar + keyboard shortcuts             → TASKS/10-status-bar-shortcuts.md
- [x] 11 — Viewport polish                             → TASKS/11-viewport-polish.md
- [x] 12 — Selection model + highlight                 → TASKS/12-selection-model-highlight.md
- [x] 13 — Sketch dimensions                           → TASKS/13-sketch-dimensions.md
- [x] 14 — Project metadata + recent files             → TASKS/14-project-metadata-recents.md
- [x] 15 — Error / message surface                     → TASKS/15-error-message-surface.md
- [x] 16 — Engine sketch consolidation                 → TASKS/16-engine-sketch-consolidation.md
- [x] 17 — Render quality pass (edge lines + hover)    → TASKS/17-render-quality-pass.md
- [x] 18 — Feature tree keyboard nav                   → TASKS/18-tree-keyboard-nav.md
- [x] 19 — Final UI polish pass                        → TASKS/19-final-ui-polish-pass.md
- [x] 53 — Body color / appearance                      → completed inline
- [x] 54 — Section view / cross-section                  → TASKS/54-section-view.md
- [x] 55 — Measurement tool (point-to-point distance)    → TASKS/55-measurement-tool.md
- [x] 56 — View cube widget                              → TASKS/56-view-cube.md
- [x] 57 — Sketch inference hints (hover snap labels)    → TASKS/57-sketch-inference-hints.md

- [x] 58 — Body visibility toggle (eye icon + V shortcut)  → TASKS/58-body-visibility-toggle.md
- [x] 59 — Sketch offset entity                           → already implemented
- [x] 60 — Sketch corner fillet (2D)                     → already implemented
- [x] 61 — Smooth camera snap animation                  → TASKS/61-smooth-camera-snap.md

- [x] 62 — Polyline sketch tool                          → covered by Line tool (chains segments via PendingLineStart)
- [x] 63 — Shell feature dialog                          → already uses NumericFeatureDialog; generic panel wired
- [x] 64 — Double-click rename in feature tree           → F2 rename already wired; dbl-click opens param edit

# Backlog (out of current cycle)
# Note: unqueued task files 53-hole-csg-geometry and 54-boolean-csg-subtract-intersect
#       describe work that is already done in code (SolidMesher.cs + CadProjectStore.cs);
#       no queue entries needed for them.
- [ ] 66 — Sketch solver and definition behavior             → TASKS/66-sketch-solver-definition-behavior.md   ← NEXT
- [ ] 65 — CAD command dialog standardization                 → TASKS/65-cad-command-dialog-standardization.md
- [ ] 67 — Shell consistency pass (toolbar + viewport nav)   → TASKS/67-shell-consistency-toolbar-viewport.md
- [ ] 68 — Copilot structured CAD assistance                 → TASKS/68-copilot-structured-cad-assistance.md
- [ ] 69 — Polyline sketch tool                              → TASKS/55-polyline-sketch-tool.md
