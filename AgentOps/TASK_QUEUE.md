# TASK QUEUE

Priority order: stabilize build → Sketch foundation → profile detection → Extrude → tree/model structure → UI polish

---

## BLOCK 1 — AvaloniaApp Minimum Scaffold [NEXT]

**Goal:** Make the project compile with zero errors.

**Why it's first:** `Program.cs` references two types that don't exist. Nothing else can progress until the build is green.

**Scope (builder must create these files, no more):**

```
AvaloniaApp/
  App.axaml               — Avalonia Application root, FluentTheme
  App.axaml.cs            — partial class App : Application, OnFrameworkInitializationCompleted
  MainWindow.axaml        — empty window, Title="My3DApp"
  MainWindow.axaml.cs     — partial class MainWindow : Window
  Services/
    RuntimeLog.cs         — static class, Write(string context, string message, Exception? ex)

Assets/
  AppIcon.ico             — 1x1 placeholder .ico (or copy from any available icon)

app.manifest              — standard Windows app manifest (dpiAware, supportedOS)
```

**Acceptance:** `dotnet build` exits with 0 errors.

**Must NOT do:**
- Add any NuGet packages not already in .csproj
- Add any UI beyond an empty window
- Add any geometry/engine code

---

## BLOCK 2 — Assets/Web stub [QUEUED]

**Goal:** Add `Assets/Web/` directory with a minimal `index.html` so the WebView2 content path doesn't produce copy warnings.

**Scope:** Single `Assets/Web/index.html` placeholder.

---

## BLOCK 3 — Sketch Foundation [QUEUED]

**Goal:** Create the core Sketch data model and a canvas that can display a 2D sketch plane.

**Prerequisites:** Block 1 complete (build green).

**Scope (Engine layer):**
- `Engine/Sketch/SketchDocument.cs` — holds entities (lines, arcs, points)
- `Engine/Sketch/SketchEntity.cs` — base type
- `Engine/Sketch/SketchLine.cs` — two endpoints
- `Engine/Sketch/SketchPoint.cs` — x, y

**Scope (AvaloniaApp layer):**
- `AvaloniaApp/Views/SketchCanvas.cs` — Avalonia custom control, renders entities via `DrawingContext`
- `AvaloniaApp/ViewModels/SketchViewModel.cs` — holds `SketchDocument`, exposes entity list

**Acceptance:** App launches, clicking "New Sketch" (placeholder button) shows an empty sketch canvas with axes.

---

## BLOCK 4 — Profile Detection [QUEUED]

**Goal:** Given a closed loop of sketch lines, detect it as a profile and highlight it.

**Prerequisites:** Block 3 complete.

**Scope (Engine layer):**
- `Engine/Sketch/ProfileDetector.cs` — takes list of `SketchLine`, returns list of closed loops

---

## BLOCK 5 — Extrude [QUEUED]

**Goal:** Take a detected profile and produce a 3D solid via PicoGK.

**Prerequisites:** Block 4 complete.

**Scope (Engine layer):**
- `Engine/Operations/ExtrudeOperation.cs` — wraps PicoGK voxel extrusion
- Result stored in a `FeatureResult` object

---

## BLOCK 6 — Feature Tree / Model Structure [QUEUED]

**Goal:** Show a tree panel listing sketch + extrude features, allow rename/delete.

**Prerequisites:** Block 5 complete.

---

## BLOCK 7 — UI Polish [QUEUED]

**Goal:** Toolbar, proper icons, keyboard shortcuts, status bar.

**Prerequisites:** Block 6 complete.

---

## DEFERRED / WATCH

- Profile detection edge cases (intersecting loops, open loops)
- Constraint solver (underconstrained / overconstrained sketch)
- Fillet, chamfer, shell operations
- AI assistant integration (WebView2 panel)
- Export (STL, STEP)
