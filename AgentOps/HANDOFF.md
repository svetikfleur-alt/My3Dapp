Done (routine-ui polish pass — 2026-05-10):
- MainWindow.axaml: All 14 toolbar group labels uppercased (WORKFLOW/SHAPES/FEATURES/VIEW/TRANSFORM/MODIFY/BOOLEAN/DATUM/SKETCH/CONSTRUCTION/GRID/DIMENSION/CONSTRAINTS)
- MainWindow.axaml: Commands button label cleaned ("Commands - Ctrl+K" → "Commands" + tooltip)
- MainWindow.axaml: Sketch cancel button in SketchTaskCard uses delete.svg instead of ✕ TextBlock
- Studio.Dark.axaml + Studio.Light.axaml: ToolbarGroupLabel gains LetterSpacing=0.9, FontSize=9.5 for polished uppercase look
- Studio.Dark.axaml + Studio.Light.axaml: ToolbarActionButton + ToolbarIconToggle + SketchTaskActionButton gain CornerRadius=7
- Studio.Dark.axaml + Studio.Light.axaml: SketchTaskCard gains CornerRadius=10; SketchSidePanel gains CornerRadius=8

Not changed:
- Geometry engine, sketch logic, Extrude/Revolve/Hole/Boolean operations
- Viewport, WebViewportHost, all dialogs
- MeasureToolToggle/SectionViewToggle text glyphs (no suitable SVG available for those)
- Sweep/Loft icon workarounds (no sweep.svg or loft.svg in Assets/Icons)

Broken:
- none expected

Next:
- Verifier: run dotnet build, confirm 0 errors; verify toolbar labels display correctly in both themes
