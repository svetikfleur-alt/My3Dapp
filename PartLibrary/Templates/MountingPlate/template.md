# Mounting Plate

- **ID:** `mounting-plate`
- **Category:** Plates & mounts
- **Tags:** plate, mount, panel, maker

## Description

Rectangular maker plate with optional symmetric corner hole pattern.
Useful for electronics panel mounts, 3D printer frame adapters, and general-purpose
flat mounting surfaces.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| width | Width | 80 | 20 | 240 | mm | Overall plate width |
| height | Height | 50 | 20 | 240 | mm | Overall plate height |
| thickness | Thickness | 4 | 1 | 25 | mm | Plate thickness |
| cornerRadius | Corner Radius | 4 | 0 | 30 | mm | Rounded corner radius (0 = sharp) |
| holeDiameter | Hole Diameter | 5 | 0 | 20 | mm | Fastener hole diameter (0 = no holes) |
| holeMargin | Hole Margin | 10 | 2 | 40 | mm | Hole center offset from edges |
| holeCount | Hole Count | 4 | 0 | 4 | count | 0 = none, 2 = center row, 4 = corners |

## Geometry notes

Generates a rectangular extruded plate. Corner radius and holes are applied as
lightweight operations. Holes are feature operations, not full boolean cutouts.

**MVP note:** The outer plate shape is correct and printable. Holes are modeled
as feature markers. For critical fit applications, verify hole geometry in your slicer.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| Raspberry Pi 4 mount | width=85, height=56, thickness=3, holeDiameter=2.7, holeMargin=3.5, holeCount=4 |
| Standard panel | width=120, height=80, thickness=4, holeDiameter=5, holeMargin=12, holeCount=4 |
| Simple base plate | width=100, height=60, thickness=6, holeDiameter=0, holeCount=0 |
