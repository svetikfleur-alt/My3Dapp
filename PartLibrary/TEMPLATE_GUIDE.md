# Template Contributor Guide

This guide explains how to add a new maker template to UMX1.

Templates are parametric part definitions that users can select, configure, and generate
directly from the Templates workspace. They are the easiest way to contribute useful maker
parts without deep CAD kernel knowledge.

---

## What makes a good template

A template should:

- Generate a **useful, printable** part (not just a visual placeholder)
- Have **sensible defaults** that work out of the box
- Have **validated parameter ranges** that prevent degenerate geometry
- Be **documented honestly** — if it generates simplified geometry, say so

Good template candidates:
- Printer brackets and mounts
- Electronics standoffs and spacers
- Fan plates and adapters
- Cable clips and routing parts
- Panel mounts and enclosure parts
- DIN rail accessories
- Simple jigs and fixtures

---

## Step-by-step: adding a template

### 1. Define the C# record

Open [`AvaloniaApp/Services/MakerTemplateLibrary.cs`](../AvaloniaApp/Services/MakerTemplateLibrary.cs)
and add a new `MakerTemplateDefinition` to the list in `BuildTemplates()`.

```csharp
new(
    "my-template",            // unique lowercase-kebab-case id
    "My Template",            // display name shown in UI
    "Category Name",          // groups templates in the list
    "One-line description.",  // shown below the template name
    ["tag1", "tag2"],         // searchable keywords
    [
        // Parameters: key, display name, default, min, max, unit, description
        P("width",     "Width",     60, 10, 300, "mm", "Overall width"),
        P("height",    "Height",    40, 10, 300, "mm", "Overall height"),
        P("thickness", "Thickness",  4,  1,  30, "mm", "Wall thickness"),
    ],
    "Preview summary shown in the template card.",
    values =>
    {
        // Extract parameter values (returns 0.0 if key is missing)
        var w = V(values, "width");
        var h = V(values, "height");
        var t = V(values, "thickness");

        // Return a CAD command string
        return $"select plane top; start sketch; rectangle 0 0 {N(w)} {N(h)}; finish sketch; extrude {N(t)}";
    })
```

### 2. Parameter helpers

The library provides three helpers you can use inside `BuildTemplates()`:

| Helper | Purpose |
|---|---|
| `P(key, name, default, min, max, unit, desc)` | Creates a parameter definition |
| `V(values, key)` | Gets a parameter value (returns 0.0 if missing) |
| `N(double)` | Formats a number for a CAD command string (invariant culture, 3 decimal places) |

### 3. CAD command syntax

The `BuildCommand` function returns a string of semicolon-separated CAD commands.
Common patterns:

```
# Simple plate
select plane top; start sketch; rectangle 0 0 {w} {h}; finish sketch; extrude {t}

# Cylinder
select plane top; start sketch; circle 0 0 radius {r}; finish sketch; extrude {h}

# Add a hole after extruding (lightweight, not a boolean)
...; hole depth {d}

# Add a chamfer
...; chamfer {c}

# L-bracket recipe
recipe bracket {w} {d} {h} {t}
```

### 4. Create the template.md

Create a file at `PartLibrary/Templates/MyTemplate/template.md`:

```markdown
# My Template

- **ID:** `my-template`
- **Category:** Category Name
- **Tags:** tag1, tag2

## Description

What this part is and what it is useful for.

## Parameters

| Key | Display Name | Default | Min | Max | Unit | Notes |
|---|---|---|---|---|---|---|
| width | Width | 60 | 10 | 300 | mm | Overall width |
| height | Height | 40 | 10 | 300 | mm | Overall height |
| thickness | Thickness | 4 | 1 | 30 | mm | Wall thickness |

## Geometry notes

What geometry this template generates, and any current limitations.
Example: "Generates a simple rectangular plate. Holes are lightweight operations,
not full boolean subtracts."

## Export

- STL: works
- OBJ: works
- STEP: not yet supported

## Example presets

| Name | Parameters |
|---|---|
| Small bracket | width=40, height=30, thickness=3 |
| Large bracket | width=100, height=60, thickness=5 |
```

### 5. Build and test

```powershell
dotnet build .\My3DApp.csproj -c Debug
```

Then run the app, go to the Templates tab, select your template, edit the parameters, and click Generate.
Check:
- Part appears in the viewport and feature tree
- Parameters within the defined range don't crash the app
- Export STL produces a non-empty file

### 6. Submit a PR

PRs adding templates are welcome. Include:
- The C# changes to `MakerTemplateLibrary.cs`
- The `template.md` documentation file
- A brief description of what the template generates

---

## Parameter design guidelines

**Prefer parameters that makers actually edit:**
- overall dimensions (width, height, depth, diameter)
- wall thickness
- hole diameter and count
- spacing and margin values
- chamfer / fillet amounts

**Always define min/max:**
- Minimum should prevent degenerate geometry (e.g., min wall thickness = 1mm)
- Maximum should be a sensible upper bound (e.g., max width = 500mm)
- Default should work immediately without editing

**Use consistent units:**
- All linear dimensions in mm
- Angles in degrees
- Counts as unitless integers

---

## Geometry limitations (current MVP)

Some advanced geometry is not yet fully supported:

- **Hole booleans** — "hole" commands add a feature record but may not produce a full boolean cutout in all cases. If your template needs real through-holes, document this honestly.
- **Complex booleans** — union/subtract/intersect work for simple cases but can fail for complex overlapping bodies.
- **Fillets on templates** — fillet after extrude works in most cases; complex edge selection is limited.
- **STEP export** — not yet functional; use STL or OBJ.

If your template uses simplified geometry, add a note to `template.md`:

```markdown
## Geometry notes

> **MVP note:** Holes are generated as lightweight operations, not full boolean cutouts.
> The outer body shape is correct for 3D printing. Drill or reprint holes as needed.
```

---

## AI-assisted authoring

You can prompt Claude or GPT to write templates for you:

> "Write a MakerTemplateDefinition for UMX1 for a DIN rail clip.
> Parameters: rail width (35mm default, min 10, max 60), clip depth (20mm, min 10, max 60), wall thickness (3mm, min 1, max 10).
> Follow the same structure as the MountingPlate template in MakerTemplateLibrary.cs."

This is a valid and encouraged contribution workflow.
The AI output will need minor review and testing before submission.
