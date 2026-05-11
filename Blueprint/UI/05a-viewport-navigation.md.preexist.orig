# Viewport Navigation — Canonical Bindings

_Source of truth for all future tasks referencing viewport camera controls._

## Mouse bindings

| Gesture | Action |
|---|---|
| Left-drag | Orbit (rotate around scene centre) |
| Middle-drag | Pan (translate camera + target along screen plane) |
| Right-drag | Pan (same as middle-drag) |
| Scroll wheel | Zoom toward cursor |
| Left-click (body/plane) | Select entity |
| Right-click | Context menu (Delete / Focus Camera) |

## Keyboard shortcuts

| Key | Action |
|---|---|
| F | Fit all visible bodies into view |
| Home | Reset camera to default position |
| Escape | Cancel active sketch step; close context menu |

## Sketch mode overrides

When the application is in **Sketch** mode:
- Left-click dispatches to sketch point placement, **not** to orbit.
- Orbit (`enableRotate`) is disabled so dragging does not fight sketch input.
- Pan (middle-drag) and scroll-zoom remain active.
- Cursor shows `crosshair`.

## Cursor feedback

| State | Cursor |
|---|---|
| Hovering entity (non-sketch) | `pointer` |
| Middle/right button held | `grabbing` |
| Sketch mode | `crosshair` |
| Default | `default` |

## Walk mode

**Deferred.** No UI entry point exists. Would require a separate first-person
controller (key-capture loop + mouse-look) that conflicts with sketch and
transform interaction. Track as a future task.

## Implementation notes

- Controls: Three.js `OrbitControls` — `mouseButtons` configured as `{ LEFT: ROTATE, MIDDLE: PAN, RIGHT: PAN }`.
- Fit function: `frameBounds(getSceneBodyBounds())` / `applyDefaultView()`.
- Orbit suppression in sketch mode: `controls.enableRotate = false` on `setScene()`.
- All navigation is JS-side; no postMessage round-trip for camera moves.
