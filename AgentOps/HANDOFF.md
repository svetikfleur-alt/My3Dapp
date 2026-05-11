done (Inspector pass — 2026-05-11):
- Merged full codebase from origin/claude/youthful-hawking-veRo1 into claude/inspiring-fermi-WKlvN
- Fixed Engine/CadProjectStore.cs — sketch preview cursor dots missing in current branch:
  - Point tool: now shows cursor dot when hovering (was: nothing)
  - Line tool: now shows cursor dot before first click (was: nothing)
  - Rectangle tool: now shows cursor dot before first click and anchor point when at anchor (was: nothing / empty)
- These fixes were present in youthful-hawking-U7VHz but absent from phone-cloud-sync history
- Written full VERIFICATION.md with CAD workflow inspection, UX bugs flagged, Builder tasks A-E

Not done:
- Build not confirmed (no dotnet SDK in execution environment)
- Tree icon/glyph overlap not fixed (needs `ShowGlyph => IsSectionNode` change, flagged as Task A)
- Sweep/Loft missing dedicated icons (flagged as Task B)
- Export button missing ToolSplitGroup wrapper (flagged as Task C)

Known issues (see VERIFICATION.md for full details):
- ShowGlyph == HasSvgIcon for all non-section tree nodes → glyph text overlaps SVG icon
- Sweep and Loft toolbar buttons both reuse incorrect icons
- Transform/Rotate tool deactivation defaults to "Rectangle" regardless of prior tool
- `OnSketchStartClick` is dead code in MainWindow.axaml.cs
- Feature tree double-click cascade is architecturally fragile (10+ sequential checks)

Next:
- Builder: fix tree icon/glyph overlap (Task A — trivial FeatureNodeViewModel.cs change)
- Builder: add sweep.svg and loft.svg icons, update AXAML references (Task B)
- Builder: wrap Export button in ToolSplitGroup (Task C — trivial)
- Builder: remove dead OnSketchStartClick (Task D — trivial)
- Verifier: run build and confirm the sketch preview cursor fixes compile and render correctly
