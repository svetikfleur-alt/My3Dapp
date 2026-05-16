# Spacer / Standoff

- **ID:** `spacer`
- **Category:** Disks & spacers
- **Tags:** spacer, standoff, hardware, electronics

## Description

Round standoff or spacer column for electronics mounting, panel spacing,
and PCB standoffs. Optionally chamfered on the top edge.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| outerDiameter | Outer Diameter | 12 | 4 | 80 | mm | Outside diameter of the spacer |
| innerDiameter | Inner Diameter | 4 | 0 | 40 | mm | Center bore diameter (0 = solid post) |
| height | Height | 16 | 2 | 120 | mm | Spacer height / standoff length |
| chamfer | Chamfer | 0.5 | 0 | 8 | mm | Top edge chamfer amount (0 = none) |

## Geometry notes

Generates a cylinder extruded to the specified height.
Center bore and chamfer are applied as lightweight operations.

Good for PCB standoffs, panel spacers, and 3D printer Z-axis stops.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| M3 standoff 10mm | outerDiameter=6, innerDiameter=3.2, height=10, chamfer=0.3 |
| M4 standoff 20mm | outerDiameter=8, innerDiameter=4.2, height=20, chamfer=0.5 |
| Solid post 5mm | outerDiameter=10, innerDiameter=0, height=5, chamfer=0 |
