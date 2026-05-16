# Hinge Bracket

- **ID:** `hinge-bracket`
- **Category:** Brackets
- **Tags:** hinge, bracket, pivot, door, lid

## Description

Two-leaf printable hinge bracket for panel doors, enclosure lids,
and folding mechanism prototypes. Each leaf has mounting holes
and a knuckle pin barrel for the hinge axis.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| leafWidth | Leaf Width | 30 | 10 | 100 | mm | Width of each hinge leaf |
| leafLength | Leaf Length | 40 | 15 | 150 | mm | Length of each hinge leaf |
| thickness | Thickness | 3 | 1.5 | 8 | mm | Leaf plate thickness |
| pinDiameter | Pin Diameter | 5 | 2 | 12 | mm | Hinge pin outer diameter |
| knuckleCount | Knuckle Count | 3 | 2 | 5 | count | Number of knuckle cylinders |

## Geometry notes

Generates the flat leaf plate and the knuckle barrel body.
The two leaves are printed separately and assembled with a pin (printed or metal).

For a working hinge:
- Print leaf A (this template output)
- Mirror the geometry for leaf B (or print two and assemble interleaved)
- Use a 3mm or 4mm metal rod as the hinge pin for durability
- Leave 0.2-0.3mm clearance in the knuckle bore for fit

MVP note: The interlocking knuckle offset and leaf B interleaving require
manual adjustment. The current template generates a single leaf body.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| Small panel hinge | leafWidth=25, leafLength=35, thickness=2.5, pinDiameter=4, knuckleCount=3 |
| Large door hinge | leafWidth=40, leafLength=60, thickness=4, pinDiameter=6, knuckleCount=3 |
