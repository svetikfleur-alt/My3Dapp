# My3DApp / UMX1 Studio

Early open-source AI-assisted maker CAD studio.

This project is building toward a compact desktop CAD workspace for makers, 3D printing users, and contributors who want to add parametric parts, sketch tools, and AI-assisted workflows without starting from a full enterprise CAD stack.

## Current MVP direction

- Unified Avalonia studio shell
- Part studio workflow with sketch + feature foundation
- Starter maker template library
- STL / OBJ export path
- Integrated AI copilot dock
- Open contributor-oriented architecture

## What works today

- Launches into one CAD studio shell
- Reference planes, feature tree, properties, copilot, and recipe views
- Sketch entry from plane selection
- Basic sketch tools: point, line, rectangle, circle, arc
- Constraint/dimension foundation
- Core feature dialogs: extrude, revolve, sweep, loft, fillet, chamfer, shell, patterns, hole
- Primitive creation and body management
- Export dialog for STL / OBJ
- Starter maker templates through the Templates workspace

## Run

```powershell
dotnet build .\My3DApp.csproj -c Debug -p:StudioUiHost=Avalonia
dotnet run --project .\My3DApp.csproj -c Debug -p:StudioUiHost=Avalonia
```

## Basic flow

1. Open the app.
2. Stay in `Part Studio` for direct CAD work, or switch to `Templates`.
3. Choose a starter maker template and edit the parameters.
4. Generate the part into the active part studio.
5. Refine with sketch / feature tools if needed.
6. Switch to `Prepare` and export STL.

## Template library

Starter templates currently include:

- Mounting Plate
- Washer
- Spacer / Standoff
- L-Bracket
- Fan Adapter Plate
- Cable Clip

See [PartLibrary/README.md](PartLibrary/README.md) and [PartLibrary/TEMPLATE_GUIDE.md](PartLibrary/TEMPLATE_GUIDE.md).

## AI assistant

The copilot is integrated as a secondary dock and workspace, not as the startup screen.

It can:

- suggest local CAD commands
- insert starter recipes
- explain selected features
- help contributors think through next steps

If no provider key is configured, it stays in a clean inactive state.

## Limitations

- Early work in progress. Not production CAD yet.
- Some templates currently generate simplified maker-friendly solids instead of full manufacturing-detail geometry.
- STEP export and advanced booleans are not yet a polished end-user workflow.
- Constraint solving is improving, but it is not yet equivalent to professional commercial CAD.

## Contributing

This repo is intended to be contributor-friendly.

Useful contribution areas:

- add new maker templates
- improve sketch behavior
- extend feature dialogs
- improve export and prepare workflows
- strengthen AI-assisted local command generation

You can also use Codex / Claude / GPT tools to help add templates and open PRs.

## Roadmap

- stronger template geometry and validation
- better prepare/export review
- more robust sketch solver
- face-based sketching and richer feature editing
- more publish-ready template packs for printer mods, brackets, mounts, and enclosures
