# T-Slot Nut

- **ID:** `t-slot-nut`
- **Category:** Fasteners & hardware
- **Tags:** t-nut, t-slot, extrusion, 2020, hardware

## Description

Printable T-nut for standard 2020, 2040, and 3030 aluminum extrusion T-slots.
Used in 3D printer frames, CNC fixtures, and modular maker builds.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| slotWidth | Slot Width | 6 | 4 | 10 | mm | Extrusion slot width (2020=6, 3030=8) |
| nutLength | Nut Length | 20 | 8 | 60 | mm | Nut body length |
| nutHeight | Nut Height | 3 | 1.5 | 8 | mm | Nut body thickness |
| holeDiameter | Thread Hole Dia. | 3.2 | 1.5 | 6 | mm | Center hole diameter |
| flangeWidth | Flange Width | 10 | 6 | 20 | mm | T-flange outer width |

## Geometry notes

Generates the nut body with correct flange width. The T-profile tongue
(smaller dimension to fit the slot) is approximated in the current MVP.
Print in PETG or ABS for best dimensional accuracy in the slot.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| M3 2020 nut | slotWidth=6, nutLength=20, nutHeight=3, holeDiameter=3.2, flangeWidth=10 |
| M5 2020 nut | slotWidth=6, nutLength=25, nutHeight=3.5, holeDiameter=5.2, flangeWidth=10 |
| M5 3030 nut | slotWidth=8, nutLength=25, nutHeight=4, holeDiameter=5.2, flangeWidth=14 |
