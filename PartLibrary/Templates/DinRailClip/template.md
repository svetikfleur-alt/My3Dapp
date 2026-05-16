# DIN Rail Clip

- **ID:** `din-rail-clip`
- **Category:** Clips & routing
- **Tags:** din, rail, clip, electronics, panel

## Description

Parametric 35mm DIN rail mounting clip for attaching electronics modules to standard
DIN rails in panels and enclosures. Includes optional screw holes for securing components.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| clipLength | Clip Length | 45 | 20 | 120 | mm | Length along the rail |
| railWidth | Rail Width | 35 | 25 | 45 | mm | DIN rail width (standard: 35mm) |
| wallThickness | Wall Thickness | 3 | 1.5 | 8 | mm | Clip body wall thickness |
| lipDepth | Lip Depth | 7 | 4 | 14 | mm | Rail retention lip depth |
| screwHoleDiameter | Screw Hole Dia. | 3.5 | 0 | 6 | mm | Mounting screw hole diameter (0=none) |

## Geometry notes

Generates a rectangular clip body sized for the specified rail width.
The lip depth determines how deeply the clip engages the rail channel.

MVP note: The rail channel cutout and snap mechanism are represented as a
simplified body. For a functional snap-fit, the clip material flexibility
(PETG or TPU recommended) provides retention.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| Standard 35mm DIN | railWidth=35, clipLength=45, wallThickness=3, lipDepth=7 |
| Short 35mm clip | railWidth=35, clipLength=25, wallThickness=2.5, lipDepth=6 |
