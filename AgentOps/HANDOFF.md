Done:
- UI polish pass (2026-05-14): SketchTaskCard + SketchSidePanel now have CornerRadius (14/12); dialog buttons (Primary/Secondary) have pointerover + pressed hover states; ModeToggleButton has explicit CornerRadius=10; TreeNodeSecondaryText FontSize 9.75→10 for readability; selected tree items now show subtle accent BorderBrush; toolbar "Commands - Ctrl+K" label cleaned to "Commands" with tooltip; both Dark and Light themes updated; commit 3489091

Not done:
- Build not verified (dotnet not installed in this agent environment); changes are AXAML/style-only (no C# logic altered); low build-break risk

Broken:
- none expected

Next:
- Verifier: run build (0 errors expected); spot-check rounded corners on SketchTaskCard in sketch mode; verify dialog Cancel/Apply buttons show hover state; confirm tree selection accent visible
