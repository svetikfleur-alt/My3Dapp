# UMX1 CAD Language

UMX1 includes an app-owned scripting language for fast part generation, template use, reusable modeling scripts, and AI-assisted CAD workflows.

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

## Real scripting features

UMX1 now supports a compact scripting layer on top of direct commands, recipes, and templates.

### Variables and expressions

```umx1
let width = 80
let height = width / 2 + 10
template mounting-plate width=$width height=$height thickness=${max(4, width / 20)}
```

Supported math:
- `+ - * / %`
- parentheses
- variables
- built-ins: `min`, `max`, `abs`, `round`, `floor`, `ceil`, `clamp`, `pow`, `sqrt`

### Repeat blocks

```umx1
repeat 4
{
  template washer outerDiameter=18 innerDiameter=5 thickness=2
}
```

Loop variables available inside a repeat block:
- `$index` starting at `0`
- `$iteration` starting at `1`

### For loops

```umx1
for i in 0..3
{
  template spacer outerDiameter=${8 + i * 2} innerDiameter=3.2 height=${6 + i * 3}
}
```

Optional step value:

```umx1
for hole in 10..40 step 10
{
  template mounting-plate width=100 height=60 thickness=4 holeMargin=$hole
}
```

### Reusable functions / macros

```umx1
def bracketPlate(width, height, thickness = 4)
{
  select plane top
  start sketch
  rectangle 0 0 $width $height
  finish sketch
  extrude $thickness
}

bracketPlate(80, 40, 5)
bracketPlate(width = 120, height = 60)
```

Macros can call:
- direct CAD commands
- recipes
- templates
- other macros
- loops and variables inside their own block

### Interpolation

You can inject variable or expression values directly into normal command text:

```umx1
let d = 24
circle 0 0 ${d / 2}
extrude ${d / 6}
```

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

- It is still a compact engineering DSL, not a full general-purpose language
- No custom types or user-defined objects yet
- No arrays, dictionaries, or string interpolation beyond numeric script expansion
- No conditionals yet (`if/else` is not implemented)
- Template geometry builders still route through the current MVP feature pipeline

## Next upgrades

- named presets
- conditionals and comparisons
- reusable part packs and importable script modules
- external recipe files
- stronger AI-assisted script generation and repair
