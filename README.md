<<<<<<< ours
# My3DApp — AI Maker CAD Studio

> **Early work in progress. Not production CAD yet.**
> This is an honest early-access open-source project. It runs, it creates parts, it exports STL.
> It is not SolidWorks or Onshape. It is a foundation people can build on.

An open-source, AI-assisted desktop CAD studio for makers, 3D printing users, and contributors
who want a compact but real parametric workflow — without the weight of an enterprise tool.

Built with .NET 10, Avalonia UI, and WebView2 for 3D rendering.

---

## What this is

A small, serious desktop CAD studio focused on:

- **Maker workflows** — brackets, spacers, mounts, adapters, clips
- **Parametric part templates** — pick a template, edit dimensions, generate the part
- **Sketch-based CAD** — sketch planes, 2D tools, extrude to solid
- **AI Copilot** — integrated assistant dock, not a separate chatbot app
- **STL/OBJ export** — so you can actually print what you design
- **Open contributor model** — designed to be extended with new templates and features

This is not a mockup or a UI demo. The sketch tools work, the templates generate real bodies,
the export produces files you can send to a slicer.

---

## Requirements

- Windows 10/11 (WebView2 is Windows-only for now)
- .NET 10 SDK
- WebView2 Runtime (usually pre-installed on Windows 11)

---

## Run

```powershell
# Build
dotnet build .\My3DApp.csproj -c Debug

# Run
dotnet run --project .\My3DApp.csproj -c Debug
```

Or open `My3DApp.sln` in Visual Studio 2022 / Rider and press F5.

---

## What works today

### Studio shell
- Unified Avalonia CAD shell — no split startup, no separate landing page
- Top bar: document name, save/open, undo/redo, mode badge, theme toggle, command palette (Ctrl+K)
- Grouped toolbar: 3D primitives, sketch tools, feature tools, export
- Left panel: feature/project tree with Origin, Planes, Sketches, Features, Bodies
- Center: 3D viewport via WebView2 + Three.js (orbit, pan, zoom, focus)
- Right panel: Properties inspector, AI Copilot dock, Recipe view
- Bottom: workspace tab bar (Part Studio | Sketch | Templates | Prepare | AI Chat | +)
- Bottom: part studio tab bar (multiple part studios per document)
- Status bar with notifications

### Sketch workflow
- Start Sketch → select plane (XY / YZ / ZX or planar face)
- Sketch tools: Point, Line, Rectangle, Circle, Arc, Polygon, Slot, Spline, Mirror, Trim, Offset, Corner Fillet
- Construction mode (Q) — dashed reference geometry excluded from solid profiles
- Dimensions: Linear, Radius, Angle
- Constraints: Horizontal, Vertical, Coincident, Equal, Fix, Tangent, Parallel, Perpendicular, Concentric
- Grid overlay with snap (G to toggle)
- Finish Sketch → back to 3D with the sketch visible in the feature tree

### Feature tools
- **Extrude** — blind, symmetric, join body, cut body
- **Revolve** — axis + angle
- **Sweep** — profile along path
- **Loft** — between two or more profiles
- **Hole** — depth + diameter
- **Fillet / Chamfer** — edge rounding and beveling
- **Shell** — hollow a solid with wall thickness
- **Linear Pattern / Circular Pattern** — repeat a feature
- **Mirror** — reflect across a plane
- **Boolean**: Union, Subtract, Intersect (multi-body workflows)
- **Datum Plane** — offset reference planes
- **Primitives**: Box, Cylinder, Sphere, Cone, Torus, Pyramid, Wedge, Prism, Capsule, Hemisphere, Ellipsoid, and more

### Document system
- New / Open / Save / Save As in the top studio shell
- JSON-based project format with preferred `.umxproj` extension
- Dirty-state tracking and saved/unsaved status in the shell
- Autosave under `%APPDATA%/My3DApp/Autosave/`
- Recent documents stored in `%APPDATA%/My3DApp/settings.json`
- Recovery prompt on startup when autosave content is available

### Template library
Starter parametric templates you can select, configure, and generate:

| Template | Category | Key parameters |
|---|---|---|
| Mounting Plate | Plates & mounts | width, height, thickness, corner radius, holes |
| Washer | Disks & spacers | outer diameter, inner diameter, thickness |
| Spacer / Standoff | Disks & spacers | OD, ID, height, chamfer |
| L-Bracket | Brackets | width, height, depth, thickness, holes |
| Fan Adapter Plate | Plates & mounts | fan size, thickness, screw holes, center opening |
| Cable Clip | Clips & routing | cable diameter, clip width, wall thickness, gap |
| Simple Box | Enclosures | width, depth, height, wall thickness, top mode |
| Lid / Cover | Enclosures | width, depth, thickness, lip height, tolerance |
| DIN Rail Clip | Mounts & fixtures | rail width, clip height, lip depth, screw hole |
| T-Slot Nut | Fasteners & hardware | slot width, nut length, hole |
| Box Enclosure | Enclosures | inner size, wall thickness, corner radius |
| Hinge Bracket | Brackets | leaf size, thickness, pin diameter |
| PCB Tray | Electronics | board size, wall height, standoffs |

### ACL scripting language
- Direct CAD commands for sketching and feature work
- `recipe ...` macros for reusable modeling sequences
- `template ...` / `part ...` invocations for parameterized maker parts
- script envelopes with comments for AI-generated command blocks

See [Docs/ACL_LANGUAGE.md](Docs/ACL_LANGUAGE.md).

### Export
- STL export (Ctrl+E → Export dialog)
- OBJ export
- STEP export placeholder (not yet functional)
- Prepare workspace — part review, dimension summary, export controls

### AI Copilot
- Integrated as right-panel dock and "AI Chat" workspace tab
- Provider: OpenAI or Anthropic
- Modes: Auto, Do, Think, Assist, Think&Do
- Contextual commands: creates features, inserts recipes, explains selections
- Clean inactive state when no API key is configured (no error spam)

---

## Basic workflow

```
1. Launch → opens in Part Studio
2. Press S or click "Sketch" → select a reference plane
3. Draw a rectangle or circle with sketch tools
4. Finish Sketch (Esc or toolbar)
5. Press E or click "Extrude" → set distance → OK
6. See the solid body in the viewport and feature tree
7. Press Ctrl+E → Export STL → save file → open in your slicer
```

Or using templates:

```
1. Click the "Templates" tab
2. Select a template (e.g., Mounting Plate)
3. Edit the parameters in the panel
4. Click "Generate" → part appears in Part Studio
5. Go to "Prepare" tab → Export STL
```

### Document flow

```
1. Click New or open an existing `.umxproj`
2. Edit a template or part studio
3. Watch the shell show Unsaved / Autosaved / Saved state
4. Save manually, or recover from autosave after restart if needed
5. Reopen the document and continue from the restored workspace/template state
```

---

## Project structure

```
My3DApp/
├── AvaloniaApp/            # UI layer (Avalonia MVVM)
│   ├── MainWindow.axaml    # Main shell layout
│   ├── ViewModels/         # StudioShellViewModel (main app state)
│   ├── Services/           # MakerTemplateLibrary, AssistantChatService, etc.
│   ├── Controls/           # WebViewportHost (3D viewport)
│   └── Dialogs/            # Feature dialogs (Extrude, Hole, Pattern, etc.)
├── Engine/                 # CAD kernel (CadModel, SolidCompiler, MeshBuilder)
├── Core/                   # Geometry math (Solids, Primitives, Mesh)
├── Backends/               # Geometry backend interface
├── Export/                 # STL / OBJ exporters
├── PartLibrary/            # Contributor-facing template library docs
│   ├── README.md
│   ├── TEMPLATE_GUIDE.md
│   └── Templates/          # One folder per template
├── Assets/Icons/           # SVG icon set (40+ icons)
└── AgentOps/               # Build automation and agent task tracking
```

---

## How to add a new template

Templates are defined by:
- a manifest in `PartLibrary/Templates/<TemplateName>/template.json`
- a runtime loader and builder mapping in [`AvaloniaApp/Services/MakerTemplateLibrary.cs`](AvaloniaApp/Services/MakerTemplateLibrary.cs)
- the ACL scripting language described in [Docs/ACL_LANGUAGE.md](Docs/ACL_LANGUAGE.md)

Each template includes:
- `id` — unique string key
- `displayName` — shown in the UI
- `category` — groups templates in the list
- `description` — one-line summary
- `tags` — searchable keywords
- `parameters` — named values with default/min/max/unit
- `builder` — maps the manifest to a stable CAD command builder

### Minimal example

```json
{
  "id": "my-bracket",
  "displayName": "My Bracket",
  "category": "Brackets",
  "description": "A simple custom bracket.",
  "tags": ["bracket", "custom"],
  "previewSummary": "Starter bracket shape.",
  "builder": "my-bracket",
  "parameters": [
    { "key": "width", "displayName": "Width", "defaultValue": 60, "minValue": 10, "maxValue": 200, "unit": "mm", "description": "Bracket width" }
  ]
}
```

Then:
1. Add `template.json` and `template.md` in `PartLibrary/Templates/MyBracket/`
2. Map the `builder` id in `MakerTemplateLibrary.cs`
3. Run `dotnet build` and test it
4. Submit a PR

See [PartLibrary/TEMPLATE_GUIDE.md](PartLibrary/TEMPLATE_GUIDE.md) for the full contributor guide.

For document structure details, see [Docs/DOCUMENT_SYSTEM.md](Docs/DOCUMENT_SYSTEM.md).

---

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| S | Start Sketch |
| E | Extrude |
| F | Focus / Fit view |
| M | Move tool |
| Q | Toggle Construction mode |
| G | Toggle Grid |
| L | Line tool |
| R | Rectangle tool |
| C | Circle tool |
| A | Arc tool |
| P | Point tool |
| D | Linear Dimension |
| Ctrl+K | Command palette |
| Ctrl+S | Save |
| Ctrl+O | Open |
| Ctrl+E | Export |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+D | Duplicate |
| Esc | Cancel current tool / return to 3D |

---

## Contributing

This repo is designed to be contributor-friendly. Small targeted PRs are welcome.

Good first contributions:
- **Add a new maker template** — define parameters, wire into registry, document it
- **Improve sketch behavior** — snap, constraints, entity editing
- **Improve export** — STEP export, per-body export, filename conventions
- **Improve Prepare workspace** — print info, dimension summary, slicer hints
- **Add SVG icons** — the icon set in `Assets/Icons/` uses consistent style
- **Write tests** — especially for geometry and template generation

You can also use Claude / Codex / GPT to help generate new templates from a description and open a PR.

---

## Current limitations

- **Windows only** — WebView2 is Windows-specific; macOS/Linux port is on the roadmap
- **Constraint solver** — sketch constraints are improving but not yet equivalent to commercial CAD
- **CSG booleans** — union/subtract/intersect work for simple cases; complex boolean trees can fail
- **Template geometry** — some templates generate simplified solids; complex details (hole booleans on all templates) are not yet universal
- **STEP export** — not yet functional; STL and OBJ work
- **No cloud sync** — local files only (.my3dapp JSON format)
- **Single-user** — no collaboration features yet

---

## Roadmap

- [ ] Full sketch constraint solver (DOF tracker, fully-constrained feedback)
- [ ] Face-based sketching (sketch on any planar face, not just origin planes)
- [ ] STEP export
- [ ] More template packs: enclosures, printer mods, electronics mounts
- [ ] Plugin/extension system for custom templates
- [ ] macOS / Linux port
- [ ] Performance improvements for complex models
- [ ] Community part sharing gallery

---

## License

MIT — see LICENSE file.

---

*Built with .NET 10 · Avalonia UI · WebView2 · Three.js*
*Early AI-assisted open-source maker CAD studio — contributions welcome.*
=======
# comfyui-mcp-runner (v0.5 in-progress)

A **local-first MCP media runner** for Claude and other MCP-compatible clients.

## Local-first, not hosted
This project is **not** a hosted service. You run it on your own machine.
- You run this MCP server locally.
- You run ComfyUI locally.
- No project-hosted generation server, no accounts, no SaaS dashboard.

## Current backends
1. **Local ComfyUI backend** (generative image/video workflows).
2. **Code-generated SVG backend** (precise, deterministic image assets).

## Why code-generated images?
Generative models are strong for realism/art direction, but often weak for exact text/layout consistency.
Code-rendered images are excellent for:
- exact text
- repeatable layouts
- diagrams
- GitHub banners
- social launch cards
- thumbnails and feature cards

They are also local, free, deterministic, and require no API key.

## MCP tools
- `check_comfyui_health`
- `inspect_comfyui_workflow`
- `suggest_workflow_mapping`
- `create_preset_from_workflow`
- `render_code_image` ✅ (new)
- `list_recent_outputs`
- `run_comfyui_workflow`

## Generated launch assets
Generated with `render_code_image` / local generator script:
- `demo-assets/hero-banner.svg`
- `demo-assets/pipeline-diagram.svg`
- `demo-assets/social-launch-card.svg`

## Install
```bash
npm install
cp config.example.json config.json
npm run build
npm start
```

## Generate launch/demo assets (no ComfyUI required)
```bash
npm run generate:demo-assets
```

## Manual real ComfyUI integration test (when ComfyUI is running)
1. Start ComfyUI locally (default `http://127.0.0.1:8188`).
2. Start this MCP server (`npm start`).
3. From Claude Desktop MCP client, call:
   - `check_comfyui_health`
   - `run_comfyui_workflow`
4. Verify output file paths and logs/index updates.

## Scope guardrails
- Focused on image/video media workflows.
- Not a 3D/CAD/Blender/STL project.
- No paid API providers in this pass.

## Optional future BYOK/BYOC backends (roadmap only)
- Google Gemini / Nano Banana / Veo
- OpenAI GPT Image
- fal.ai
- Replicate
- Comfy Cloud

## Future video-as-code backends (roadmap only)
- Remotion
- Motion Canvas
- Manim
- FFmpeg/MoviePy assembly

## Roadmap (realistic status)
- v0.1 ✅ local ComfyUI MCP runner
- v0.2 ✅ local code-generated SVG image backend
- v0.3 ✅ output index / local gallery base
- v0.4 ✅ workflow inspection / mapping helper
- v0.5 🔄 video workflow support hardening
- v0.6 ⏳ video-as-code backend exploration
- v0.7 ⏳ optional cloud/API backends (BYOK/BYOC)
>>>>>>> theirs
