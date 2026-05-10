# 68. Copilot structured CAD assistance pass

## Goal
Make the assistant panel more useful as a small CAD copilot without turning it into a generic chat app.

## Scope
- Keep the assistant panel secondary.
- Improve the inactive/configured states so they stay clean.
- Strengthen structured action suggestions using current CAD context:
  - selected plane/body/profile
  - active sketch mode / 3D mode
  - recent feature actions
- Prefer compact actionable assistance over noisy freeform chatter.
- Keep provider/model selectors compact.

## Required behavior
- If unconfigured, the panel remains quiet and intentional.
- If configured, the panel should reflect the current CAD context more clearly.
- Suggestions should feel like CAD help, not generic chat filler.
- The panel must not dominate the studio.

## Out of scope
- Full remote AI backend redesign.
- Chat-first UX.
- New providers/packages.

## Likely files
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs`
- `AvaloniaApp/MainWindow.axaml`
- `AvaloniaApp/Themes/Studio.Light.axaml`
- `AvaloniaApp/Themes/Studio.Dark.axaml`

## Acceptance
1. Build succeeds with 0 errors and 0 warnings.
2. Assistant remains secondary and clean.
3. Structured CAD-oriented suggestions improve when context exists.
4. Unconfigured state stays compact and non-noisy.

## Verifier checklist
- [ ] Build passes cleanly.
- [ ] Assistant panel still renders properly.
- [ ] Unconfigured state is quiet.
- [ ] Context-aware assistance is more CAD-specific where available.
