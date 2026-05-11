# 05. Feature tree presentation pass

## Goal
Make the feature tree read like a real CAD timeline — clear hierarchy, icons, indentation, expand/collapse, a default-geometry section, and a visible marker on the currently active plane / sketch / feature.

## Scope
- Node icons per type: Plane, Sketch, Extrude, Revolve, Fillet, Shell, Mirror, Body, Origin.
- Two-level indentation: top-level features at root; child entities (sketches under a feature, or constraints under a sketch) indented.
- Expand/collapse chevrons on parent nodes.
- "Default geometry" section at top: Origin, Top/Front/Right reference planes, world axis.
- Active highlighting: the current sketch / active plane / last-edited feature is bolded or accent-tinted.
- Hover row highlight; selected row highlight distinct from active.
- Right-click context: Edit, Suppress, Delete, Rename (placeholders OK if not all wired).

## Out of scope
- Drag-to-reorder timeline (separate task).
- Inline rename (separate; right-click Rename can open a small dialog).
- Filtering enhancements beyond what already exists.

## Files likely involved
- `AvaloniaApp/ViewModels/FeatureNodeViewModel.cs` — children, icon name, active flag.
- `AvaloniaApp/MainWindow.axaml` — feature tree TreeView template.
- `AvaloniaApp/Themes/Studio.Light.axaml`, `Studio.Dark.axaml` — node row styles, active brush.
- `AvaloniaApp/Services/SvgIconLoader.cs` (Studio path) and any new asset under `Assets/`.

## Expected behavior (acceptance)
1. Feature tree renders Default Geometry section first (Origin, Top, Front, Right) with a divider before user features.
2. Each node shows an icon matching its feature type.
3. Sketches appear as children of the feature that consumes them (or as standalone if not yet consumed).
4. Active plane / active sketch / current feature are bolded or accent-tinted.
5. Chevrons toggle expand/collapse; state persists during the session.
6. Right-click on a node shows Edit / Suppress / Delete / Rename (some may be no-op stubs).

## Notes / hints
- Reference layouts: `References/onshape/blocks/03_left_feature_panel.jpg`, `References/zoo/blocks/02_left_feature_tree.jpg`.
- Keep all logic in ViewModel; XAML stays declarative.
- Avoid introducing new icons libraries; reuse existing SvgIconLoader pattern.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Default Geometry section visible at top.
- [ ] Icons present per node type.
- [ ] Active plane during sketch is bold/tinted.
- [ ] Expand/collapse works; state preserved during interaction.
- [ ] Right-click context menu opens with the listed items.

## User addendum (2026-04-27)
Treat **Onshape's feature tree as the visual + behavioral baseline** — the level of polish the user expects. Reference: `References/onshape/blocks/03_left_feature_panel.jpg`. Specifically:
- Default-geometry section (Origin / Top / Front / Right) visually distinct from user features.
- Clean expand/collapse chevrons aligned consistently.
- Active sketch / current feature highlighted clearly.
- Icons per feature type (sketch, extrude, revolve, fillet, chamfer, shell, pattern) — small, monochrome, consistent weight.
- Hover state per row.
- Compact density — no SaaS-style padding.

Don't ship a partial that "kinda looks like a tree" — match the Onshape baseline.
