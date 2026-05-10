# 17. Render quality pass — viewport visuals

## Goal
Bring the viewport closer to "looks like a tool, not a demo": antialiasing, line weights, sketch-vs-feature visual differentiation, hover/selection palette consistency.

## Scope
- Antialiasing on the WebView2 Three.js renderer (MSAA / FXAA where available).
- Line weights tuned: feature edges thicker than sketch entities; construction lines lighter / dashed.
- Sketch entity color palette distinct from solid edge color (and theme-aware).
- Hover / selection palette: hover = soft accent tint, selected = saturated accent, both readable in Light + Dark.
- Background gradient subtle; grid (if present) uses softer color.
- Anti-z-fighting tweak for coplanar sketch + plane.

## Out of scope
- New shaders / PBR.
- Hidden-line / outline-only modes (separate viewport polish task already covers display modes).
- View Cube (separate task).

## Files likely involved
- `AvaloniaApp/Controls/WebViewportHost.cs` — JS bridge / renderer config.
- Embedded JS / HTML resources for the WebView2 viewport.
- `AvaloniaApp/Themes/Studio.Light.axaml`, `Studio.Dark.axaml` — palette tokens consumed by the JS via theme messages.

## Expected behavior (acceptance)
1. Antialiasing visibly on (Box edges no longer pixelated).
2. Sketch lines and feature edges visually distinct (thickness + color).
3. Hover and selection both readable in Light and Dark themes.
4. No z-fighting between active plane and sketch entities.
5. Switching theme updates viewport palette without restart.

## Notes / hints
- Keep performance acceptable on integrated GPUs — don't enable expensive post FX by default.
- Reference: `References/onshape/blocks/04_viewport.jpg`, `References/zoo/blocks/05_viewport.jpg`.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Take a viewport screenshot, compare against References for line weight / palette.
- [ ] Switch theme — no flash, palette updates.
- [ ] FPS not visibly worse on a moderate model.
