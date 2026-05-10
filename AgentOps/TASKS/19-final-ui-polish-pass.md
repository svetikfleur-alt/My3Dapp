# 19. Final UI polish pass

## Goal
Bring the studio to a presentable "real engineering desktop tool" baseline after the structural and functional tasks in this cycle land.

This is a dedicated end-of-cycle polish stage. It should not run after every feature task.

## Cadence
- Run this only as a rare final stage for the active queue cycle.
- Do not create duplicate polish tasks while one pending or completed final UI polish item already exists in TASK_QUEUE.md.
- Do not use this task as an excuse to redesign the product or add features.

## Scope
- Walk every visible surface of the app: top toolbar, left feature tree, viewport chrome, right assistant/properties panel, status bar, and active dialogs.
- Tighten alignment, spacing, padding, typography hierarchy, icon consistency, and hover/pressed/disabled states.
- Fix obvious Light/Dark theme inconsistencies.
- Replace placeholder text, lorem ipsum copy, and visible debug labels.
- Confirm dialogs use consistent title/body/button structure.
- Keep sketch entity rendering clean: points, segments, dimensions, and selection markers should read as CAD elements, not generic UI shapes.

## Out of scope
- New CAD features.
- New themes.
- Architecture rewrites.
- Layout-zone redesign. The top/left/center/right studio structure stays.
- Removing working controls because they look imperfect.

## Likely files
- AvaloniaApp/App.axaml
- AvaloniaApp/MainWindow.axaml
- AvaloniaApp/MainWindow.axaml.cs
- AvaloniaApp/Themes/*
- AvaloniaApp/Controls/*
- AvaloniaApp/Dialogs/*
- AvaloniaApp/Resources/*

## Acceptance
1. Build succeeds with 0 errors.
2. Primary tools remain icon-led and compact.
3. Top toolbar groups look coherent and consistent.
4. Feature tree rows align cleanly; default geometry, features, sketches, and bodies remain readable.
5. Viewport remains dominant and CAD-like; no grid or demo geometry is introduced by this task.
6. Right assistant/properties panel remains secondary and not chat-first.
7. Dialogs share consistent spacing, title/header behavior, and button placement.
8. Light and Dark themes both look intentionally styled.
9. No placeholder/debug labels are visible in the main UI.
10. Existing primitive, sketch, feature, tree, and viewport workflows are preserved.

## Verification
- Run `dotnet build --nologo -v minimal`.
- If possible, launch the app and write short visual notes to AgentOps/APP_BEHAVIOR.md or AgentOps/HANDOFF.md.
- Check Light and Dark themes.
- Check toolbar, tree, viewport, right panel, status bar, and currently wired dialogs.
- Do not mark the queue item complete unless the polish is real and behavior remains stable.
