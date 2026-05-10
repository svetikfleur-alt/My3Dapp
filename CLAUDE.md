# MY3DAPP - AGENT RULES

## Before every task:
- Run dotnet build first
- Check that build succeeds before making changes
- Read existing code before editing

## Architecture rules:
- Backend files: /Backends/ and /Engine/ only
- UI files: /AvaloniaApp/ only
- Never mix backend and UI in same edit session
- Never create duplicate classes or x:Class directives

## Package rules:
- Do not add new NuGet packages without checking
  existing ones first
- Do not change WebView2 package version
- Do not add WinForms or WPF references

## After every task:
- Run dotnet build
- Confirm build succeeds before finishing
- Report any warnings

## Never:
- Create temp folders inside /AvaloniaApp/
- Duplicate existing classes
- Run multiple conflicting changes simultaneously

---

## Architecture Overview

```
My3DApp/
├── Core/
│   └── RuntimeLog.cs          — Thread-safe logging (file + Debug.WriteLine)
│                                Namespace: My3DApp.Core
│
├── Engine/                    — Pure geometry/scene layer; no UI references
│   ├── Mesh.cs                — Triangle mesh with Box/Cylinder/Sphere factories,
│   │                            binary STL read/write
│   ├── SceneGraph.cs          — SceneObject (Id, Name, Type, Geometry, Transform)
│   │                            + SceneGraph with ObjectAdded/Removed events
│   ├── ModelManager.cs        — Creates primitives, imports STL/OBJ, exports STL/OBJ
│   └── UndoRedoStack.cs       — Command pattern: IUndoableAction, Push/Undo/Redo
│                                Concrete: AddSceneObjectAction, RemoveSceneObjectAction
│
├── Backends/                  — External service integrations; no UI references
│   ├── AiBackend.cs           — Claude claude-sonnet-4-6 chat; geometry-script extraction;
│   │                            CancellationTokenSource per-request; sliding-window history
│   └── GeometryBackend.cs     — PicoGK integration (probe via reflection; graceful fallback)
│
├── AvaloniaApp/               — Avalonia UI layer (Windows desktop target)
│   ├── App.axaml / App.axaml.cs
│   ├── MainWindow.axaml       — Dark Onshape-style layout:
│   │                            Top: menu + toolbar
│   │                            Left: Feature Tree (260px) with properties strip
│   │                            Center: 3D Viewport (WebView2 + Three.js)
│   │                            Right: AI Chat Panel (300px)
│   │                            Bottom: Status bar
│   ├── MainWindow.axaml.cs    — Viewport init, DragDrop, keyboard shortcuts (S/E/Esc)
│   ├── Styles/
│   │   └── AppStyles.axaml    — toolbar-btn, ai-btn, ai-send-btn, viewport-btn, icon-btn
│   ├── Services/
│   │   ├── RuntimeLog.cs      — global using alias → My3DApp.Core.RuntimeLog
│   │   ├── ViewportService.cs — Hosts WebView2 via reflection (platform-safe on Linux CI)
│   │   │                        Methods: InitializeAsync, ExecuteScriptAsync, FitView,
│   │   │                        ResetView, SetWireframe, SetView
│   │   └── FileDialogService.cs — IFileDialogService + StorageProvider-based file pickers
│   └── ViewModels/
│       ├── ViewModelBase.cs   — INotifyPropertyChanged + SetField<T>
│       ├── RelayCommand.cs    — ICommand with canExecute gate + NotifyCanExecuteChanged
│       ├── AsyncRelayCommand.cs — async ICommand; prevents double-fire while running
│       ├── FeatureNode.cs     — Tree node: Icon, Name, FeatureType, IsVisible, IsSelected,
│       │                        SceneObjectId (links to SceneGraph), Delete/Rename/
│       │                        ToggleVisibility commands (set by VM to avoid $parent binding)
│       ├── AiMessage.cs       — Role, Content, Background (IBrush), RoleColor (IBrush)
│       └── MainWindowViewModel.cs — Central VM; owns AiBackend, SceneGraph, UndoRedoStack,
│                                    ModelManager; wires all commands; BuildContextualPrompt
│                                    serialises feature tree for AI context
│
└── Assets/
    ├── Web/
    │   └── viewer.html        — Three.js 0.168 viewport; window.viewer API:
    │                            addBox, addCylinder, addSphere, removeObject,
    │                            clearScene, fitView, resetView, setView,
    │                            setWireframe, setObjectVisible, loadSTL
    └── AppIcon.ico
```

## Key design decisions

- **Compiled Avalonia bindings** (`AvaloniaUseCompiledBindingsByDefault=true`): every
  `DataTemplate` and `TreeDataTemplate` must carry `x:DataType`. ContextMenu bindings
  go on the data item (FeatureNode), not `$parent[TreeView].DataContext`.

- **WebView2 via reflection**: `ViewportService` loads the WinForms WebView2 type at
  runtime so the project compiles on Linux CI without native WebView2 binaries.

- **PicoGK via reflection**: `GeometryBackend` probes `Type.GetType` and degrades
  gracefully if the native runtime is absent.

- **FeatureNode ↔ SceneObject link**: `FeatureNode.SceneObjectId` (Guid?) ties UI tree
  nodes to `SceneGraph` objects for delete/visibility sync.

- **AI context**: Every AI request prepends the full feature tree via `BuildContextualPrompt`
  so Claude has part-aware design context.
