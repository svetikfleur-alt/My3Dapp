Done (this session):
- SectionAxisCombo: added SelectionChanged="OnSectionAxisChanged" in XAML and matching handler
  in MainWindow.axaml.cs. Section plane now updates when user changes X/Y/Z axis while
  section view is active. Previously only the offset spinner change triggered a viewport update.

Not done:
- Build confirmation (dotnet not available in cloud env)

Broken:
- None expected from this change (isolated fix, same pattern as OnSectionOffsetChanged)

Next:
- Builder: create sweep.svg + loft.svg icons (Sweep button uses extrude.svg, Loft uses revolve.svg)
- Builder: spline profile rejection — add user-visible hint when a spline is drawn in sketch
  that it cannot feed an extrude profile
- Builder: feature tree chronological grouping (sketch + derived feature as timeline unit)
- Builder: constraint application on selected entity, not last-drawn entity
- Verifier: run build and confirm 0 errors; test section view axis toggle in viewport
