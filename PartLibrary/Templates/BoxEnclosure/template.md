# Box Enclosure

- **ID:** `box-enclosure`
- **Category:** Enclosures
- **Tags:** box, enclosure, case, electronics, housing

## Description

Parametric rectangular enclosure shell for electronics project boxes,
sensor housings, and general-purpose containers.
The shell is generated with the correct interior dimensions and optional
rounded corners for a polished look.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| innerWidth | Inner Width | 80 | 20 | 300 | mm | Interior cavity width |
| innerHeight | Inner Height | 50 | 15 | 200 | mm | Interior cavity height |
| innerDepth | Inner Depth | 40 | 15 | 200 | mm | Interior cavity depth |
| wallThickness | Wall Thickness | 3 | 1.5 | 10 | mm | Shell wall thickness |
| cornerRadius | Corner Radius | 3 | 0 | 15 | mm | Exterior corner rounding (0=sharp) |

## Geometry notes

Generates a solid box extruded to the outer dimensions, then applies
a shell operation to create the hollow interior. The shell is open on top.

For a lid, generate a second Box Enclosure with `innerHeight` = desired lid height
and the same width/depth. Print separately and stack.

MVP note: Lid rails/snaps and cable entry holes require manual feature addition
after generation.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| Arduino Nano box | innerWidth=55, innerHeight=25, innerDepth=30, wallThickness=2.5, cornerRadius=2 |
| Raspberry Pi 4 case | innerWidth=90, innerHeight=35, innerDepth=62, wallThickness=3, cornerRadius=4 |
| Sensor housing | innerWidth=40, innerHeight=20, innerDepth=30, wallThickness=2, cornerRadius=2 |
