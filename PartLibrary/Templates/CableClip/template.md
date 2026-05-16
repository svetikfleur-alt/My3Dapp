# Cable Clip

- **ID:** `cable-clip`
- **Category:** Clips & routing
- **Tags:** clip, cable, routing, wiring, maker

## Description

Simple printable clip block for cable management and routing.
Snap-fits around cables and wires to keep them organized along printer frames,
electronics enclosures, and cable trays.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| cableDiameter | Cable Diameter | 6 | 2 | 30 | mm | Target cable or bundle outer diameter |
| clipWidth | Clip Width | 14 | 4 | 80 | mm | Length of the clip body (along cable axis) |
| wallThickness | Wall Thickness | 2.5 | 1 | 12 | mm | Clip wall thickness |
| openingGap | Opening Gap | 4 | 1 | 20 | mm | Entry slot width for snap-fit insertion |

## Geometry notes

Generated as a rectangular block with overall dimensions computed from
cable diameter and wall thickness. A fillet is applied for printability.

MVP note: The snap-fit opening slot is represented as a feature marker.
For tight retention, set opening gap smaller than cable diameter by 10-20%.
Print with the opening facing up for best layer adhesion.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| USB cable clip | cableDiameter=6, clipWidth=12, wallThickness=2, openingGap=4 |
| Wire loom clip | cableDiameter=14, clipWidth=20, wallThickness=3, openingGap=8 |
| Thin wire guide | cableDiameter=3, clipWidth=8, wallThickness=1.5, openingGap=2 |
