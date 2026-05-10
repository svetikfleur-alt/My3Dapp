using System.Collections.ObjectModel;
using System.IO;
using System.Text;
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
        new FeatureNode { Icon = "📦", Name = "Part Studio 1", FeatureType = "root", Children =
        {
            new FeatureNode { Icon = "⬜", Name = "Sketch 1",   FeatureType = "sketch" },
            new FeatureNode { Icon = "⬆", Name = "Extrude 1",  FeatureType = "extrude" },
        }}
    };

    public ObservableCollection<AiMessage> AiMessages { get; } = new()
    {
        new AiMessage
        {
            Role    = "AI",
            Content = "Hello! I'm your AI Design Assistant. Describe what you'd like to model and I'll help you build it."
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
        ExtrudeCommand          = new RelayCommand(Extrude);
        RevolveCommand          = new RelayCommand(Revolve);
        LoftCommand             = new RelayCommand(Loft);
        ShellCommand            = new RelayCommand(Shell);
        BooleanUnionCommand     = new RelayCommand(BooleanUnion);
        BooleanSubtractCommand  = new RelayCommand(BooleanSubtract);
        BooleanIntersectCommand = new RelayCommand(BooleanIntersect);
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
        FeatureNodes.Add(MakeNode("📦", "Part Studio 1", "root"));
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

        var node = MakeNode("📥", Path.GetFileNameWithoutExtension(path),
                            Path.GetExtension(path).TrimStart('.'));
        if (FeatureNodes.Count > 0)
            FeatureNodes[0].Children.Add(node);

        // Load geometry into viewport
        var safeName = node.Name.Replace("'", "");
        if (path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase) && ViewportService != null)
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var b64 = Convert.ToBase64String(bytes);
            await ViewportService.ExecuteScriptAsync($"viewer.loadSTL('{b64}', '{safeName}');");
        }
        else if (ViewportService != null)
        {
            // Non-STL formats: show a placeholder box so the viewport isn't empty
            await ViewportService.ExecuteScriptAsync($"viewer.addBox('{safeName}', 100, 100, 100); viewer.fitView();");
        }

        WindowTitle = $"My3DApp — {Path.GetFileName(path)}";
        StatusMessage = $"Imported: {Path.GetFileName(path)}";
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
        var first = _scene.Objects.FirstOrDefault();
        if (first is null) { StatusMessage = "Nothing to export."; return; }

        await _models.ExportAsync(first.Id, path);
        StatusMessage = $"Exported: {Path.GetFileName(path)}";
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────
    private void NewSketch()
    {
        if (FeatureNodes.Count == 0) return;
        var name = $"Sketch {FeatureNodes[0].Children.Count + 1}";
        var action = new AddSceneObjectAction(_scene, name, "sketch");
        _history.Push(action);
        var node = MakeNode("⬜", name, "sketch");
        node.SceneObjectId = _scene.Objects.FirstOrDefault(o => o.Name == name)?.Id;
        FeatureNodes[0].Children.Add(node);
        _ = ViewportService?.ExecuteScriptAsync(
            $"viewer.addSketchPlane('{name.Replace("'", "")}'); viewer.fitView();");
        StatusMessage = $"{name} — sketch plane added. Define profile in the AI panel.";
        RuntimeLog.Info("VM", $"New sketch '{name}' created.");
    }

    private void Extrude()
    {
        if (FeatureNodes.Count == 0) return;
        var name = $"Extrude {FeatureNodes[0].Children.Count + 1}";
        var action = new AddSceneObjectAction(_scene, name, "extrude");
        _history.Push(action);
        var node = MakeNode("⬆", name, "extrude");
        node.SceneObjectId = _scene.Objects.FirstOrDefault(o => o.Name == name)?.Id;
        FeatureNodes[0].Children.Add(node);
        // Push a default box to the viewport as a placeholder for the extrude
        _ = ViewportService?.ExecuteScriptAsync($"viewer.addBox('{name.Replace("'", "")}', 100, 50, 30); viewer.fitView();");
        StatusMessage = $"{name} added. Adjust dimensions in the properties panel.";
    }

    private void Revolve()
    {
        if (FeatureNodes.Count == 0) return;
        var name = $"Revolve {FeatureNodes[0].Children.Count + 1}";
        _history.Push(new AddSceneObjectAction(_scene, name, "revolve"));
        var node = MakeNode("↻", name, "revolve");
        node.SceneObjectId = _scene.Objects.FirstOrDefault(o => o.Name == name)?.Id;
        FeatureNodes[0].Children.Add(node);
        _ = ViewportService?.ExecuteScriptAsync($"viewer.addCylinder('{name.Replace("'", "")}', 40, 80, 32); viewer.fitView();");
        StatusMessage = $"{name} added — select profile and axis to refine.";
    }

    private void Loft()
    {
        if (FeatureNodes.Count == 0) return;
        var name = $"Loft {FeatureNodes[0].Children.Count + 1}";
        _history.Push(new AddSceneObjectAction(_scene, name, "loft"));
        var node = MakeNode("⤵", name, "loft");
        node.SceneObjectId = _scene.Objects.FirstOrDefault(o => o.Name == name)?.Id;
        FeatureNodes[0].Children.Add(node);
        _ = ViewportService?.ExecuteScriptAsync($"viewer.addBox('{name.Replace("'", "")}', 80, 120, 80); viewer.fitView();");
        StatusMessage = $"{name} added — select profiles to define the loft.";
    }

    private void Shell()         => StatusMessage = "Shell: select faces to remove.";
    private void BooleanUnion()
    {
        if (FeatureNodes.Count == 0) return;
        var name = $"Union {FeatureNodes[0].Children.Count + 1}";
        _history.Push(new AddSceneObjectAction(_scene, name, "boolean-union"));
        var node = MakeNode("⊕", name, "boolean-union");
        node.SceneObjectId = _scene.Objects.FirstOrDefault(o => o.Name == name)?.Id;
        FeatureNodes[0].Children.Add(node);
        StatusMessage = $"{name} — select two bodies to merge.";
    }

    private void BooleanSubtract()
    {
        if (FeatureNodes.Count == 0) return;
        var name = $"Subtract {FeatureNodes[0].Children.Count + 1}";
        _history.Push(new AddSceneObjectAction(_scene, name, "boolean-subtract"));
        var node = MakeNode("⊖", name, "boolean-subtract");
        node.SceneObjectId = _scene.Objects.FirstOrDefault(o => o.Name == name)?.Id;
        FeatureNodes[0].Children.Add(node);
        StatusMessage = $"{name} — select target body, then tool body.";
    }

    private void BooleanIntersect()
    {
        if (FeatureNodes.Count == 0) return;
        var name = $"Intersect {FeatureNodes[0].Children.Count + 1}";
        _history.Push(new AddSceneObjectAction(_scene, name, "boolean-intersect"));
        var node = MakeNode("⊗", name, "boolean-intersect");
        node.SceneObjectId = _scene.Objects.FirstOrDefault(o => o.Name == name)?.Id;
        FeatureNodes[0].Children.Add(node);
        StatusMessage = $"{name} — select bodies to intersect.";
    }
    private void AiGenerate()    => StatusMessage = "AI: enter a description in the AI panel →";
    private void FocusAiPanel()  => StatusMessage = "AI panel focused.";
    private void AddFeature()    => StatusMessage = "Select feature type to add.";
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
            if (root.Children.Remove(node))
            {
                // Sync scene graph and viewport
                if (node.SceneObjectId is { } id)
                {
                    var obj = _scene.Find(id);
                    if (obj != null)
                        _history.Push(new RemoveSceneObjectAction(_scene, obj));
                    else
                        _scene.Remove(id);
                }
                _ = ViewportService?.ExecuteScriptAsync(
                    $"viewer.removeObject('{node.Name.Replace("'", "")}');");
                StatusMessage = $"Deleted '{node.Name}'.";
                RuntimeLog.Info("VM", $"Deleted feature '{node.Name}'");
                return;
            }
        }
        StatusMessage = "Cannot delete root node.";
    }

    private void RenameFeature(FeatureNode node)
    {
        // Appends " (2)", " (3)" … until unique — real input dialog wired later
        var baseName = node.Name.Contains(" (") ? node.Name[..node.Name.LastIndexOf(" (")] : node.Name;
        int suffix = 2;
        string candidate;
        do { candidate = $"{baseName} ({suffix++})"; }
        while (AllNodes().Any(n => n != node && n.Name == candidate));
        node.Name = candidate;
        StatusMessage = $"Renamed to '{candidate}'.";
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
                await (ViewportService?.ExecuteScriptAsync(script) ?? Task.CompletedTask);
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
        const string prompt = "Generate a parametric sketch with dimensions. Describe the profile and provide a geometry-script to visualise it.";
        AiMessages.Add(new AiMessage { Role = "You", Content = "(Generate Sketch)" });
        AiIsThinking = true;
        StatusMessage = "AI generating sketch…";
        try
        {
            var context = BuildFeatureTreeContext();
            var reply = await _ai.ChatAsync(prompt, context);
            AiMessages.Add(new AiMessage { Role = "AI", Content = reply });
            if (_ai.LastGeometryScript is { } script)
                await (ViewportService?.ExecuteScriptAsync(script) ?? Task.CompletedTask);
        }
        finally { AiIsThinking = false; StatusMessage = "Ready"; }
    }

    private void AiClear()
    {
        _ai.ClearHistory();
        AiMessages.Clear();
        AiMessages.Add(new AiMessage { Role = "AI", Content = "Chat cleared. How can I help you design?" });
        StatusMessage = "AI chat cleared.";
    }

    public void Dispose()
    {
        ViewportService = null; // unsubscribes CursorPositionChanged
        _ai.Dispose();
    }

    private async Task AiSuggestFeatureAsync()
    {
        const string prompt = "Based on my current feature tree, what should I model next?";
        AiMessages.Add(new AiMessage { Role = "You", Content = "(Suggest next feature)" });
        AiIsThinking = true;
        StatusMessage = "AI analyzing part...";
        try
        {
            var context = BuildFeatureTreeContext();
            var reply = await _ai.ChatAsync(prompt, context);
            AiMessages.Add(new AiMessage { Role = "AI", Content = reply });
            if (_ai.LastGeometryScript is { } script)
                await (ViewportService?.ExecuteScriptAsync(script) ?? Task.CompletedTask);
        }
        finally
        {
            AiIsThinking = false;
            StatusMessage = "Ready";
        }
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
        sb.AppendLine($"{new string(' ', depth * 2)}{node.Icon} {node.Name} ({node.FeatureType})");
        foreach (var child in node.Children)
            AppendFeatureNode(sb, child, depth + 1);
    }
}
