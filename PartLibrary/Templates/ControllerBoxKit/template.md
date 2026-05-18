# Controller Box Kit

- **ID:** `controller-box-kit`
- **Category:** Electronics
- **Tags:** electronics, enclosure, kit, controller, fan, standoff, cable

## Description

ACL-driven printable electronics enclosure kit for maker control boxes and bench projects.
It lays out a matching set of fabrication parts in one studio:

- open-top enclosure body
- matching lid
- four accessory standoffs
- two cable clips
- one fan adapter plate

This is meant to prove the ACL language can drive a practical multi-part generation flow,
not just a single primitive or tiny demo part.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---:|---:|---:|---|---|
| boxWidth | Box Width | 120 | 60 | 300 | mm | Enclosure width |
| boxDepth | Box Depth | 80 | 40 | 240 | mm | Enclosure depth |
| boxHeight | Box Height | 45 | 20 | 180 | mm | Enclosure height |
| wallThickness | Wall Thickness | 3 | 1.5 | 10 | mm | Shared wall thickness |
| lidThickness | Lid Thickness | 3 | 1 | 10 | mm | Cover thickness |
| standoffHeight | Standoff Height | 8 | 3 | 30 | mm | Printed standoff height |
| cableDiameter | Cable Diameter | 6 | 2 | 20 | mm | Cable clip target diameter |
| fanSize | Fan Size | 60 | 40 | 140 | mm | Fan adapter nominal size |

## ACL generation notes

This template is generated from `template.acl`, not from a handwritten C# builder.

The ACL script uses:

- variables
- arithmetic expressions
- reusable functions (`def`)
- `for` loops
- nested template calls
- body moves to lay out multiple printable parts

## Geometry notes

The generated parts are practical MVP geometry:

- the box and lid are ready for simple print-and-fit iteration
- standoffs and cable clips are printable helper accessories
- the fan plate is useful for quick electronics cooling layouts

Some detailed production geometry is still simplified:

- fastener holes and booleans remain limited by the current MVP feature pipeline
- the kit is optimized for printable concept/prototype parts, not final production hardware

## Export

- STL: intended workflow
- OBJ: available if enabled in the current build
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| Compact controller | boxWidth=100, boxDepth=70, boxHeight=40, wallThickness=2.5, lidThickness=2.5, standoffHeight=6, cableDiameter=5, fanSize=40 |
| Bench PSU box | boxWidth=160, boxDepth=110, boxHeight=55, wallThickness=3, lidThickness=3, standoffHeight=10, cableDiameter=8, fanSize=80 |
