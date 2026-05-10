# 06. Top panel polish round 2

## Goal
Continue from the v1 polish (hover styles, label fixes already merged) to a visually consistent toolbar: button density, icon alignment, separators, mode indicator, tooltips, missing-icon resolution.

## Scope
- Replace remaining text-glyph buttons (Export `↓`) with proper icons; if no asset exists, add minimal SVGs to `Assets/Icons/`.
- Consistent button width / padding across groups.
- Logical separators between groups (File | Edit | Sketch | 3D | View | Assistant).
- Mode indicator text element ("Modeling" / "Sketch on <plane>" / "Assembly" — last is placeholder).
- Tooltips on every command button with name + shortcut.
- Theme parity: every style added to Light has a Dark counterpart.
- Active-tool button highlighted while a sketch tool is selected.

## Out of scope
- New commands (no behavior changes; visual / consistency only).
- Tooltip rich content beyond plain text.

## Files likely involved
- `AvaloniaApp/MainWindow.axaml` — toolbar markup, separators, tooltips.
- `AvaloniaApp/Themes/Studio.Light.axaml`, `Studio.Dark.axaml` — button styles.
- `Assets/Icons/` — add export.svg and any missing icons.
- `AvaloniaApp/Services/SvgIconLoader.cs` (or equivalent) — load new icons.

## Expected behavior (acceptance)
1. No raw text glyphs remain in toolbar buttons; all use icons.
2. Button widths consistent within groups; vertical alignment matches.
3. Group separators visible in both themes.
4. Mode indicator updates with current mode; visible in both themes.
5. Tooltips on every command button include shortcut hint where one exists.
6. Active sketch tool button is visibly toggled while in use.
7. Hover styling identical in both themes.

## Notes / hints
- Reuse existing CommandButton class; do not introduce a parallel one.
- Use pre-existing accent palette; no new color tokens.
- Reference: `References/zoo/blocks/01_top_bar.jpg`, `References/onshape/blocks/01_top_bar.jpg`.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Switch Light <-> Dark: toolbar visually consistent in both.
- [ ] Hover Export -> icon shown, tooltip says "Export (Ctrl+E)" or similar.
- [ ] Mode indicator visible and updates on Sketch entry.
- [ ] No regression in Commands button or other v1 fixes.

## User addendum (2026-04-27)
Top panel must read like **Onshape's command bar** — that is the explicit baseline the user expects. Reference: `References/onshape/blocks/01_top_bar.jpg`. Specifically:
- Consistent button height + padding across all command groups.
- Group separators with subtle vertical dividers.
- Icons monochrome, single weight, single style (no mixed glyph sources).
- Tooltips on every command with the keyboard shortcut shown in parentheses.
- Hover + pressed states present in both Light and Dark themes.
- No oversized buttons or SaaS-style coloring.
- Active mode indicator (Sketch vs 3D vs Assembly when present).

Don't ship a partial. Match Onshape feel.
