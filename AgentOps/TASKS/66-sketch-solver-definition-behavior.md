# 66. Sketch solver and definition behavior pass

## Goal
Strengthen the sketch constraint behavior so the app feels closer to a real small CAD tool, not just a geometry editor with labels.

## Scope
- Improve the lightweight sketch solver so stored constraints re-assert more reliably after edits.
- Tighten the fully-defined / under-defined / over-defined behavior already present.
- Ensure unconstrained sketch geometry remains movable and visually blue.
- Ensure constrained/fully-defined geometry reads dark/black.
- Ensure sketch tree tooltip/status stays accurate:
  - `Sketch not fully defined.`
  - `Sketch fully defined.`
  - `Sketch overdefined.` where applicable
- Reduce obvious cases where constraints exist but do not meaningfully affect behavior.

## Required behavior
- Coincident, Horizontal, Vertical, Equal, Fix, Parallel, Perpendicular, Concentric should affect geometry consistently in supported cases.
- Driving dimensions should keep acting like real drivers.
- Fully defined simple sketches should stop behaving loose.
- Under-defined simple sketches should remain draggable.

## Out of scope
- Full symbolic/professional CAD solver kernel.
- Assembly/mate solving.
- New sketch tools.

## Likely files
- `Engine/CadProjectStore.cs`
- `AvaloniaApp/Services/StudioWorkspaceController.cs`
- `AvaloniaApp/Controls/SoftwareViewportControl.cs`
- `AvaloniaApp/Controls/WebViewportHost.cs`
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs`

## Acceptance
1. Build succeeds with 0 errors and 0 warnings.
2. Simple constrained sketches hold shape better after edits.
3. Under-defined geometry remains blue and movable.
4. Fully defined geometry reads dark and stable.
5. Tree tooltip/status matches actual sketch state.

## Verifier checklist
- [ ] Build passes cleanly.
- [ ] A simple triangle/rectangle can remain under-defined and draggable.
- [ ] Adding supported constraints reduces looseness.
- [ ] Fully defined status appears only when appropriate.
- [ ] No regression in sketch preview/commit flow.
