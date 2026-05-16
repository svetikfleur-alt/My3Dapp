# PCB Tray

- **ID:** `pcb-tray`
- **Category:** Electronics
- **Tags:** pcb, tray, sled, electronics, raspberry-pi, arduino

## Description

Parametric PCB tray with walls and standoff support for makers building electronics enclosures.

## Parameters

| Key | Default | Unit | Notes |
|---|---:|---|---|
| boardWidth | 85 | mm | PCB width |
| boardDepth | 56 | mm | PCB depth |
| wallHeight | 8 | mm | Tray wall height |
| wallThickness | 2.5 | mm | Tray wall thickness |
| standoffHeight | 5 | mm | Corner standoff height |
| standoffDiameter | 6 | mm | Corner standoff OD |
| standoffHoleDia | 2.7 | mm | 0 makes solid standoff |

## Geometry notes

Current MVP generator builds a tray shell plus a representative boss/hole workflow. It is useful for concepting and printable prototypes while full corner-feature placement evolves.
