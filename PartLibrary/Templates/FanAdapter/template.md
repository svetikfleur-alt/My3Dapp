# Fan Adapter Plate

- **ID:** `fan-adapter`
- **Category:** Plates & mounts
- **Tags:** fan, adapter, plate, cooling, printer

## Description

Square adapter plate sized for common PC and 3D printer fan footprints.
Supports 40mm, 60mm, 80mm, and 120mm standard fan sizes.
Includes screw holes at the standard fan bolt pattern and a center airflow opening.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| fanSize | Fan Size | 80 | 40 | 140 | mm | Nominal fan body size (standard: 40, 60, 80, 120) |
| thickness | Thickness | 3 | 1 | 20 | mm | Plate thickness |
| screwHoleDiameter | Screw Hole Diameter | 4.5 | 0 | 12 | mm | Fan screw hole diameter (0 = no screw holes) |
| centerOpeningDiameter | Center Opening | 60 | 10 | 120 | mm | Center airflow opening diameter |

## Geometry notes

Generates a square plate with four corner screw holes and a center circular opening.
Corner holes and center opening are applied as lightweight feature operations.

Standard fan bolt patterns (center-to-center): 40mm=32mm, 60mm=50mm, 80mm=71.5mm, 120mm=105mm.

MVP note: Hole positions use an approximation based on fanSize.
Verify against your specific fan before final print.

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| 80mm standard fan | fanSize=80, thickness=3, screwHoleDiameter=4.5, centerOpeningDiameter=70 |
| 120mm exhaust | fanSize=120, thickness=4, screwHoleDiameter=4.5, centerOpeningDiameter=110 |
| 40mm extruder fan | fanSize=40, thickness=2, screwHoleDiameter=3, centerOpeningDiameter=32 |
