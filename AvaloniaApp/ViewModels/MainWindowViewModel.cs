using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using My3DApp.AvaloniaApp.Services;
using My3DApp.Backends;
using My3DApp.Engine;

namespace My3DApp.AvaloniaApp.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AiBackend _ai = new();
    private readonly SceneGraph _scene = new();
    private readonly UndoRedoStack _history = new();
    private readonly ModelManager _models;

    public IFileDialogService? FileDialogs { get; set; }


    private ViewportService? _viewportService;
    public ViewportService? ViewportService
    {
        get => _viewportService;
        set
        {
            if (_viewportService != null)
            {
                _viewportService.CursorPositionChanged -= OnCursorPositionChanged;
                _viewportService.ObjectSelected        -= OnObjectSelected;
            }
            _viewportService = value;
            if (_viewportService != null)
            {
                _viewportService.CursorPositionChanged += OnCursorPositionChanged;
                _viewportService.ObjectSelected        += OnObjectSelected;
            }
        }
    }

    private void OnCursorPositionChanged(float x, float y, float z)
        => CursorPosition = $"X: {x:F1}  Y: {y:F1}  Z: {z:F1}";

    private void OnObjectSelected(string? name)
    {
        if (name is null)
        {
            SelectionInfo = "Nothing selected";
            return;
        }
        SelectionInfo = $"Selected: {name}";
        // Sync feature tree selection to match viewport click
        var match = AllNodes().FirstOrDefault(n => n.Name == name);
        if (match != null) SelectedFeatureNode = match;
    }

    // ── Bindable state ────────────────────────────────────────────────────────
    private bool _isViewportLoading = true;
    public bool IsViewportLoading
    {
        get => _isViewportLoading;
        set => SetField(ref _isViewportLoading, value);
    }

    private string _windowTitle = "My3DApp — AI Native CAD";
    public string WindowTitle
    {
        get => _windowTitle;
        set => SetField(ref _windowTitle, value);
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private string _cursorPosition = "X: 0.000  Y: 0.000  Z: 0.000";
    public string CursorPosition
    {
        get => _cursorPosition;
        set => SetField(ref _cursorPosition, value);
    }

    private string _selectionInfo = "Nothing selected";
    public string SelectionInfo
    {
        get => _selectionInfo;
        set => SetField(ref _selectionInfo, value);
    }

    private string _aiPrompt = string.Empty;
    public string AiPrompt
    {
        get => _aiPrompt;
        set => SetField(ref _aiPrompt, value);
    }

    private string _featureFilter = string.Empty;
    public string FeatureFilter
    {
        get => _featureFilter;
        set
        {
            if (SetField(ref _featureFilter, value))
            {
                OnPropertyChanged(nameof(FilteredFeatureNodes));
                OnPropertyChanged(nameof(PartsHeader));
            }
        }
    }

    public IEnumerable<FeatureNode> FilteredFeatureNodes
    {
        get
        {
            if (FeatureNodes.Count == 0) return Enumerable.Empty<FeatureNode>();
            var children = FeatureNodes[0].Children;
            if (string.IsNullOrWhiteSpace(_featureFilter)) return FeatureNodes;
            // Filter returns matching children wrapped in a synthetic root so TreeView shows them
            var matched = children.Where(n =>
                n.Name.Contains(_featureFilter, StringComparison.OrdinalIgnoreCase) ||
                n.FeatureType.Contains(_featureFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var root = new FeatureNode { Icon = FeatureNodes[0].Icon, Name = FeatureNodes[0].Name, FeatureType = "root" };
            foreach (var n in matched) root.Children.Add(n);
            return new[] { root };
        }
    }

    public string PartsHeader =>
        FeatureNodes.Count == 0 ? "PARTS" :
        $"PARTS  ({FeatureNodes[0].Children.Count})";

    public string SolidsInfo
    {
        get
        {
            var n = _scene.Objects.Count(o => o.Solid != null && !o.ConsumedByBoolean);
            return n == 0 ? "No solids yet" : $"{n} solid{(n == 1 ? "" : "s")} — ready to export";
        }
    }

    private void RefreshSolidsInfo() => OnPropertyChanged(nameof(SolidsInfo));

    private bool _aiIsThinking;
    public bool AiIsThinking
    {
        get => _aiIsThinking;
        set
        {
            if (SetField(ref _aiIsThinking, value))
                ((RelayCommand)AiCancelCommand).NotifyCanExecuteChanged();
        }
    }

    // ── Collections ───────────────────────────────────────────────────────────
    public ObservableCollection<FeatureNode> FeatureNodes { get; } = new()
    {
        new FeatureNode { Icon = "📦", Name = "Part Studio 1", FeatureType = "root" }
    };

    public ObservableCollection<AiMessage> AiMessages { get; } = new()
    {
        new AiMessage
        {
            Role    = "AI",
            Content = "Hello! I'm your AI Design Assistant.\n\nDescribe any practical 3D part — e.g. \"a mounting bracket 80×50mm with two holes\" — and I'll generate the geometry live in the viewport and add it to your feature tree.\n\nTip: use the ✦ AI Generate button for a quick start."
        }
    };

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand NewPartStudioCommand    { get; private set; } = null!;
    public ICommand OpenCommand             { get; private set; } = null!;
    public ICommand ExportCommand           { get; private set; } = null!;
    public ICommand ExitCommand             { get; private set; } = null!;
    public ICommand UndoCommand             { get; private set; } = null!;
    public ICommand RedoCommand             { get; private set; } = null!;
    public ICommand SetViewCommand          { get; private set; } = null!;
    public ICommand NewSketchCommand        { get; private set; } = null!;
    public ICommand ExtrudeCommand          { get; private set; } = null!;
    public ICommand RevolveCommand          { get; private set; } = null!;
    public ICommand LoftCommand             { get; private set; } = null!;
    public ICommand SweepCommand            { get; private set; } = null!;
    public ICommand FilletCommand           { get; private set; } = null!;
    public ICommand ChamferCommand          { get; private set; } = null!;
    public ICommand ShellCommand            { get; private set; } = null!;
    public ICommand BooleanUnionCommand     { get; private set; } = null!;
    public ICommand BooleanSubtractCommand  { get; private set; } = null!;
    public ICommand BooleanIntersectCommand { get; private set; } = null!;
    public ICommand AiGenerateCommand       { get; private set; } = null!;
    public ICommand AiSendCommand           { get; private set; } = null!;
    public ICommand FocusAiPanelCommand     { get; private set; } = null!;
    public ICommand AiGenerateSketchCommand { get; private set; } = null!;
    public ICommand AiSuggestFeatureCommand { get; private set; } = null!;
    public ICommand AddFeatureCommand       { get; private set; } = null!;
    public ICommand FitViewCommand          { get; private set; } = null!;
    public ICommand ResetViewCommand        { get; private set; } = null!;
    public ICommand WireframeCommand        { get; private set; } = null!;
    public ICommand SolidViewCommand        { get; private set; } = null!;
    public ICommand OpenDocsCommand         { get; private set; } = null!;
    public ICommand ShowAboutCommand        { get; private set; } = null!;
    public ICommand AiCancelCommand         { get; private set; } = null!;
    public ICommand AiClearCommand          { get; private set; } = null!;

    private FeatureNode? _selectedFeatureNode;
    public FeatureNode? SelectedFeatureNode
    {
        get => _selectedFeatureNode;
        set => SetField(ref _selectedFeatureNode, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public MainWindowViewModel()
    {
        _models = new ModelManager(_scene);
        _history.StackChanged += OnHistoryChanged;

        NewPartStudioCommand    = new RelayCommand(NewPartStudio);
        OpenCommand             = new AsyncRelayCommand(OpenAsync);
        ExportCommand           = new AsyncRelayCommand(ExportAsync);
        ExitCommand             = new RelayCommand(Exit);
        UndoCommand             = new RelayCommand(Undo, () => _history.CanUndo);
        RedoCommand             = new RelayCommand(Redo, () => _history.CanRedo);
        SetViewCommand          = new RelayCommand(p => SetView(p as string ?? "iso"));
        NewSketchCommand        = new RelayCommand(NewSketch);
        ExtrudeCommand          = new AsyncRelayCommand(ExtrudeAsync);
        RevolveCommand          = new AsyncRelayCommand(RevolveAsync);
        LoftCommand             = new AsyncRelayCommand(LoftAsync);
        SweepCommand            = new AsyncRelayCommand(SweepAsync);
        FilletCommand           = new AsyncRelayCommand(FilletAsync);
        ChamferCommand          = new AsyncRelayCommand(ChamferAsync);
        ShellCommand            = new AsyncRelayCommand(ShellAsync);
        BooleanUnionCommand     = new AsyncRelayCommand(BooleanUnionAsync);
        BooleanSubtractCommand  = new AsyncRelayCommand(BooleanSubtractAsync);
        BooleanIntersectCommand = new AsyncRelayCommand(BooleanIntersectAsync);
        AiGenerateCommand       = new RelayCommand(AiGenerate);
        AiSendCommand           = new AsyncRelayCommand(AiSendAsync);
        FocusAiPanelCommand     = new RelayCommand(FocusAiPanel);
        AiGenerateSketchCommand = new AsyncRelayCommand(AiGenerateSketchAsync);
        AiSuggestFeatureCommand = new AsyncRelayCommand(AiSuggestFeatureAsync);
        AddFeatureCommand       = new RelayCommand(AddFeature);
        FitViewCommand          = new AsyncRelayCommand(() => ViewportService?.FitViewAsync()   ?? Task.CompletedTask);
        ResetViewCommand        = new AsyncRelayCommand(() => ViewportService?.ResetViewAsync() ?? Task.CompletedTask);
        WireframeCommand        = new AsyncRelayCommand(() => ViewportService?.SetWireframeAsync(true)  ?? Task.CompletedTask);
        SolidViewCommand        = new AsyncRelayCommand(() => ViewportService?.SetWireframeAsync(false) ?? Task.CompletedTask);
        OpenDocsCommand  = new RelayCommand(() => OpenUrl("https://github.com/svetikfleur-alt/my3dapp"));
        ShowAboutCommand = new RelayCommand(ShowAbout);
        AiCancelCommand  = new RelayCommand(() => _ai.CancelCurrentRequest(), () => AiIsThinking);
        AiClearCommand   = new RelayCommand(AiClear);

        // Wire context-menu commands onto initial tree nodes
        foreach (var root in FeatureNodes)
            WireNodeCommands(root);

        // Notify filter/parts-header properties whenever the root children change
        FeatureNodes.CollectionChanged += (_, _) => RefreshTreeBindings();
        if (FeatureNodes.Count > 0)
            FeatureNodes[0].Children.CollectionChanged += (_, _) => RefreshTreeBindings();
    }

    private void RefreshTreeBindings()
    {
        OnPropertyChanged(nameof(FilteredFeatureNodes));
        OnPropertyChanged(nameof(PartsHeader));
        RefreshSolidsInfo();
    }

    // ── History ───────────────────────────────────────────────────────────────
    private void OnHistoryChanged()
    {
        ((RelayCommand)UndoCommand).NotifyCanExecuteChanged();
        ((RelayCommand)RedoCommand).NotifyCanExecuteChanged();
    }

    // ── Part Studio ───────────────────────────────────────────────────────────
    // ── Node command wiring ───────────────────────────────────────────────────
    private FeatureNode WireNodeCommands(FeatureNode node)
    {
        node.DeleteCommand          = new RelayCommand(() => DeleteFeature(node));
        node.RenameCommand          = new RelayCommand(() => RenameFeature(node));
        node.ToggleVisibilityCommand = new RelayCommand(() => ToggleVisibility(node));
        foreach (var child in node.Children)
            WireNodeCommands(child);
        return node;
    }

    private FeatureNode MakeNode(string icon, string name, string type)
        => WireNodeCommands(new FeatureNode { Icon = icon, Name = name, FeatureType = type });

    // ── Part Studio ───────────────────────────────────────────────────────────
    private void NewPartStudio()
    {
        _scene.Clear();
        _history.Clear();
        FeatureNodes.Clear();
        var newRoot = MakeNode("📦", "Part Studio 1", "root");
        FeatureNodes.Add(newRoot);
        newRoot.Children.CollectionChanged += (_, _) => RefreshTreeBindings();
        WindowTitle = "My3DApp — New Part Studio";
        StatusMessage = "New Part Studio created.";
        _ = ViewportService?.ExecuteScriptAsync("viewer.clearScene();");
    }

    // ── File operations ───────────────────────────────────────────────────────
    private async Task OpenAsync()
    {
        if (FileDialogs is null) { StatusMessage = "File dialog not ready."; return; }

        var path = await FileDialogs.OpenFileAsync(
            "Open 3D Model",
            FileDialogService.AllCad,
            FileDialogService.Stl,
            FileDialogService.Obj,
            FileDialogService.Step,
            FileDialogService.ThreeMf);

        if (path is null) return;
        await ImportFileDirectAsync(path);
    }

    public async Task ImportFileDirectAsync(string path)
    {
        StatusMessage = $"Importing {Path.GetFileName(path)}…";
        var obj = await _models.ImportAsync(path);
        if (obj is null) { StatusMessage = "Import failed."; return; }

        var ext      = Path.GetExtension(path).TrimStart('.');
        var node     = MakeNode("📥", Path.GetFileNameWithoutExtension(path), ext);
        node.SceneObjectId = obj.Id;
        node.SolidDescription = obj.Solid?.ToString() ?? $"Imported {ext.ToUpperInvariant()}";

        if (FeatureNodes.Count > 0)
            FeatureNodes[0].Children.Add(node);

        // Load geometry into viewport and record the add script for delete-undo
        var safeName = node.Name.Replace("'", "");
        var lowerExt = Path.GetExtension(path).ToLowerInvariant();
        string finalStatus;

        if (lowerExt == ".stl" && ViewportService != null)
        {
            var bytes  = await File.ReadAllBytesAsync(path);
            var b64    = Convert.ToBase64String(bytes);
            var script = $"viewer.loadSTL('{b64}', '{safeName}');";
            node.ViewportAddScript = script;
            await ViewportService.ExecuteScriptAsync(script);
            finalStatus = $"Imported: {Path.GetFileName(path)}";
        }
        else if (ViewportService != null)
        {
            // OBJ / STEP / 3MF: geometry not parsed — show a placeholder box
            var script = $"viewer.addBox('{safeName}', 100, 100, 100); viewer.fitView();";
            node.ViewportAddScript = script;
            await ViewportService.ExecuteScriptAsync(script);
            var fmt = lowerExt.TrimStart('.').ToUpperInvariant();
            finalStatus = $"Imported {Path.GetFileName(path)} ({fmt} shown as placeholder box — full geometry parsing not yet supported)";
        }
        else
        {
            finalStatus = $"Imported: {Path.GetFileName(path)}";
        }

        WindowTitle = $"My3DApp — {Path.GetFileName(path)}";
        StatusMessage = finalStatus;
        RefreshSolidsInfo();
    }

    private async Task ExportAsync()
    {
        if (FileDialogs is null) { StatusMessage = "File dialog not ready."; return; }

        var path = await FileDialogs.SaveFileAsync(
            "Export 3D Model",
            "export.stl",
            FileDialogService.Stl,
            FileDialogService.Obj,
            FileDialogService.ThreeMf);

        if (path is null) return;

        StatusMessage = $"Exporting to {Path.GetFileName(path)}...";

        var ext = Path.GetExtension(path).ToLowerInvariant();

        // Collect solids to export: skip objects consumed by a boolean result to avoid doubling
        var solids = _scene.Objects.Where(o => o.Solid != null && !o.ConsumedByBoolean).ToList();

        if (solids.Count == 0)
        {
            StatusMessage = "Nothing to export — add geometry first.";
            return;
        }

        // Merge all solids into one mesh at export time
        var merged = new Mesh();
        foreach (var solid in solids)
            foreach (var tri in solid.Solid!.ToMesh().Triangles)
                merged.Triangles.Add(tri);

        if (ext == ".obj")
        {
            var sb2 = new StringBuilder();
            sb2.AppendLine("# Exported by My3DApp");
            sb2.AppendLine("o Merged");
            int vIdx = 1;
            foreach (var t in merged.Triangles)
            {
                sb2.AppendLine($"v {t.V0.X:F6} {t.V0.Y:F6} {t.V0.Z:F6}");
                sb2.AppendLine($"v {t.V1.X:F6} {t.V1.Y:F6} {t.V1.Z:F6}");
                sb2.AppendLine($"v {t.V2.X:F6} {t.V2.Y:F6} {t.V2.Z:F6}");
                sb2.AppendLine($"f {vIdx} {vIdx+1} {vIdx+2}");
                vIdx += 3;
            }
            await File.WriteAllTextAsync(path, sb2.ToString());
        }
        else
        {
            await using var fs = File.Create(path);
            merged.WriteBinaryStl(fs);
        }
        StatusMessage = $"Exported {solids.Count} solid{(solids.Count == 1 ? "" : "s")} ({merged.Triangles.Count} triangles): {Path.GetFileName(path)}";
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────
    // Creates a FeatureNodeUndoAction, pushes it (Execute is called inside Push), updates status.
    // viewportScript uses {name} as a placeholder for the safe (single-quote-escaped) node name.
    private void PushFeatureNode(string icon, string featureType, string displayPrefix,
                                 string? viewportScript, string status,
                                 SolidParams? solid = null)
    {
        if (FeatureNodes.Count == 0) return;
        var count = FeatureNodes[0].Children.Count + 1;
        var name  = $"{displayPrefix} {count}";
        var node  = MakeNode(icon, name, featureType);
        node.SolidDescription = solid?.ToString();
        var safe   = name.Replace("'", "");
        var script = viewportScript?.Replace("{name}", safe);
        _history.Push(new FeatureNodeUndoAction(
            _scene, FeatureNodes[0].Children, node, ViewportService, script, solid));
        StatusMessage = status.Replace("{name}", name);
        RefreshSolidsInfo();
        RuntimeLog.Info("VM", $"Created '{name}' ({featureType}) solid={solid?.ToString() ?? "none"}");
    }

    private void NewSketch()
        => PushFeatureNode("⬜", "sketch", "Sketch",
            "viewer.addSketchPlane('{name}'); viewer.fitView();",
            "{name} — sketch plane added. Define profile in the AI panel.");

    private async Task ExtrudeAsync()
    {
        if (FileDialogs is null) { PushExtrudeDefault(); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("extrude");
        if (!r.Confirmed) return;
        var d = r.Depth > 0 ? r.Depth : 50f;
        var solid = new BoxParams(100, d, 60);
        PushFeatureNode("⬆", r.Mode == "New" ? "extrude" : $"extrude-{r.Mode.ToLower()}",
            $"Extrude",
            $"viewer.addBox('{{name}}', 100, {d:F1}, 60); viewer.fitView();",
            $"{{name}} — Extrude {d:F1}mm [{r.Mode}] added.",
            solid);
    }

    private void PushExtrudeDefault()
        => PushFeatureNode("⬆", "extrude", "Extrude",
            "viewer.addBox('{name}', 100, 50, 60); viewer.fitView();",
            "{name} added.",
            new BoxParams(100, 50, 60));

    private async Task RevolveAsync()
    {
        if (FileDialogs is null) { PushRevolveDefault(); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("revolve");
        if (!r.Confirmed) return;
        var angle = r.Angle > 0 ? r.Angle : 360f;
        var solid = new CylinderParams(40, 80, 32);
        PushFeatureNode("↻", r.Mode == "New" ? "revolve" : $"revolve-{r.Mode.ToLower()}",
            "Revolve",
            $"viewer.addCylinder('{{name}}', 40, 80, 32); viewer.fitView();",
            $"{{name}} — Revolve {angle:F0}° [{r.Mode}] added.",
            solid);
    }

    private void PushRevolveDefault()
        => PushFeatureNode("↻", "revolve", "Revolve",
            "viewer.addCylinder('{name}', 40, 80, 32); viewer.fitView();",
            "{name} added.",
            new CylinderParams(40, 80, 32));

    private async Task LoftAsync()
    {
        if (FileDialogs is null) { PushLoftDefault(); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("loft");
        if (!r.Confirmed) return;
        var h  = r.LoftHeight > 0 ? r.LoftHeight : 60f;
        var sc = Math.Clamp(r.LoftScale, 0.1f, 2f);
        var solid = new LoftParams(80, 60, h, sc);
        var endW = (int)(80 * sc); var endD = (int)(60 * sc);
        PushFeatureNode("⤵", r.Mode == "New" ? "loft" : $"loft-{r.Mode.ToLower()}",
            "Loft",
            $"viewer.addLoft('{{name}}', 80, 60, {h:F1}, {sc:F2}); viewer.fitView();",
            $"{{name}} — Loft 80×60→{endW}×{endD}, h={h:F1}mm [{r.Mode}] added.",
            solid);
    }

    private void PushLoftDefault()
        => PushFeatureNode("⤵", "loft", "Loft",
            "viewer.addLoft('{name}', 80, 60, 60, 0.6); viewer.fitView();",
            "{name} added.",
            new LoftParams(80, 60, 60, 0.6f));

    private async Task SweepAsync()
    {
        if (FileDialogs is null) { PushSweepDefault(); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("sweep");
        if (!r.Confirmed) return;
        var len = r.PathLength > 0 ? r.PathLength : 80f;
        var solid = new SweepParams(8, len, r.TwistAngle);
        PushFeatureNode("⬡", r.Mode == "New" ? "sweep" : $"sweep-{r.Mode.ToLower()}",
            "Sweep",
            $"viewer.addSweep('{{name}}', 8, {len:F1}); viewer.fitView();",
            $"{{name}} — Sweep r=8mm path={len:F1}mm [{r.Mode}] added.",
            solid);
    }

    private void PushSweepDefault()
        => PushFeatureNode("⬡", "sweep", "Sweep",
            "viewer.addSweep('{name}', 8, 80); viewer.fitView();",
            "{name} added.",
            new SweepParams(8, 80));

    private async Task FilletAsync()
    {
        if (FileDialogs is null) { PushFilletDefault(); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("fillet");
        if (!r.Confirmed) return;
        var rad = r.FilletRadius > 0 ? r.FilletRadius : 3f;
        PushFeatureNode("◯", "fillet", "Fillet",
            $"viewer.addFillet('{{name}}', {rad:F1}); viewer.fitView();",
            $"{{name}} — Fillet r={rad:F1}mm added.",
            null);
    }

    private void PushFilletDefault()
        => PushFeatureNode("◯", "fillet", "Fillet",
            "viewer.addFillet('{name}', 3); viewer.fitView();",
            "{name} added.", null);

    private async Task ChamferAsync()
    {
        if (FileDialogs is null) { PushChamferDefault(); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("chamfer");
        if (!r.Confirmed) return;
        var dist = r.ChamferDist > 0 ? r.ChamferDist : 2f;
        PushFeatureNode("◢", "chamfer", "Chamfer",
            $"viewer.addChamfer('{{name}}', {dist:F1}, {r.ChamferAngle:F0}); viewer.fitView();",
            $"{{name}} — Chamfer d={dist:F1}mm, {r.ChamferAngle:F0}° added.",
            null);
    }

    private void PushChamferDefault()
        => PushFeatureNode("◢", "chamfer", "Chamfer",
            "viewer.addChamfer('{name}', 2, 45); viewer.fitView();",
            "{name} added.", null);

    private async Task ShellAsync()
    {
        if (FileDialogs is null) { PushShellDefault(); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("shell");
        if (!r.Confirmed) return;
        var t = r.ShellThickness > 0 ? r.ShellThickness : 2f;

        // Shell wraps the last added solid as its body
        var lastSolid = AllNodes()
            .Where(n => n.SceneObjectId.HasValue)
            .Select(n => _scene.Find(n.SceneObjectId!.Value))
            .LastOrDefault(o => o?.Solid != null && !o.ConsumedByBoolean);

        SolidParams? bodyParam = lastSolid?.Solid;
        if (bodyParam is null)
        {
            StatusMessage = "Shell: no solid body found — add geometry first.";
            return;
        }

        var shell = new ShellParams(bodyParam, t, r.Mode);
        PushFeatureNode("⚙", "shell", "Shell",
            null,
            $"{{name}} — Shell t={t:F1}mm ({r.Mode}) applied.",
            shell);
    }

    private void PushShellDefault()
        => PushFeatureNode("⚙", "shell", "Shell",
            null, "{name} — shell (select body + wall thickness in AI panel).");

    private async Task BooleanUnionAsync()
    {
        if (FileDialogs is null) { PushBooleanDefault("⊕", "boolean-union", "Union"); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("boolean-union");
        if (!r.Confirmed) return;
        await ApplyBooleanAsync("⊕", "boolean-union", "Union", r.BodyA, r.BodyB,
            (a, b) => new BooleanUnionParams(a, b),
            (sa, sb) => $"viewer.booleanUnion('{{name}}', '{sa}', '{sb}'); viewer.fitView();");
    }

    private async Task BooleanSubtractAsync()
    {
        if (FileDialogs is null) { PushBooleanDefault("⊖", "boolean-subtract", "Subtract"); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("boolean-subtract");
        if (!r.Confirmed) return;
        await ApplyBooleanAsync("⊖", "boolean-subtract", "Subtract", r.BodyA, r.BodyB,
            (a, b) => new BooleanSubtractParams(a, b),
            (sa, sb) => $"viewer.booleanSubtract('{{name}}', '{sa}', '{sb}'); viewer.fitView();");
    }

    private async Task BooleanIntersectAsync()
    {
        if (FileDialogs is null) { PushBooleanDefault("⊗", "boolean-intersect", "Intersect"); return; }
        var r = await FileDialogs.ShowFeatureDialogAsync("boolean-intersect");
        if (!r.Confirmed) return;
        await ApplyBooleanAsync("⊗", "boolean-intersect", "Intersect", r.BodyA, r.BodyB,
            (a, b) => new BooleanIntersectParams(a, b),
            (sa, sb) => $"viewer.booleanIntersect('{{name}}', '{sa}', '{sb}'); viewer.fitView();");
    }

    private async Task ApplyBooleanAsync(
        string icon, string featureType, string prefix,
        string nameA, string nameB,
        Func<SolidParams, SolidParams, SolidParams> buildParams,
        Func<string, string, string> scriptBuilder)
    {
        if (FeatureNodes.Count == 0) return;

        if (string.IsNullOrWhiteSpace(nameA) || string.IsNullOrWhiteSpace(nameB))
        {
            StatusMessage = $"{prefix}: enter both body names in the dialog.";
            return;
        }

        // Resolve body nodes by name (case-insensitive)
        var nodeA = AllNodes().FirstOrDefault(n => n.Name.Equals(nameA, StringComparison.OrdinalIgnoreCase));
        var nodeB = AllNodes().FirstOrDefault(n => n.Name.Equals(nameB, StringComparison.OrdinalIgnoreCase));

        SceneObject? objA = nodeA?.SceneObjectId.HasValue == true ? _scene.Find(nodeA.SceneObjectId!.Value) : null;
        SceneObject? objB = nodeB?.SceneObjectId.HasValue == true ? _scene.Find(nodeB.SceneObjectId!.Value) : null;

        if (objA?.Solid is null || objB?.Solid is null)
        {
            var missing = string.Join(", ",
                new[] { (nameA, objA), (nameB, objB) }
                    .Where(p => p.Item2?.Solid is null)
                    .Select(p => $"'{p.Item1}'"));
            StatusMessage = $"{prefix}: body {missing} not found — check names in the feature tree.";
            return;
        }

        // Mark inputs as consumed so export does not double the geometry
        objA.ConsumedByBoolean = true;
        objB.ConsumedByBoolean = true;

        var boolParams = buildParams(objA.Solid!, objB.Solid!);
        var safeA = nameA.Replace("'", "");
        var safeB = nameB.Replace("'", "");
        var script = scriptBuilder(safeA, safeB);

        // PushFeatureNode uses FeatureNodeUndoAction which calls _scene.Add itself,
        // so we only pass the SolidParams — no pre-created SceneObject.
        PushFeatureNode(icon, featureType, prefix, script,
            $"{{name}} — {prefix}({nameA}, {nameB}) applied.", boolParams);

        await Task.CompletedTask;
    }

    private void PushBooleanDefault(string icon, string featureType, string prefix)
        => PushFeatureNode(icon, featureType, prefix,
            null, $"{{name}} — open the dialog to select bodies for {prefix}.");
    private void AiGenerate()
    {
        AiPrompt = "Generate a practical 3D part with geometry — describe what you want to model and include a geometry-script to visualise it.";
        StatusMessage = "AI Generate: type your description and press Send →";
    }

    private void FocusAiPanel()  => StatusMessage = "AI panel focused.";
    private void AddFeature()    => NewSketch();
    private void Exit()          => Environment.Exit(0);
    private void Undo()
    {
        var desc = _history.NextUndoDescription;
        if (_history.Undo()) StatusMessage = desc != null ? $"Undid: {desc}" : "Undo complete.";
    }
    private void Redo()
    {
        var desc = _history.NextRedoDescription;
        if (_history.Redo()) StatusMessage = desc != null ? $"Redid: {desc}" : "Redo complete.";
    }
    private void ShowAbout()     => StatusMessage = "My3DApp — AI Native CAD  |  v0.1.0-alpha";
    private void SetView(string v) { StatusMessage = $"View: {v}"; _ = ViewportService?.SetViewAsync(v); }
    private void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    // ── Feature tree context menu ─────────────────────────────────────────────
    private void DeleteFeature(FeatureNode node)
    {
        foreach (var root in FeatureNodes)
        {
            var idx = root.Children.IndexOf(node);
            if (idx < 0) continue;

            // Use DeleteFeatureNodeAction so delete is undoable and restores node + viewport
            _history.Push(new DeleteFeatureNodeAction(
                _scene, root.Children, node, idx, ViewportService));

            StatusMessage = $"Deleted '{node.Name}'.";
            RuntimeLog.Info("VM", $"Deleted feature '{node.Name}'");
            return;
        }
        StatusMessage = "Cannot delete root node.";
    }

    private async void RenameFeature(FeatureNode node)
    {
        if (FileDialogs is null)
        {
            // Fallback when dialog service isn't ready (shouldn't happen at runtime)
            node.Name += " (renamed)";
            return;
        }
        var newName = await FileDialogs.ShowRenameDialogAsync(node.Name);
        if (newName is null || newName == node.Name) return;

        // Ensure uniqueness
        var base2 = newName;
        int suffix = 2;
        while (AllNodes().Any(n => n != node && n.Name == newName))
            newName = $"{base2} ({suffix++})";

        // Rename in viewport too (remove + re-add with new name)
        var oldSafe = node.Name.Replace("'", "");
        var newSafe = newName.Replace("'", "");
        if (node.ViewportAddScript is { } addScript)
        {
            _ = ViewportService?.ExecuteScriptAsync($"viewer.removeObject('{oldSafe}');");
            node.ViewportAddScript = addScript.Replace($"'{oldSafe}'", $"'{newSafe}'");
            _ = ViewportService?.ExecuteScriptAsync(node.ViewportAddScript);
        }

        node.Name = newName;
        if (node.SceneObjectId.HasValue)
        {
            var so = _scene.Find(node.SceneObjectId.Value);
            if (so != null) so.Name = newName;
        }
        StatusMessage = $"Renamed to '{newName}'.";
        RefreshTreeBindings();
    }

    private void ToggleVisibility(FeatureNode node)
    {
        node.IsVisible = !node.IsVisible;
        _ = ViewportService?.ExecuteScriptAsync(
            $"viewer.setObjectVisible('{node.Name.Replace("'", "")}', {(node.IsVisible ? "true" : "false")});");
        StatusMessage = $"'{node.Name}' {(node.IsVisible ? "visible" : "hidden")}.";
    }

    private IEnumerable<FeatureNode> AllNodes()
    {
        foreach (var root in FeatureNodes)
            foreach (var n in Flatten(root))
                yield return n;

        static IEnumerable<FeatureNode> Flatten(FeatureNode n)
        {
            yield return n;
            foreach (var child in n.Children)
                foreach (var desc in Flatten(child))
                    yield return desc;
        }
    }

    // ── AI handlers ───────────────────────────────────────────────────────────
    private async Task AiSendAsync()
    {
        var prompt = AiPrompt.Trim();
        if (string.IsNullOrEmpty(prompt) || AiIsThinking) return;

        AiPrompt = string.Empty;
        AiMessages.Add(new AiMessage { Role = "You", Content = prompt });
        AiIsThinking = true;
        StatusMessage = "AI thinking...";

        try
        {
            var context = BuildFeatureTreeContext();
            var reply = await _ai.ChatAsync(prompt, context);
            AiMessages.Add(new AiMessage { Role = "AI", Content = reply });

            if (_ai.LastGeometryScript is { } script)
            {
                await (ViewportService?.ExecuteScriptAsync(script) ?? Task.CompletedTask);
                SyncFeatureTreeFromScript(script);
            }
        }
        catch (Exception ex)
        {
            AiMessages.Add(new AiMessage { Role = "AI", Content = $"Error: {ex.Message}" });
            RuntimeLog.Error("VM", "AI send failed.", ex);
        }
        finally
        {
            AiIsThinking = false;
            StatusMessage = "Ready";
        }
    }

    private async Task AiGenerateSketchAsync()
    {
        const string prompt = "I want to start a new 3D part. Suggest a practical part worth modelling " +
            "(e.g. a bracket, housing, shaft, clip, or enclosure) and immediately generate its geometry " +
            "script with correct positions so the solids are properly assembled and non-overlapping.";
        AiMessages.Add(new AiMessage { Role = "You", Content = "(✦ Generate Part)" });
        AiIsThinking = true;
        StatusMessage = "AI generating part…";
        try
        {
            var context = BuildFeatureTreeContext();
            var reply = await _ai.ChatAsync(prompt, context);
            AiMessages.Add(new AiMessage { Role = "AI", Content = reply });
            if (_ai.LastGeometryScript is { } script)
            {
                await (ViewportService?.ExecuteScriptAsync(script) ?? Task.CompletedTask);
                SyncFeatureTreeFromScript(script);
            }
        }
        finally { AiIsThinking = false; StatusMessage = "Ready"; }
    }

    private void AiClear()
    {
        _ai.ClearHistory();
        AiMessages.Clear();
        AiMessages.Add(new AiMessage { Role = "AI", Content = "Chat cleared. Describe a part to model and I'll generate it in the viewport." });
        StatusMessage = "AI chat cleared.";
    }

    public void Dispose()
    {
        ViewportService = null; // unsubscribes CursorPositionChanged
        _ai.Dispose();
    }

    private async Task AiSuggestFeatureAsync()
    {
        const string prompt = "Looking at my current part, what would be the most useful next solid to add? " +
            "Consider the existing dimensions and positions. Generate the geometry script for it immediately, " +
            "using correct x,y,z placement relative to existing solids.";
        AiMessages.Add(new AiMessage { Role = "You", Content = "(Suggest next feature)" });
        AiIsThinking = true;
        StatusMessage = "AI analyzing part...";
        try
        {
            var context = BuildFeatureTreeContext();
            var reply = await _ai.ChatAsync(prompt, context);
            AiMessages.Add(new AiMessage { Role = "AI", Content = reply });
            if (_ai.LastGeometryScript is { } script)
            {
                await (ViewportService?.ExecuteScriptAsync(script) ?? Task.CompletedTask);
                SyncFeatureTreeFromScript(script);
            }
        }
        finally
        {
            AiIsThinking = false;
            StatusMessage = "Ready";
        }
    }

    // ── AI → feature-tree sync ────────────────────────────────────────────────
    // Parses a viewer geometry script and keeps the feature tree in sync so
    // AI-generated shapes appear as named, selectable, deletable nodes.
    private void SyncFeatureTreeFromScript(string script)
    {
        if (FeatureNodes.Count == 0) return;
        var children = FeatureNodes[0].Children;

        // clearScene → remove all children
        if (Regex.IsMatch(script, @"viewer\.clearScene\s*\("))
        {
            foreach (var n in children.ToList())
            {
                if (n.SceneObjectId.HasValue) _scene.Remove(n.SceneObjectId.Value);
            }
            children.Clear();
        }

        // moveObject('name', x, y, z) → update position in existing SceneObject's SolidParams
        foreach (Match m in Regex.Matches(script,
            @"viewer\.moveObject\s*\(\s*['""]([^'""]+)['""]\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)"))
        {
            var name = m.Groups[1].Value;
            float mx = TryF(m.Groups[2].Value), my = TryF(m.Groups[3].Value), mz = TryF(m.Groups[4].Value);
            var node = children.FirstOrDefault(n => n.Name == name);
            if (node?.SceneObjectId.HasValue == true)
            {
                var so = _scene.Find(node.SceneObjectId.Value);
                if (so?.Solid != null)
                {
                    so.Solid = so.Solid switch
                    {
                        BoxParams bp            => bp with { X = mx, Y = my, Z = mz },
                        CylinderParams cp       => cp with { X = mx, Y = my, Z = mz },
                        SphereParams sp         => sp with { X = mx, Y = my, Z = mz },
                        ExtrudePolygonParams ep  => ep with { X = mx, Y = my, Z = mz },
                        RevolveProfileParams rp  => rp with { X = mx, Y = my, Z = mz },
                        SweepParams sw           => sw with { X = mx, Y = my, Z = mz },
                        LoftParams lp            => lp with { X = mx, Y = my, Z = mz },
                        _                        => so.Solid
                    };
                    node.SolidDescription = so.Solid.ToString();
                }
            }
        }

        // removeObject('name') → remove matching node
        foreach (Match m in Regex.Matches(script, @"viewer\.removeObject\s*\(\s*['""]([^'""]+)['""]"))
        {
            var name = m.Groups[1].Value;
            var node = children.FirstOrDefault(n => n.Name == name);
            if (node != null)
            {
                if (node.SceneObjectId.HasValue) _scene.Remove(node.SceneObjectId.Value);
                children.Remove(node);
            }
        }

        // addBox / addCylinder / addSphere / addSketchPlane / addSweep / addLoft / extrudePolygon / revolveProfile → add nodes
        var addPattern = new Regex(
            @"viewer\.(addBox|addCylinder|addSphere|addSketchPlane|addSweep|addLoft|extrudePolygon|revolveProfile)\s*\(\s*['""]([^'""]+)['""]");
        foreach (Match m in addPattern.Matches(script))
        {
            var call = m.Groups[1].Value;
            var name = m.Groups[2].Value;
            if (children.Any(n => n.Name == name)) continue; // already present

            var (icon, type) = call switch
            {
                "addBox"          => ("⬆", "extrude"),
                "addCylinder"     => ("↻", "revolve"),
                "addSphere"       => ("⚪", "sphere"),
                "addSketchPlane"  => ("⬜", "sketch"),
                "addSweep"        => ("⬡", "sweep"),
                "addLoft"         => ("⤵", "loft"),
                "extrudePolygon"  => ("⬆", "extrude"),
                "revolveProfile"  => ("↻", "revolve"),
                _                 => ("⚙", "mesh"),
            };

            // Extract just this one add-call line so ViewportAddScript is precise
            // [""'] in a $@"" string is the regex character class ["'] (double-quote or single-quote)
            var lineMatch = Regex.Match(script,
                $@"viewer\.{call}\s*\(\s*[""']{Regex.Escape(name)}[""'][^;]*\);");

            var node = MakeNode(icon, name, type);
            node.ViewportAddScript = lineMatch.Success ? lineMatch.Value : null;

            // Parse dimensions + optional position from the script call
            SceneObject obj = call switch
            {
                "addBox" when Regex.Match(script,
                    $@"viewer\.addBox\s*\(\s*[""']{Regex.Escape(name)}[""']\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)(?:\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+))?")
                    is { Success: true } bm
                    && float.TryParse(bm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var bw)
                    && float.TryParse(bm.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var bh)
                    && float.TryParse(bm.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var bd)
                    => _models.CreateBox(name, bw, bh, bd,
                        TryF(bm.Groups[4].Value), TryF(bm.Groups[5].Value), TryF(bm.Groups[6].Value)),

                "addCylinder" when Regex.Match(script,
                    $@"viewer\.addCylinder\s*\(\s*[""']{Regex.Escape(name)}[""']\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)(?:\s*,\s*(-?[\d.]+))?(?:\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+))?")
                    is { Success: true } cm
                    && float.TryParse(cm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cr)
                    && float.TryParse(cm.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ch)
                    => _models.CreateCylinder(name, cr, ch, 32,
                        TryF(cm.Groups[4].Value), TryF(cm.Groups[5].Value), TryF(cm.Groups[6].Value)),

                "addSphere" when Regex.Match(script,
                    $@"viewer\.addSphere\s*\(\s*[""']{Regex.Escape(name)}[""']\s*,\s*(-?[\d.]+)(?:\s*,\s*(-?[\d.]+))?(?:\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+))?")
                    is { Success: true } sm
                    && float.TryParse(sm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sr)
                    => _models.CreateSphere(name, sr, 32,
                        TryF(sm.Groups[3].Value), TryF(sm.Groups[4].Value), TryF(sm.Groups[5].Value)),

                "extrudePolygon" when Regex.Match(script,
                    $@"viewer\.extrudePolygon\s*\(\s*[""']{Regex.Escape(name)}[""']\s*,\s*\[([^\]]+)\]\s*,\s*(-?[\d.]+)(?:\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+))?")
                    is { Success: true } em
                    => ParseExtrudePolygon(name, em),

                "revolveProfile" when Regex.Match(script,
                    $@"viewer\.revolveProfile\s*\(\s*[""']{Regex.Escape(name)}[""']\s*,\s*\[([^\]]+)\]\s*,\s*(-?[\d.]+)(?:\s*,\s*(-?[\d.]+))?(?:\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+))?")
                    is { Success: true } rm
                    => ParseRevolveProfile(name, rm),

                "addSweep" when Regex.Match(script,
                    $@"viewer\.addSweep\s*\(\s*[""']{Regex.Escape(name)}[""']\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)(?:\s*,\s*(-?[\d.]+))?(?:\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+))?")
                    is { Success: true } wm
                    && float.TryParse(wm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var wr)
                    && float.TryParse(wm.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var wl)
                    => _models.CreateSweep(name, wr, wl, 0f, 32,
                        TryF(wm.Groups[4].Value), TryF(wm.Groups[5].Value), TryF(wm.Groups[6].Value)),

                "addLoft" when Regex.Match(script,
                    $@"viewer\.addLoft\s*\(\s*[""']{Regex.Escape(name)}[""']\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)(?:\s*,\s*(-?[\d.]+))?(?:\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+))?")
                    is { Success: true } lm
                    && float.TryParse(lm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lbw)
                    && float.TryParse(lm.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lbd)
                    && float.TryParse(lm.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lh)
                    => _models.CreateLoft(name, lbw, lbd, lh, TryF(lm.Groups[4].Value) is var sc && sc > 0 ? sc : 0.6f,
                        TryF(lm.Groups[5].Value), TryF(lm.Groups[6].Value), TryF(lm.Groups[7].Value)),

                _ => _scene.Add(name, type)
            };

            node.SceneObjectId = obj.Id;
            node.SolidDescription = obj.Solid?.ToString();
            children.Add(node);
            RuntimeLog.Info("VM", $"AI synced solid '{name}' ({type}) — {obj.Solid?.ToString() ?? "no solid"}");
        }

        // booleanUnion/Subtract/Intersect('Result', 'NameA', 'NameB') → add boolean node
        var boolPattern = new Regex(
            @"viewer\.(booleanUnion|booleanSubtract|booleanIntersect)\s*\(\s*['""]([^'""]+)['""],\s*['""]([^'""]+)['""],\s*['""]([^'""]+)['""]");
        foreach (Match m in boolPattern.Matches(script))
        {
            var op     = m.Groups[1].Value;
            var result = m.Groups[2].Value;
            var nameA  = m.Groups[3].Value;
            var nameB  = m.Groups[4].Value;
            if (children.Any(n => n.Name == result)) continue;

            var (icon, type) = op switch
            {
                "booleanUnion"     => ("⊕", "boolean-union"),
                "booleanSubtract"  => ("⊖", "boolean-subtract"),
                "booleanIntersect" => ("⊗", "boolean-intersect"),
                _                  => ("⚙", "boolean"),
            };

            var nodeA = children.FirstOrDefault(n => n.Name == nameA);
            var nodeB = children.FirstOrDefault(n => n.Name == nameB);
            var solidA = nodeA?.SceneObjectId.HasValue == true ? _scene.Find(nodeA.SceneObjectId.Value)?.Solid : null;
            var solidB = nodeB?.SceneObjectId.HasValue == true ? _scene.Find(nodeB.SceneObjectId.Value)?.Solid : null;

            SceneObject boolObj;
            if (solidA != null && solidB != null)
            {
                boolObj = op switch
                {
                    "booleanUnion"     => _models.CreateBooleanUnion(result, solidA, solidB),
                    "booleanSubtract"  => _models.CreateBooleanSubtract(result, solidA, solidB),
                    "booleanIntersect" => _models.CreateBooleanIntersect(result, solidA, solidB),
                    _                  => _scene.Add(result, type)
                };

                // Mark inputs as consumed so export doesn't double the geometry
                if (nodeA?.SceneObjectId.HasValue == true)
                {
                    var soA = _scene.Find(nodeA.SceneObjectId.Value);
                    if (soA != null) soA.ConsumedByBoolean = true;
                }
                if (nodeB?.SceneObjectId.HasValue == true)
                {
                    var soB = _scene.Find(nodeB.SceneObjectId.Value);
                    if (soB != null) soB.ConsumedByBoolean = true;
                }
            }
            else
            {
                boolObj = _scene.Add(result, type);
            }

            var boolNode = MakeNode(icon, result, type);
            boolNode.SceneObjectId = boolObj.Id;
            boolNode.SolidDescription = boolObj.Solid?.ToString() ?? $"{op}({nameA}, {nameB})";
            children.Add(boolNode);
            RuntimeLog.Info("VM", $"AI synced boolean '{result}' ({op})");
        }

        RefreshTreeBindings();
    }

    // Returns the feature tree as a context prefix string.
    // Passed to AiBackend.ChatAsync as contextPrefix so it's sent to the API for the
    // current turn only — not stored in history, preventing token bloat across turns.
    private string? BuildFeatureTreeContext()
    {
        if (FeatureNodes.Count == 0) return null;
        var sb = new StringBuilder();
        sb.AppendLine("[Current part studio feature tree]");
        foreach (var root in FeatureNodes)
            AppendFeatureNode(sb, root, 0);
        return sb.ToString();
    }

    private static void AppendFeatureNode(StringBuilder sb, FeatureNode node, int depth)
    {
        var dims = node.SolidDescription is { } d ? $"  [{d}]" : "";
        sb.AppendLine($"{new string(' ', depth * 2)}{node.Icon} {node.Name} ({node.FeatureType}){dims}");
        foreach (var child in node.Children)
            AppendFeatureNode(sb, child, depth + 1);
    }

    private SceneObject ParseExtrudePolygon(string name, System.Text.RegularExpressions.Match m)
    {
        var pts = ParseFloatArray(m.Groups[1].Value);
        float depth = TryF(m.Groups[2].Value);
        float x = TryF(m.Groups[3].Value), y = TryF(m.Groups[4].Value), z = TryF(m.Groups[5].Value);
        return _models.CreateExtrudePolygon(name, pts, depth, x, y, z);
    }

    private SceneObject ParseRevolveProfile(string name, System.Text.RegularExpressions.Match m)
    {
        var pts = ParseFloatArray(m.Groups[1].Value);
        float angle = TryF(m.Groups[2].Value);
        if (angle == 0) angle = 360f;
        float x = TryF(m.Groups[4].Value), y = TryF(m.Groups[5].Value), z = TryF(m.Groups[6].Value);
        return _models.CreateRevolveProfile(name, pts, angle, 32, x, y, z);
    }

    private static float[] ParseFloatArray(string s)
        => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Select(t => float.TryParse(t.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f)
           .ToArray();

    // Returns the parsed float or 0 when the regex group didn't match.
    private static float TryF(string s)
        => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
}
