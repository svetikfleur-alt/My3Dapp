# 67. Shell consistency pass — toolbar and viewport navigation

## Goal
Remove the remaining “two generations stitched together” feeling in the studio shell and make the toolbar + viewport navigation read as one coherent CAD workspace.

## Scope
- Tighten the top command strip so labels, spacing, and groups stay compact and readable.
- Keep primitives in a pop-down menu pattern.
- Keep feature commands directly clickable, not hidden in pop-downs.
- Refine the viewport orientation controls:
  - compact cube
  - compact orientation dock
  - no oversized always-open navigation cluster
- Keep the viewport dominant and clean.
- Ensure Light and Dark themes both style these surfaces consistently.

## Required behavior
- No clipped/truncated toolbar group labels.
- No old debug-style viewport button grid.
- View controls feel intentional and secondary to the model view.
- No regression in existing feature buttons.

## Out of scope
- New CAD features.
- Tree redesign.
- Assistant redesign.

## Likely files
- `AvaloniaApp/MainWindow.axaml`
- `AvaloniaApp/Themes/Studio.Light.axaml`
- `AvaloniaApp/Themes/Studio.Dark.axaml`
- `AvaloniaApp/Controls/WebViewportHost.cs`

## Acceptance
1. Build succeeds with 0 errors and 0 warnings.
2. Toolbar remains icon-based and compact.
3. Primitives stay in pop-down workflow.
4. Feature buttons stay directly clickable.
5. View cube/navigation feels integrated with the shell.

## Verifier checklist
- [ ] Build passes cleanly.
- [ ] Toolbar labels do not clip.
- [ ] Primitive menu still works.
- [ ] Feature buttons remain visible and direct.
- [ ] Viewport navigation dock is compact and coherent.
