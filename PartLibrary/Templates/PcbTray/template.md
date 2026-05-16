# PCB Tray

- **ID:** `pcb-tray`
- **Category:** Electronics
- **Tags:** pcb, tray, sled, electronics, raspberry-pi, arduino

## Description

Parametric PCB mounting tray with corner standoffs for single-board computers,
microcontroller shields, and custom PCBs.
The tray has a floor, perimeter walls, and four corner standoffs that
raise the board off the floor and accept mounting screws.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| boardWidth | Board Width | 85 | 30 | 200 | mm | PCB width |
| boardDepth | Board Depth | 56 | 25 | 200 | mm | PCB depth |
| wallHeight | Wall Height | 8 | 3 | 40 | mm | Tray wall height |
| wallThickness | Wall Thickness | 2.5 | 1.5 | 8 | mm | Tray wall thickness |
| standoffHeight | Standoff Height | 5 | 2 | 20 | mm | Corner standoff height above floor |
| standoffDiameter | Standoff Diameter | 6 | 3 | 15 | mm | Corner standoff outer diameter |
| standoffHoleDia | Standoff Hole Dia. | 2.7 | 0 | 5 | mm | Standoff center hole (0=solid post) |

## Common board sizes

| Board | Width | Depth |
|---|---|---|
| Raspberry Pi 4/5 | 85 | 56 |
| Arduino Uno | 68.6 | 53.3 |
| Arduino Nano | 45 | 18 |
| ESP32 DevKit | 54 | 28 |
| Raspberry Pi Pico | 51 | 21 |

## Geometry notes

Generates a tray shell and four corner standoffs. Standoff positions are
at the board corners offset inward by the standoff radius and wall thickness.

For common boards, set board dimensions slightly larger (1-2mm) than the actual
PCB to allow easy insertion and removal. Verify standoff positions against
your specific board's mounting hole layout.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| Raspberry Pi 4 tray | boardWidth=85, boardDepth=56, wallHeight=10, wallThickness=2.5, standoffHeight=5, standoffDiameter=6, standoffHoleDia=2.7 |
| Arduino Uno tray | boardWidth=70, boardDepth=55, wallHeight=8, wallThickness=2.5, standoffHeight=4, standoffDiameter=6, standoffHoleDia=2.7 |
| ESP32 DevKit tray | boardWidth=56, boardDepth=30, wallHeight=6, wallThickness=2, standoffHeight=3, standoffDiameter=5, standoffHoleDia=2.7 |
