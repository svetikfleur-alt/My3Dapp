# My3DApp Roadmap

This roadmap tracks what's shipped, what's in progress, and what's coming next. It is updated with each release.

---

## v1.0 — Studio Foundation (current)

- [x] Unified studio shell with workspace tabs (Part Studio · Sketch · Templates · Prepare · AI Chat)
- [x] 11 parametric maker templates (bracket, cable clip, DIN rail clip, T-slot nut, box enclosure, hinge bracket, PCB tray, …)
- [x] Feature tree with sections, icons, and property inspector
- [x] Sketch engine: line, rectangle, circle, arc, slot, polygon, spline, fillet, mirror, trim, offset
- [x] Sketch constraints: horizontal, vertical, coincident, equal, fix, concentric, parallel, perpendicular, tangent
- [x] Sketch dimensions: linear, radial, angular
- [x] Extrude, revolve, shell, fillet, chamfer, hole operations
- [x] Boolean union / subtract / intersect
- [x] Circular and linear patterns
- [x] Datum plane creation and management
- [x] STL and OBJ export with body-scope selector
- [x] AI assistant dock with provider / model selection (OpenAI, Anthropic)
- [x] Natural-language command expansion via template library
- [x] Document system: save / open / recent files (`.my3dapp` JSON)
- [x] Autosave every 2 minutes with crash-recovery on next launch
- [x] New document (Ctrl+N) with unsaved-changes guard
- [x] Undo / redo stack (50 levels)
- [x] Move tool with viewport drag
- [x] Section view (X / Y / Z planes, adjustable offset)
- [x] Measure tool (point-to-point distance)
- [x] Dark / light theme toggle

---

## v1.1 — Template Studio

- [ ] Template parameter UI: sliders, step inputs, unit display
- [ ] Live geometry preview while adjusting template parameters
- [ ] Template search and filtering by category and tag
- [ ] Community template registry (pull-request based)
- [ ] Template import from URL

---

## v1.2 — Sketch Quality

- [ ] Fully constrained sketch indicator (turns green when closed + fully constrained)
- [ ] Sketch constraint solver (auto-detect over/under constrained state)
- [ ] Sketch-to-feature preview (live extrude ghost while sketching)
- [ ] Construction geometry in sketcher
- [ ] Fillet and chamfer at sketch corners

---

## v1.3 — Assembly & Multi-body

- [ ] Multi-body Part Studio (boolean operations produce separate bodies)
- [ ] Assembly workspace with mate connectors
- [ ] In-context editing for assembly components
- [ ] BOM (bill of materials) export

---

## v1.4 — Cloud & Collaboration

- [ ] Cloud document sync (optional, OAuth-based)
- [ ] Share-by-link for read-only view
- [ ] Comment threads on features
- [ ] Real-time multi-user cursor presence

---

## v2.0 — Pro Features

- [ ] STEP and IGES import / export
- [ ] BREP-level editing (face move, edge push/pull)
- [ ] Render mode with PBR materials
- [ ] CNC toolpath preview (basic G-code outline)
- [ ] Slicer integration (pipe to PrusaSlicer / OrcaSlicer)

---

## Internals / Tech Debt

- [ ] Replace the command-string CAD pipeline with a typed IR
- [ ] Unit tests for geometry builders (xUnit)
- [ ] CI pipeline (GitHub Actions: build + test on push)
- [ ] Packaging: MSIX installer + winget manifest
- [ ] Localization infrastructure (resource strings)

---

_Last updated: 2026-05-16_
