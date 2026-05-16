# Washer

- **ID:** `washer`
- **Category:** Disks & spacers
- **Tags:** washer, spacer, round, hardware

## Description

Printable washer or spacer disk with a center bore.
Useful for load distribution, spacing, and DIY hardware replacement.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| outerDiameter | Outer Diameter | 24 | 6 | 120 | mm | Outside diameter of the washer |
| innerDiameter | Inner Diameter | 8 | 0 | 60 | mm | Center bore diameter (0 = solid disk) |
| thickness | Thickness | 2.5 | 0.5 | 25 | mm | Washer thickness |

## Geometry notes

Generates a circular disk extruded to the specified thickness.
Center bore is applied as a lightweight hole feature.

Constraint: inner diameter must be smaller than outer diameter.
The library clamps inner radius to prevent degenerate geometry.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| M5 washer | outerDiameter=10, innerDiameter=5.5, thickness=1 |
| M8 fender washer | outerDiameter=30, innerDiameter=9, thickness=2.5 |
| Printed shim | outerDiameter=20, innerDiameter=0, thickness=0.4 |
