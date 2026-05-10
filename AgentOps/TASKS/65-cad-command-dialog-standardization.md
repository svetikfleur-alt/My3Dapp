# 65. CAD command dialog standardization

## Goal
Make the core command dialogs feel like one professional CAD application instead of a mix of old forms and temporary shells.

## Scope
- Standardize the visible command dialogs for:
  - Extrude
  - Revolve
  - Sweep
  - Loft
  - Fillet
  - Chamfer
  - Hole
  - Shell
  - Mirror
  - Pattern dialogs where already wired
- Use a shared layout principle:
  - clear title
  - compact subtitle
  - stacked labeled inputs
  - checkboxes for boolean/toggle options
  - consistent OK / Cancel or Apply / Cancel row
  - consistent validation message placement
- Remove obvious raw/debug phrasing.
- Keep the existing working backend bindings intact.

## Required behavior
- Inputs must read clearly at a glance.
- Toggle options should use checkboxes where appropriate instead of improvised controls.
- Numeric fields should align consistently.
- Selection summaries should be readable and compact.
- Dialogs must remain real feature entry points, not decorative shells.

## Out of scope
- New feature backend logic.
- New modeling features.
- Rewriting the entire dialog framework.

## Likely files
- `AvaloniaApp/Dialogs/*.axaml`
- `AvaloniaApp/Dialogs/*.axaml.cs`
- `AvaloniaApp/Themes/Studio.Light.axaml`
- `AvaloniaApp/Themes/Studio.Dark.axaml`

## Acceptance
1. Build succeeds with 0 errors and 0 warnings.
2. Core feature dialogs share one clear visual language.
3. Checkboxes/input fields are used consistently.
4. No dialog regresses into debug-form appearance.
5. Existing feature execution still works.

## Verifier checklist
- [ ] Build passes cleanly.
- [ ] Extrude dialog looks and behaves consistently.
- [ ] Revolve dialog looks and behaves consistently.
- [ ] Numeric feature dialogs match the same structure.
- [ ] Validation remains visible and readable.
- [ ] No backend feature regression.
