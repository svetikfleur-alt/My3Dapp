using System.Collections.ObjectModel;
using System.Windows.Input;
using My3DApp.AvaloniaApp.Services;
using My3DApp.Backends;

namespace My3DApp.AvaloniaApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly AiBackend _ai = new();

    // ── Viewport ─────────────────────────────────────────────────────────────
    public ViewportService? ViewportService { get; set; }

    private bool _isViewportLoading = true;
    public bool IsViewportLoading
    {
        get => _isViewportLoading;
        set => SetField(ref _isViewportLoading, value);
    }

    // ── Window state ──────────────────────────────────────────────────────────
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

    // ── Feature Tree ─────────────────────────────────────────────────────────
    public ObservableCollection<FeatureNode> FeatureNodes { get; } = new()
    {
        new FeatureNode { Icon = "📦", Name = "Part Studio 1", FeatureType = "root", Children =
        {
            new FeatureNode { Icon = "⬜", Name = "Sketch 1", FeatureType = "sketch" },
            new FeatureNode { Icon = "⬆", Name = "Extrude 1", FeatureType = "extrude" },
        }}
    };

    // ── AI Panel ─────────────────────────────────────────────────────────────
    public ObservableCollection<AiMessage> AiMessages { get; } = new()
    {
        new AiMessage
        {
            Role = "AI",
            Content = "Hello! I'm your AI Design Assistant. Describe what you'd like to model and I'll help you build it."
        }
    };

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
        set => SetField(ref _aiIsThinking, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand NewPartStudioCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand SetViewCommand { get; }
    public ICommand NewSketchCommand { get; }
    public ICommand ExtrudeCommand { get; }
    public ICommand RevolveCommand { get; }
    public ICommand LoftCommand { get; }
    public ICommand ShellCommand { get; }
    public ICommand BooleanUnionCommand { get; }
    public ICommand BooleanSubtractCommand { get; }
    public ICommand BooleanIntersectCommand { get; }
    public ICommand AiGenerateCommand { get; }
    public ICommand AiSendCommand { get; }
    public ICommand FocusAiPanelCommand { get; }
    public ICommand AiGenerateSketchCommand { get; }
    public ICommand AiSuggestFeatureCommand { get; }
    public ICommand AddFeatureCommand { get; }
    public ICommand FitViewCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand WireframeCommand { get; }
    public ICommand SolidViewCommand { get; }
    public ICommand OpenDocsCommand { get; }
    public ICommand ShowAboutCommand { get; }

    public MainWindowViewModel()
    {
        NewPartStudioCommand   = new RelayCommand(NewPartStudio);
        OpenCommand            = new RelayCommand(Open);
        ExportCommand          = new RelayCommand(Export);
        ExitCommand            = new RelayCommand(Exit);
        UndoCommand            = new RelayCommand(Undo);
        RedoCommand            = new RelayCommand(Redo);
        SetViewCommand         = new RelayCommand(p => SetView(p as string ?? "iso"));
        NewSketchCommand       = new RelayCommand(NewSketch);
        ExtrudeCommand         = new RelayCommand(Extrude);
        RevolveCommand         = new RelayCommand(Revolve);
        LoftCommand            = new RelayCommand(Loft);
        ShellCommand           = new RelayCommand(Shell);
        BooleanUnionCommand    = new RelayCommand(BooleanUnion);
        BooleanSubtractCommand = new RelayCommand(BooleanSubtract);
        BooleanIntersectCommand = new RelayCommand(BooleanIntersect);
        AiGenerateCommand      = new RelayCommand(AiGenerate);
        AiSendCommand          = new RelayCommand(async () => await AiSendAsync());
        FocusAiPanelCommand    = new RelayCommand(FocusAiPanel);
        AiGenerateSketchCommand = new RelayCommand(async () => await AiGenerateSketchAsync());
        AiSuggestFeatureCommand = new RelayCommand(async () => await AiSuggestFeatureAsync());
        AddFeatureCommand      = new RelayCommand(AddFeature);
        FitViewCommand         = new RelayCommand(async () => await ViewportService?.FitViewAsync()!);
        ResetViewCommand       = new RelayCommand(async () => await ViewportService?.ResetViewAsync()!);
        WireframeCommand       = new RelayCommand(async () => await ViewportService?.SetWireframeAsync(true)!);
        SolidViewCommand       = new RelayCommand(async () => await ViewportService?.SetWireframeAsync(false)!);
        OpenDocsCommand        = new RelayCommand(() => OpenUrl("https://github.com/svetikfleur-alt/my3dapp"));
        ShowAboutCommand       = new RelayCommand(ShowAbout);
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────
    private void NewSketch()
    {
        StatusMessage = "Creating new sketch...";
        var node = new FeatureNode { Icon = "⬜", Name = $"Sketch {FeatureNodes[0].Children.Count + 1}", FeatureType = "sketch" };
        FeatureNodes[0].Children.Add(node);
        StatusMessage = "Sketch ready. Select a plane to begin.";
        RuntimeLog.Info("VM", "New sketch created.");
    }

    private void Extrude()
    {
        StatusMessage = "Extrude: select sketch to extrude.";
        var node = new FeatureNode { Icon = "⬆", Name = "Extrude", FeatureType = "extrude" };
        FeatureNodes[0].Children.Add(node);
    }

    private void Revolve() => StatusMessage = "Revolve: select profile and axis.";
    private void Loft()    => StatusMessage = "Loft: select profiles.";
    private void Shell()   => StatusMessage = "Shell: select faces to remove.";

    private void BooleanUnion()     => StatusMessage = "Boolean Union: select bodies.";
    private void BooleanSubtract()  => StatusMessage = "Boolean Subtract: select target and tool bodies.";
    private void BooleanIntersect() => StatusMessage = "Boolean Intersect: select bodies.";

    private void AiGenerate()   => StatusMessage = "AI: Enter a description in the AI panel.";
    private void FocusAiPanel() => StatusMessage = "AI panel focused.";
    private void AddFeature()   => StatusMessage = "Select feature type to add.";

    private void NewPartStudio()
    {
        FeatureNodes.Clear();
        FeatureNodes.Add(new FeatureNode { Icon = "📦", Name = "Part Studio 1", FeatureType = "root" });
        WindowTitle = "My3DApp — New Part Studio";
        StatusMessage = "New Part Studio created.";
    }

    private void Open()    => StatusMessage = "Open: not yet implemented.";
    private void Export()  => StatusMessage = "Export: not yet implemented.";
    private void Exit()    => Environment.Exit(0);
    private void Undo()    => StatusMessage = "Undo.";
    private void Redo()    => StatusMessage = "Redo.";

    private void SetView(string view)
    {
        StatusMessage = $"View: {view}";
        _ = ViewportService?.SetViewAsync(view);
    }

    private void ShowAbout()  => StatusMessage = "My3DApp — AI Native CAD  |  v0.1.0-alpha";
    private void OpenUrl(string url) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

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
            var reply = await _ai.ChatAsync(prompt);
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
        AiPrompt = "Generate a sketch for: ";
        StatusMessage = "Describe the sketch you want to generate.";
        await Task.CompletedTask;
    }

    private async Task AiSuggestFeatureAsync()
    {
        AiIsThinking = true;
        StatusMessage = "AI analyzing part...";
        try
        {
            var reply = await _ai.ChatAsync("Look at the current feature tree and suggest the next modeling step.");
            AiMessages.Add(new AiMessage { Role = "AI", Content = reply });
        }
        finally
        {
            AiIsThinking = false;
            StatusMessage = "Ready";
        }
    }
}
