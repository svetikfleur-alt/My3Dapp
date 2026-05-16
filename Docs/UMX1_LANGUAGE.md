# UMX1 CAD Language

UMX1 includes a lightweight app-owned command language for fast part generation, template use, and AI-assisted modeling.

It is intentionally small, readable, and contributor-friendly.

## Core idea

The language expands into the existing CAD command pipeline:

`UMX1 script -> recipes/templates -> CAD commands -> studio actions`

That means contributors can add new reusable part logic without redesigning the shell.

## Supported layers

### Direct commands

```text
select plane top
start sketch
rectangle 0 0 80 50
finish sketch
extrude 4
```

### Recipes

```text
recipe bracket 60 40 50 4
recipe shelled-box 90 60 40 3
recipe boss 6 12
```

### Template invocations

```text
template mounting-plate width=80 height=50 thickness=4 holeCount=4
template pcb-tray boardWidth=85 boardDepth=56 wallHeight=8
part din-rail-clip railWidth=35 clipHeight=28 wallThickness=3
make template box-enclosure innerWidth=100 innerDepth=70 wallThickness=3
```

The script layer expands template calls into a sequence of executable CAD commands.

## Script envelopes

The parser also accepts simple wrapped scripts:

```umx1
program
{
  template mounting-plate width=100 height=60 thickness=5
  fillet 2
}
```

Comments are allowed with `#` or `//`.

## Contributor model

Templates are defined by:

1. `PartLibrary/Templates/<TemplateName>/template.json`
2. a documented `builder` id
3. a command builder in `MakerTemplateLibrary.cs`

This keeps the public template surface external and readable while preserving a stable generation backend.

## Current limitations

- It is still a compact engineering DSL, not a full general-purpose programming language yet
- No variables, loops, or user-defined functions yet
- Template geometry builders still route through the current MVP feature pipeline

## Next upgrades

- named presets
- variables / simple expressions
- reusable part packs
- external recipe files
- richer AI-assisted script generation
