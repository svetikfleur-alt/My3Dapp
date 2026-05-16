# PartLibrary

`PartLibrary/` is the contributor-facing documentation and structure for the UMX1 maker template library.

The **runtime registry** lives in [`AvaloniaApp/Services/MakerTemplateLibrary.cs`](../AvaloniaApp/Services/MakerTemplateLibrary.cs).
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

---

## How templates work

Each template is a C# record in `MakerTemplateLibrary.cs` that specifies:

1. **Metadata** — id, display name, category, description, tags
2. **Parameters** — list of named, typed, range-validated values
3. **BuildCommand** — a function that takes the user's parameter values and returns a CAD command string

The app reads the template list on startup, shows it in the Templates workspace,
lets the user edit parameters, and executes the BuildCommand to generate the part.

---

## Adding a new template

1. Add a `MakerTemplateDefinition` entry to `BuildTemplates()` in `MakerTemplateLibrary.cs`
2. Create a folder here: `Templates/MyTemplate/`
3. Add a `template.md` documenting the template (see any existing template for format)
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
2. Write the `MakerTemplateDefinition` C# record
3. Write the `template.md` documentation
4. Generate a test preset

This is a valid and encouraged contribution workflow.
Example prompt:

> "Write a MakerTemplateDefinition for UMX1 for a DIN rail clip.
> Parameters: rail width (35mm default), clip depth (20mm), wall thickness (3mm), screw hole diameter (4mm).
> Use the same pattern as the existing MountingPlate template."
