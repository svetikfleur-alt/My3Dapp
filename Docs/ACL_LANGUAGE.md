# ACL Feature Language

ACL is the embedded scripting language inside My3DApp.

Its role is not to rename the studio. The studio remains `My3DApp` while ACL is the language used for:
- custom features
- parametric part generation
- reusable maker macros
- AI-assisted scripted modeling flows

It is intentionally compact, readable, and contributor-friendly.

## Core idea

ACL expands into the existing CAD command pipeline:

`ACL script -> recipes/templates -> CAD commands -> studio actions`

That means contributors can add reusable feature logic and generated part flows without redesigning the shell.

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

The scripting layer expands template calls into executable CAD command sequences.

## Real scripting features

ACL supports a compact scripting layer on top of direct commands, recipes, and templates.

### Variables and expressions

```acl
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

```acl
repeat 4
{
  template washer outerDiameter=18 innerDiameter=5 thickness=2
}
```

Loop variables available inside a repeat block:
- `$index` starting at `0`
- `$iteration` starting at `1`

### For loops

```acl
for i in 0..3
{
  template spacer outerDiameter=${8 + i * 2} innerDiameter=3.2 height=${6 + i * 3}
}
```

Optional step value:

```acl
for hole in 10..40 step 10
{
  template mounting-plate width=100 height=60 thickness=4 holeMargin=$hole
}
```

### Reusable functions / macros

```acl
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

```acl
let d = 24
circle 0 0 ${d / 2}
extrude ${d / 6}
```

## Script envelopes

The parser also accepts simple wrapped scripts:

```acl
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

- conditionals and comparisons
- reusable part packs and importable script modules
- external recipe files
- richer feature authoring constructs
- stronger AI-assisted script generation and repair
