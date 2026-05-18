# PartLibrary

`PartLibrary/` is the contributor-facing documentation and structure for the My3DApp maker template library.

The **runtime registry** is manifest-driven:
- public manifests live in `Templates/*/template.json`
- runtime loading and builder mapping live in [`AvaloniaApp/Services/MakerTemplateLibrary.cs`](../AvaloniaApp/Services/MakerTemplateLibrary.cs)

This folder documents each template for contributors and reviewers.

---

## Current starter templates

| Template | ID | Category | Key parameters |
|---|---|---|---|
| [Mounting Plate](Templates/MountingPlate/template.md) | `mounting-plate` | Plates & mounts | width, height, thickness, corner radius, hole count |
| [Washer](Templates/Washer/template.md) | `washer` | Disks & spacers | outer diameter, inner diameter, thickness |
| [Spacer / Standoff](Templates/Spacer/template.md) | `spacer` | Disks & spacers | OD, ID, height, chamfer |
| [L-Bracket](Templates/LBracket/template.md) | `l-bracket` | Brackets | width, height, depth, thickness, holes |
| [Fan Adapter Plate](Templates/FanAdapter/template.md) | `fan-adapter` | Plates & mounts | fan size, thickness, screw holes, center opening |
| [Cable Clip](Templates/CableClip/template.md) | `cable-clip` | Clips & routing | cable diameter, clip width, wall thickness, gap |
| [Simple Box](Templates/SimpleBox/template.md) | `simple-box` | Enclosures | width, depth, height, wall thickness, top mode |
| [Lid / Cover](Templates/Lid/template.md) | `lid` | Enclosures | width, depth, thickness, lip height, tolerance |
| [DIN Rail Clip](Templates/DinRailClip/template.md) | `din-rail-clip` | Mounts & fixtures | rail width, clip height, lip depth |
| [T-Slot Nut](Templates/TSlotNut/template.md) | `t-slot-nut` | Fasteners & hardware | slot width, nut length, hole |
| [Box Enclosure](Templates/BoxEnclosure/template.md) | `box-enclosure` | Enclosures | inner size, wall thickness, corner radius |
| [Hinge Bracket](Templates/HingeBracket/template.md) | `hinge-bracket` | Brackets | leaf size, thickness, pin diameter |
| [PCB Tray](Templates/PcbTray/template.md) | `pcb-tray` | Electronics | board size, wall height, standoffs |

---

## How templates work

Each template is an external manifest plus a runtime builder mapping:

1. **Metadata** — id, display name, category, description, tags
2. **Parameters** — list of named, typed, range-validated values
3. **Builder id** — maps the manifest to a generation function that returns a CAD command string

The app reads the template list on startup, shows it in the Templates workspace,
lets the user edit parameters, and executes the mapped command builder to generate the part.

---

## Adding a new template

1. Add `template.json` to `Templates/MyTemplate/`
2. Create or update `template.md`
3. Map the builder id in `MakerTemplateLibrary.cs`
4. Run `dotnet build` and verify it compiles
5. Test: select the template in the UI, edit parameters, generate, check the part
6. Export STL and verify the output is sensible
7. Submit a PR

See [TEMPLATE_GUIDE.md](TEMPLATE_GUIDE.md) for full contributor instructions.

---

## Folder structure convention

```
Templates/
  MyTemplate/
    template.md          # required — metadata, parameters, limitations
    preview.png          # optional — screenshot of a generated example
    presets.json         # optional — named parameter presets for common variants
```

---

## AI-assisted template authoring

You can describe a part in natural language to Claude / Codex / GPT and ask it to:

1. Define the parameters
2. Write the `template.json` manifest
3. Map the builder id
4. Write the `template.md` documentation
5. Generate a test preset

This is a valid and encouraged contribution workflow.
Example prompt:

> "Write a template.json and builder mapping for My3DApp for a DIN rail clip.
> Parameters: rail width (35mm default), clip depth (20mm), wall thickness (3mm), screw hole diameter (4mm).
> Use the same pattern as the existing MountingPlate manifest and builder."
