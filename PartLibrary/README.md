# PartLibrary

`PartLibrary/` contains the starter maker templates used by the studio.

The goal is to make it easy to extend My3DApp with practical parametric parts that are useful for:

- 3D printing
- mounts and brackets
- electronics standoffs
- fan / enclosure adapters
- cable management

## Current starter templates

- MountingPlate
- Washer
- Spacer
- LBracket
- FanAdapter
- CableClip

Each template folder is intended to describe:

- what the part is
- which parameters drive it
- what validation rules apply
- what geometry/export limitations still exist

## Runtime note

The current MVP runtime registry lives in `AvaloniaApp/Services/MakerTemplateLibrary.cs`.

This folder is the contributor-facing library layout and proposal structure for expanding beyond the MVP.

## Contribution direction

If you add a new template:

1. create a folder under `Templates/`
2. document parameters and intended geometry
3. wire it into the runtime template registry
4. test generation and export
5. document any current limitations honestly
