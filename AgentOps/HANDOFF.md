Done:
- Hole feature dialog + toolbar button + H shortcut (see prior log)
- routine-ui polish pass (2026-05-12, commit 55971de)
  - New icons: sweep.svg, loft.svg, measure.svg, section.svg
  - Sweep toolbar button fixed to use sweep.svg (was incorrectly extrude.svg)
  - Loft toolbar button fixed to use loft.svg (was incorrectly revolve.svg)
  - MeasureToolToggle / SectionViewToggle: replaced Unicode text glyphs with SVG icons
  - ToolbarActionButton, ToolbarIconToggle, SketchConstraintToolButton: CornerRadius added (both themes)
  - SketchTaskCard: CornerRadius=14 added (both themes)
  - SketchSidePanel: CornerRadius=12 added (both themes)
  - FeatureDialogButtonPrimary: now solid filled blue, hover+pressed states (both themes)
  - FeatureDialogButtonSecondary: hover state added (both themes)
  - ToolbarGroupLabel: LetterSpacing=0.4 for better readability
  - Extrude/Revolve tooltip text improved

Not done:
- Build not verified (dotnet not available in environment)
- Live preview in feature dialogs (no dialog→viewport bridge exists)

Broken:
- none expected

Next:
- Verify build after merge to main
- Further sketch mode visual clarity if desired
