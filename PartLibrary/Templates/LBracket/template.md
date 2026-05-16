# L-Bracket

- **ID:** `l-bracket`
- **Category:** Brackets
- **Tags:** bracket, support, angle, mount

## Description

General-purpose L-shaped maker bracket built from two joined rectangular plates.
Useful for right-angle mounts, frame joints, shelf brackets, and structural supports.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| width | Width | 60 | 10 | 200 | mm | Bracket cross-section width |
| height | Height | 50 | 10 | 200 | mm | Upright plate height |
| depth | Depth | 40 | 10 | 200 | mm | Base plate depth |
| thickness | Thickness | 4 | 1 | 30 | mm | Wall thickness of both plates |
| holeDiameter | Hole Diameter | 5 | 0 | 18 | mm | Mounting hole diameter (0 = no holes) |
| holeCount | Hole Count | 2 | 0 | 4 | count | Number of mounting holes (0, 2, or 4) |

## Geometry notes

Generated using the `recipe bracket` command which creates the L-shape as a
compound solid. The junction between the two plates is a clean right-angle joint.

Holes are applied as feature operations after the base bracket shape is created.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| Small shelf bracket | width=40, height=40, depth=30, thickness=3, holeDiameter=4, holeCount=2 |
| Frame corner | width=60, height=60, depth=40, thickness=5, holeDiameter=5, holeCount=4 |
| Heavy-duty mount | width=80, height=80, depth=60, thickness=8, holeDiameter=8, holeCount=4 |
