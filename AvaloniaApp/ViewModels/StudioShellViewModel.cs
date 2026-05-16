using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia.Media;
using FormaCore.Core;
using FormaCore.Engine;
using My3DApp.AvaloniaApp.Controls;
using My3DApp.AvaloniaApp.Services;

namespace My3DApp.AvaloniaApp.ViewModels;

public sealed class StudioShellViewModel : ViewModelBase, IDisposable
{
    private const string IconBase = "avares://My3DApp/Assets/Icons/";
    private static readonly PrimitiveToolItemViewModel[] PrimitiveCatalog =
    [
        new("box", "Box", IconBase + "box.svg"),
        new("cylinder", "Cylinder", IconBase + "cylinder.svg"),
        new("sphere", "Sphere", IconBase + "sphere.svg"),
        new("cone", "Cone", IconBase + "cone.svg"),
        new("torus", "Torus", IconBase + "torus.svg"),
        new("pyramid", "Pyramid", IconBase + "pyramid.svg"),
        new("wedge", "Wedge", IconBase + "wedge.svg"),
        new("prism", "Prism", IconBase + "prism.svg"),
        new("capsule", "Capsule", IconBase + "capsule.svg"),
        new("hemisphere", "Hemisphere", IconBase + "hemisphere.svg"),
        new("ellipsoid", "Ellipsoid", IconBase + "ellipsoid.svg"),
        new("arrow", "Arrow", IconBase + "arrow-primitive.svg"),
        new("icosphere", "Icosphere", IconBase + "icosphere.svg"),
        new("tetrahedron", "Tetrahedron", IconBase + "tetrahedron.svg"),
        new("octahedron", "Octahedron", IconBase + "octahedron.svg"),
        new("icosahedron", "Icosahedron", IconBase + "icosahedron.svg")
    ];

    private bool _isDisposed;

    private StudioNotification? _currentNotification;
    private CancellationTokenSource? _autoDismissCts;

    private readonly AssistantChatService _assistantChatService = new();
    private readonly CadCommandParser _commandParser = new();
    private readonly StudioWorkspaceController _workspaceController = new();
    private readonly AppSettings _appSettings = AppSettings.Load();

    private AssistantRuntimeConfiguration _assistantConfiguration =
        AssistantRuntimeConfiguration.FromSelection("OpenAI", "GPT-5.4 mini");
    private bool _isSketchMode;
    private bool _isThreeDMode = true;
    private string _selectedAssistantMode = "Auto";
    private string _selectedAssistantProvider = "OpenAI";
    private string _selectedAssistantModel = "GPT-5.4 mini";
    private string _currentProjectPath = string.Empty;
    private string _projectTitle = "My3DApp Project / unsaved.my3dapp";
    private string _assistantInput = string.Empty;
    private string _assistantKeyInput = string.Empty;
    private string _assistantStatus = "Ready";
    private string _assistantConfigurationSummary = string.Empty;
    private bool _isAssistantBusy;
    private bool _isAssistantConfigured;
    private CadViewportCommand? _pendingCommand;
    private string _pendingCommandSummary = "No pending command.";
    private string _featureTreeSummary = "Reference geometry ready.";
    private string _featureListHeader = "Features (0)";
    private string _assistantWorkspaceSummary = "3D workspace";
    private string _assistantPlaneSummary = "Top plane";
    private string _assistantSelectionSummary = "No active selection";
    private string _selectedProfileSummary = "Closed profile not selected";
    private string _selectedProfilePlaneSummary = "Top plane";
    private string _featureTreeFilterText = string.Empty;
    private string _codeText = string.Empty;
    private string _logText = string.Empty;
    private int _selectedInspectorTabIndex;
    private int _selectedBottomTabIndex;
    private bool _canPrimitiveTools = true;
    private bool _canStartSketch = true;
    private bool _canFinishSketch;
    private bool _canSketchTools;
    private bool _canProfileTools;
    private bool _canLoftTools;
    private bool _canBooleanTools;
    private bool _canEdgeTools;
    private string _selectedSketchTool = "Rectangle";
    private string _sketchConstraintHint = "Constraints: Horizontal · Vertical · Coincident · Equal · Fix";
    private string _sketchDimensionHint = "Dimensions appear when a sketch is selected or committed.";
    private string _sketchCursorPosition = string.Empty;
    private bool _isConstructionModeActive;
    private bool _isSelectingSketchPlane;
    private bool _hasExplicitSketchBaseSelection;
    private readonly List<string> _recentPrimitiveKinds = ["box", "cylinder", "sphere"];

    public StudioShellViewModel()
    {
        FeatureNodes = [];
        RecentPrimitiveTools = [];
        Parameters = [];
        AssistantMessages = [];
        ActiveSketchConstraints = [];
        ActiveSketchDimensions = [];
        AssistantProviders = ["OpenAI", "Anthropic"];
        AssistantModels = [];

        AssistantModes =
        [
            "Auto",
            "Do",
            "Think",
            "Assist",
            "Think&Do"
        ];

        PartStudios = new ObservableCollection<PartStudioTabItem>();
        ResetAssistantModelsForProvider(_selectedAssistantProvider, preserveSelection: false);
        RefreshRecentPrimitiveTools();
        _workspaceController.WorkspaceChanged += OnWorkspaceChanged;
        _workspaceController.DocumentChanged += OnDocumentChanged;
        ApplyWorkspaceState(_workspaceController.CurrentState);
        RefreshDocumentTabs();
        RefreshAssistantConfiguration();
        SeedAssistantHistory();
    }

    public ObservableCollection<PartStudioTabItem> PartStudios { get; }

    public string DocumentName
    {
        get => _workspaceController.DocumentName;
        set
        {
            if (_workspaceController.DocumentName == value)
            {
                return;
            }
            _workspaceController.DocumentName = value;
            RaisePropertyChanged(nameof(DocumentName));
        }
    }

    public void AddPartStudioTab()
    {
        _workspaceController.AddPartStudio();
    }

    public void ActivatePartStudio(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        _workspaceController.SwitchPartStudio(name);
    }

    public void DeletePartStudioTab(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        _workspaceController.DeletePartStudio(name);
    }

    public void RenamePartStudioTab(string oldName, string newName)
    {
        _workspaceController.RenamePartStudio(oldName, newName);
    }

    public void CyclePartStudio(bool forward)
    {
        var names = _workspaceController.PartStudioNames;
        if (names.Count <= 1) return;
        var active = _workspaceController.ActivePartStudioName;
        var idx = names.ToList().FindIndex(n => string.Equals(n, active, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;
        var next = forward ? (idx + 1) % names.Count : (idx - 1 + names.Count) % names.Count;
        _workspaceController.SwitchPartStudio(names[next]);
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        RefreshDocumentTabs();
        RaisePropertyChanged(nameof(DocumentName));
        RaisePropertyChanged(nameof(WindowTitle));
    }

    private void RefreshDocumentTabs()
    {
        var names = _workspaceController.PartStudioNames;
        var active = _workspaceController.ActivePartStudioName;

        PartStudios.Clear();
        foreach (var n in names)
        {
            PartStudios.Add(new PartStudioTabItem(n, string.Equals(n, active, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public event EventHandler<ViewportRenderState>? ViewportStateChanged;

    public event EventHandler? FocusSelectionRequested;

    public ObservableCollection<SketchConstraintDisplayItem> ActiveSketchConstraints { get; }

    public ObservableCollection<SketchDimensionDisplayItem> ActiveSketchDimensions { get; }

    public bool HasActiveSketchConstraints => ActiveSketchConstraints.Count > 0;

    public bool HasActiveSketchDimensions => ActiveSketchDimensions.Count > 0;

    public ObservableCollection<FeatureNodeViewModel> FeatureNodes { get; }

    public ObservableCollection<PrimitiveToolItemViewModel> RecentPrimitiveTools { get; }

    public ObservableCollection<ParameterItemViewModel> Parameters { get; }

    public IReadOnlyList<string> AssistantModes { get; }

    public IReadOnlyList<CadRecipe> AssistantRecipes { get; } = CadRecipeLibrary.All;

    public void InsertAssistantRecipe(string recipeName)
    {
        var recipe = CadRecipeLibrary.Find(recipeName);
        if (recipe is null)
        {
            return;
        }

        AssistantInput = recipe.Example;
    }

    public ObservableCollection<AssistantMessageViewModel> AssistantMessages { get; }

    public ObservableCollection<string> AssistantProviders { get; }

    public ObservableCollection<string> AssistantModels { get; }

    public ViewportRenderState CurrentViewportState => _workspaceController.CurrentState.ViewportState;

    public string CurrentProjectPath
    {
        get => _currentProjectPath;
        private set => SetProperty(ref _currentProjectPath, value);
    }

    public string ProjectTitle => HasUnsavedChanges ? _projectTitle + " *" : _projectTitle;
    public string WindowTitle
    {
        get
        {
            var doc = _workspaceController.DocumentName;
            var studio = _workspaceController.ActivePartStudioName;
            var label = string.IsNullOrEmpty(studio) ? doc : $"{doc} — {studio}";
            return HasUnsavedChanges ? $"* {label} — My3DApp" : $"{label} — My3DApp";
        }
    }
    private string ProjectTitleBase
    {
        get => _projectTitle;
        set
        {
            if (SetProperty(ref _projectTitle, value))
            {
                RaisePropertyChanged(nameof(ProjectTitle));
                RaisePropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string FeatureTreeSummary
    {
        get => _featureTreeSummary;
        private set => SetProperty(ref _featureTreeSummary, value);
    }

    public string FeatureListHeader
    {
        get => _featureListHeader;
        private set => SetProperty(ref _featureListHeader, value);
    }

    public string FeatureTreeFilterText
    {
        get => _featureTreeFilterText;
        set
        {
            if (!SetProperty(ref _featureTreeFilterText, value))
            {
                return;
            }

            RebuildFeatureTree(_workspaceController.CurrentState.Project, _workspaceController.CurrentState.CompileResult);
        }
    }

    public string AssistantWorkspaceSummary
    {
        get => _assistantWorkspaceSummary;
        private set => SetProperty(ref _assistantWorkspaceSummary, value);
    }

    public string AssistantPlaneSummary
    {
        get => _assistantPlaneSummary;
        private set => SetProperty(ref _assistantPlaneSummary, value);
    }

    public string AssistantSelectionSummary
    {
        get => _assistantSelectionSummary;
        private set => SetProperty(ref _assistantSelectionSummary, value);
    }

    public string SelectedProfileSummary
    {
        get => _selectedProfileSummary;
        private set => SetProperty(ref _selectedProfileSummary, value);
    }

    public string SelectedProfilePlaneSummary
    {
        get => _selectedProfilePlaneSummary;
        private set => SetProperty(ref _selectedProfilePlaneSummary, value);
    }

    public string CodeText
    {
        get => _codeText;
        private set => SetProperty(ref _codeText, value);
    }

    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    public void AppendToLog(string message)
    {
        var ts = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var line = $"[{ts}] {message}";
        LogText = string.IsNullOrEmpty(LogText) ? line : LogText + Environment.NewLine + line;
    }

    // ── Notification strip ──────────────────────────────────────────────────

    public bool HasNotification => _currentNotification is not null;

    public string NotificationText => _currentNotification?.Text ?? string.Empty;

    public IBrush NotificationBackground => _currentNotification?.Severity switch
    {
        NotificationSeverity.Info    => new SolidColorBrush(Avalonia.Media.Color.Parse("#1f6e1f")),
        NotificationSeverity.Warning => new SolidColorBrush(Avalonia.Media.Color.Parse("#7a5500")),
        NotificationSeverity.Error   => new SolidColorBrush(Avalonia.Media.Color.Parse("#8a1500")),
        _                            => Avalonia.Media.Brushes.Transparent
    };

    public IBrush NotificationForeground => Avalonia.Media.Brushes.White;

    public void ShowNotification(string text, NotificationSeverity severity)
    {
        if (_currentNotification?.Text == text && _currentNotification.Severity == severity)
            return;

        _autoDismissCts?.Cancel();
        _autoDismissCts = null;
        _currentNotification = new StudioNotification(text, severity);
        RaisePropertyChanged(nameof(HasNotification));
        RaisePropertyChanged(nameof(NotificationText));
        RaisePropertyChanged(nameof(NotificationBackground));

        if (severity == NotificationSeverity.Info)
        {
            var cts = new CancellationTokenSource();
            _autoDismissCts = cts;
            _ = AutoDismissAsync(cts.Token);
        }
    }

    public void DismissNotification()
    {
        _autoDismissCts?.Cancel();
        _autoDismissCts = null;
        _currentNotification = null;
        RaisePropertyChanged(nameof(HasNotification));
        RaisePropertyChanged(nameof(NotificationText));
        RaisePropertyChanged(nameof(NotificationBackground));
    }

    private async Task AutoDismissAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(5000, cancellationToken);
            DismissNotification();
        }
        catch (OperationCanceledException) { }
    }

    public int SelectedInspectorTabIndex
    {
        get => _selectedInspectorTabIndex;
        set => SetProperty(ref _selectedInspectorTabIndex, value);
    }

    public int SelectedBottomTabIndex
    {
        get => _selectedBottomTabIndex;
        set => SetProperty(ref _selectedBottomTabIndex, value);
    }

    public bool CanPrimitiveTools
    {
        get => _canPrimitiveTools;
        private set => SetProperty(ref _canPrimitiveTools, value);
    }

    public bool CanStartSketch
    {
        get => _canStartSketch;
        private set => SetProperty(ref _canStartSketch, value);
    }

    public bool CanFinishSketch
    {
        get => _canFinishSketch;
        private set => SetProperty(ref _canFinishSketch, value);
    }

    public bool CanCancelSketch => CanFinishSketch;

    public bool CanProfileTools
    {
        get => _canProfileTools;
        private set => SetProperty(ref _canProfileTools, value);
    }

    public bool CanSketchTools
    {
        get => _canSketchTools;
        private set => SetProperty(ref _canSketchTools, value);
    }

    public bool CanBooleanTools
    {
        get => _canBooleanTools;
        private set => SetProperty(ref _canBooleanTools, value);
    }

    public bool CanLoftTools
    {
        get => _canLoftTools;
        private set => SetProperty(ref _canLoftTools, value);
    }

    public bool CanEdgeTools
    {
        get => _canEdgeTools;
        private set => SetProperty(ref _canEdgeTools, value);
    }

    public bool CanUndo => _workspaceController.CanUndo;
    public bool CanRedo => _workspaceController.CanRedo;
    public bool HasUnsavedChanges => _workspaceController.HasUnsavedChanges;

    public string StatusBarText
    {
        get
        {
            var project = _workspaceController.CurrentState.Project;
            var bodyCount = project.Scene.Bodies.Count(b => b.Visible);
            var units = string.IsNullOrWhiteSpace(project.Units) ? "mm" : project.Units;
            var dirty = HasUnsavedChanges ? "● " : "";

            if (IsSketchMode)
            {
                var plane = AssistantPlaneSummary;
                var cursor = string.IsNullOrEmpty(SketchCursorPosition) ? "" : $"  ·  {SketchCursorPosition}";
                var hint = string.IsNullOrEmpty(SketchWorkflowHint) ? "" : $"  ·  {SketchWorkflowHint}";
                return $"{dirty}Sketch on {plane}  ·  {units}{cursor}{hint}";
            }

            var sel = _multiSelectedBodyIds.Count > 1
                ? $"{_multiSelectedBodyIds.Count} bodies selected"
                : project.Selection.IsEmpty ? "Nothing selected" : project.Selection.Name ?? "Selection";
            return $"{dirty}3D  ·  {bodyCount} bod{(bodyCount == 1 ? "y" : "ies")}  ·  {units}  ·  {sel}";
        }
    }

    public bool CanSketchPrimaryAction => CanFinishSketch || CanPrimitiveTools;

    public bool CanStartSketchFromCurrentSelection => CanStartSketch && (HasExplicitSketchBaseSelection || _selectedFacePlane is not null);

    public string SketchPrimaryGlyph => CanFinishSketch ? "[]" : "/";

    public string SketchPrimaryLabel => CanFinishSketch ? "Finish Sketch" : "Start Sketch";

    public string SketchPrimaryToolTip =>
        CanFinishSketch
            ? "Finish Sketch"
            : "Start Sketch";

    public string SketchPrimarySummary =>
        IsSelectingSketchPlane
            ? "Select Top, Front, or Right directly in the viewport to start the sketch."
            :
        CanFinishSketch
            ? $"Editing {AssistantPlaneSummary.ToLowerInvariant()} with the {SelectedSketchTool.ToLowerInvariant()} tool."
            :
        CanStartSketchFromCurrentSelection
            ? $"Start Sketch will use the selected {AssistantPlaneSummary.ToLowerInvariant()}."
            : "Click Start Sketch, then select Top, Front, or Right directly in the viewport.";

    public string ActiveSketchTaskTitle =>
        IsSketchMode
            ? "Sketch feature"
            : "3D workspace";

    public string ActiveSketchTaskSummary =>
        IsSelectingSketchPlane
            ? "Click a reference plane in the viewport."
            :
        IsSketchMode
            ? $"{ViewportSketchSessionTitle} | {SelectedSketchTool} tool | {SketchDefinitionHeadline}"
            : "Select a reference plane, then begin a sketch.";

    public string ActiveSketchTaskDetails =>
        IsSketchMode
            ? $"{SketchDefinitionDetail} | {SketchConstraintHint} | {SketchDimensionHint}"
            : "Reference geometry stays available in the tree for the next feature.";

    public string ViewportSketchEntryLabel =>
        IsSelectingSketchPlane
            ? "Select sketch plane"
            :
        CanStartSketchFromCurrentSelection
            ? $"Start Sketch on {AssistantPlaneSummary}"
            : "Start Sketch";

    public string ViewportSketchSessionTitle =>
        IsSketchMode
            ? $"Sketch on {AssistantPlaneSummary}"
            : "3D Workspace";

    public string ViewportSketchSessionSummary =>
        IsSelectingSketchPlane
            ? "Select a reference plane in the viewport to enter Sketch mode."
            :
        IsSketchMode
            ? $"{SelectedSketchTool} tool active. Click in the viewport to place sketch geometry, then finish sketch."
            : "Select a reference plane to begin a sketch-driven feature.";

    private int _sketchDofValue;
    public int SketchDofValue
    {
        get => _sketchDofValue;
        private set
        {
            if (!SetProperty(ref _sketchDofValue, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(SketchDofLabel));
            RaisePropertyChanged(nameof(SketchDefinitionHeadline));
            RaisePropertyChanged(nameof(SketchDefinitionDetail));
            RaisePropertyChanged(nameof(SketchDofStatusClass));
            RaisePropertyChanged(nameof(SketchDofBrush));
            RaisePropertyChanged(nameof(IsDofFullyConstrained));
            RaisePropertyChanged(nameof(IsDofOverconstrained));
            RaisePropertyChanged(nameof(IsDofUnderconstrained));
            RaisePropertyChanged(nameof(ActiveSketchTaskSummary));
            RaisePropertyChanged(nameof(ActiveSketchTaskDetails));
        }
    }

    public string SketchDofLabel => SketchDofValue switch
    {
        0  => "● Fully constrained",
        < 0 => "⚠ Overconstrained",
        _  => $"○ {SketchDofValue} DOF remaining"
    };

    public string SketchDefinitionHeadline => SketchDofValue switch
    {
        0 => "Fully defined",
        < 0 => "Overdefined",
        _ => "Underdefined"
    };

    public string SketchDefinitionDetail => SketchDofValue switch
    {
        0 => "Geometry is locked by dimensions and constraints.",
        < 0 => "Remove redundant constraints or conflicting driving dimensions.",
        _ => "Add dimensions or constraints to lock the remaining geometry."
    };

    public string SketchDofStatusClass => SketchDofValue switch
    {
        0  => "DofFullyConstrained",
        < 0 => "DofOverconstrained",
        _  => "DofUnderconstrained"
    };

    public IBrush SketchDofBrush => SketchDofValue switch
    {
        0   => new SolidColorBrush(Avalonia.Media.Color.Parse("#4CAF50")),
        < 0 => new SolidColorBrush(Avalonia.Media.Color.Parse("#FF9800")),
        _   => new SolidColorBrush(Avalonia.Media.Color.Parse("#909090"))
    };

    public bool IsDofFullyConstrained => IsSketchMode && SketchDofValue == 0;
    public bool IsDofOverconstrained => IsSketchMode && SketchDofValue < 0;
    public bool IsDofUnderconstrained => IsSketchMode && SketchDofValue > 0;

    public string SketchWorkflowHint =>
        IsSelectingSketchPlane
            ? "Select Top, Front, or Right in the viewport. Sketch will begin on that plane."
            :
        IsSketchMode
            ? $"Click on {AssistantPlaneSummary.ToLowerInvariant()} to place {SelectedSketchTool.ToLowerInvariant()} geometry, then finish the sketch to use features."
            : "3D workspace active. Select a reference plane, then start sketch.";

    public string SketchConstraintHint
    {
        get => _sketchConstraintHint;
        private set => SetProperty(ref _sketchConstraintHint, value);
    }

    public string SketchDimensionHint
    {
        get => _sketchDimensionHint;
        private set => SetProperty(ref _sketchDimensionHint, value);
    }

    public string SketchCursorPosition
    {
        get => _sketchCursorPosition;
        private set
        {
            if (SetProperty(ref _sketchCursorPosition, value))
                RaisePropertyChanged(nameof(StatusBarText));
        }
    }

    public string SelectedSketchTool
    {
        get => _selectedSketchTool;
        private set
        {
            if (!SetProperty(ref _selectedSketchTool, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsLineSketchToolSelected));
            RaisePropertyChanged(nameof(IsRectangleSketchToolSelected));
            RaisePropertyChanged(nameof(IsCircleSketchToolSelected));
            RaisePropertyChanged(nameof(IsArcSketchToolSelected));
            RaisePropertyChanged(nameof(IsPointSketchToolSelected));
            RaisePropertyChanged(nameof(IsPolygonSketchToolSelected));
            RaisePropertyChanged(nameof(IsSlotSketchToolSelected));
            RaisePropertyChanged(nameof(IsSplineSketchToolSelected));
            RaisePropertyChanged(nameof(IsMirrorSketchToolSelected));
            RaisePropertyChanged(nameof(IsTrimSketchToolSelected));
            RaisePropertyChanged(nameof(IsOffsetSketchToolSelected));
            RaisePropertyChanged(nameof(IsFillet2dSketchToolSelected));
            RaisePropertyChanged(nameof(IsTransformSketchToolSelected));
            RaisePropertyChanged(nameof(IsAngleDimensionToolSelected));
            RaisePropertyChanged(nameof(IsLinearDimensionToolSelected));
            RaisePropertyChanged(nameof(IsRadiusDimensionToolSelected));
            RaisePropertyChanged(nameof(SketchPrimarySummary));
            RaisePropertyChanged(nameof(SketchWorkflowHint));
            RaisePropertyChanged(nameof(ViewportSketchSessionSummary));
        }
    }

    public bool IsLineSketchToolSelected => string.Equals(SelectedSketchTool, "Line", StringComparison.OrdinalIgnoreCase);

    public bool IsRectangleSketchToolSelected => string.Equals(SelectedSketchTool, "Rectangle", StringComparison.OrdinalIgnoreCase);

    public bool IsCircleSketchToolSelected => string.Equals(SelectedSketchTool, "Circle", StringComparison.OrdinalIgnoreCase);

    public bool IsArcSketchToolSelected => string.Equals(SelectedSketchTool, "Arc", StringComparison.OrdinalIgnoreCase);

    public bool IsPointSketchToolSelected => string.Equals(SelectedSketchTool, "Point", StringComparison.OrdinalIgnoreCase);

    public bool IsPolygonSketchToolSelected => string.Equals(SelectedSketchTool, "Polygon", StringComparison.OrdinalIgnoreCase);

    public bool IsSlotSketchToolSelected => string.Equals(SelectedSketchTool, "Slot", StringComparison.OrdinalIgnoreCase);

    public bool IsSplineSketchToolSelected => string.Equals(SelectedSketchTool, "Spline", StringComparison.OrdinalIgnoreCase);

    public bool IsMirrorSketchToolSelected => string.Equals(SelectedSketchTool, "Mirror", StringComparison.OrdinalIgnoreCase);

    public bool IsTrimSketchToolSelected => string.Equals(SelectedSketchTool, "Trim", StringComparison.OrdinalIgnoreCase);

    public bool IsOffsetSketchToolSelected => string.Equals(SelectedSketchTool, "Offset", StringComparison.OrdinalIgnoreCase);

    public bool IsFillet2dSketchToolSelected => string.Equals(SelectedSketchTool, "Fillet2d", StringComparison.OrdinalIgnoreCase);

    public bool IsTransformSketchToolSelected => string.Equals(SelectedSketchTool, "Transform", StringComparison.OrdinalIgnoreCase);

    public bool IsAngleDimensionToolSelected => string.Equals(SelectedSketchTool, "AngleDimension", StringComparison.OrdinalIgnoreCase);

    public bool IsLinearDimensionToolSelected => string.Equals(SelectedSketchTool, "LinearDimension", StringComparison.OrdinalIgnoreCase);

    public bool IsRadiusDimensionToolSelected => string.Equals(SelectedSketchTool, "RadiusDimension", StringComparison.OrdinalIgnoreCase);

    public bool IsConstructionModeActive
    {
        get => _isConstructionModeActive;
        private set => SetProperty(ref _isConstructionModeActive, value);
    }

    public bool IsSelectingSketchPlane
    {
        get => _isSelectingSketchPlane;
        private set
        {
            if (!SetProperty(ref _isSelectingSketchPlane, value))
            {
                return;
            }

            RaiseSketchFlowProperties();
        }
    }

    public bool HasExplicitSketchBaseSelection
    {
        get => _hasExplicitSketchBaseSelection;
        private set
        {
            if (!SetProperty(ref _hasExplicitSketchBaseSelection, value))
            {
                return;
            }

            RaiseSketchFlowProperties();
        }
    }

    private void RaiseSketchFlowProperties()
    {
        RaisePropertyChanged(nameof(CanSketchPrimaryAction));
        RaisePropertyChanged(nameof(CanStartSketchFromCurrentSelection));
        RaisePropertyChanged(nameof(SketchPrimarySummary));
        RaisePropertyChanged(nameof(ActiveSketchTaskSummary));
        RaisePropertyChanged(nameof(SketchWorkflowHint));
        RaisePropertyChanged(nameof(ViewportSketchEntryLabel));
        RaisePropertyChanged(nameof(ViewportSketchSessionSummary));
    }

    public string SelectedAssistantMode
    {
        get => _selectedAssistantMode;
        set
        {
            if (!SetProperty(ref _selectedAssistantMode, value))
            {
                return;
            }

            AssistantStatus = IsAssistantBusy ? $"Running ({_selectedAssistantMode})" : BuildIdleStatusText();
        }
    }

    public string SelectedAssistantProvider
    {
        get => _selectedAssistantProvider;
        set
        {
            if (!SetProperty(ref _selectedAssistantProvider, value))
            {
                return;
            }

            ResetAssistantModelsForProvider(value, preserveSelection: true);
            RefreshAssistantConfiguration();
        }
    }

    public string SelectedAssistantModel
    {
        get => _selectedAssistantModel;
        set
        {
            if (!SetProperty(ref _selectedAssistantModel, value))
            {
                return;
            }

            RefreshAssistantConfiguration();
        }
    }

    public string AssistantInput
    {
        get => _assistantInput;
        set
        {
            if (!SetProperty(ref _assistantInput, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(CanSendAssistant));
        }
    }

    public string AssistantKeyInput
    {
        get => _assistantKeyInput;
        set
        {
            if (!SetProperty(ref _assistantKeyInput, value))
            {
                return;
            }

            RefreshAssistantConfiguration();
        }
    }

    public string AssistantStatus
    {
        get => _assistantStatus;
        private set => SetProperty(ref _assistantStatus, value);
    }

    public string AssistantConfigurationSummary
    {
        get => _assistantConfigurationSummary;
        private set => SetProperty(ref _assistantConfigurationSummary, value);
    }

    public bool IsAssistantConfigured
    {
        get => _isAssistantConfigured;
        private set
        {
            if (!SetProperty(ref _isAssistantConfigured, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(AssistantStatusBadge));
            RaisePropertyChanged(nameof(AssistantInputHint));
            RaisePropertyChanged(nameof(ShowAssistantInactiveState));
        }
    }

    public bool IsAssistantBusy
    {
        get => _isAssistantBusy;
        private set
        {
            if (!SetProperty(ref _isAssistantBusy, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(CanSendAssistant));
            RaisePropertyChanged(nameof(AssistantStatusBadge));
            RaisePropertyChanged(nameof(CanApplyPendingCommand));
        }
    }

    public bool CanSendAssistant => !IsAssistantBusy && !string.IsNullOrWhiteSpace(AssistantInput);

    public bool HasPendingCommand => _pendingCommand is not null;

    public bool CanApplyPendingCommand => HasPendingCommand && !IsAssistantBusy;

    public bool HasAssistantMessages => AssistantMessages.Count > 0;

    public bool ShowAssistantInactiveState => !IsAssistantConfigured;

    public string AssistantInactiveTitle => "Assistant unavailable";

    public string AssistantInactiveSummary =>
        !string.IsNullOrWhiteSpace(_assistantConfiguration.ValidationError) &&
        !_assistantConfiguration.IsConfigured
            ? $"{SelectedAssistantProvider} is not connected yet. {_assistantConfiguration.MissingKeyHint} Local CAD commands still work without remote AI."
            : "Choose a provider and model below, then connect a provider key when you are ready. Local CAD commands still work without remote AI.";

    public string PendingCommandSummary
    {
        get => _pendingCommandSummary;
        private set => SetProperty(ref _pendingCommandSummary, value);
    }

    public string AssistantStatusBadge
    {
        get
        {
            if (IsAssistantBusy)
            {
                return "RUNNING";
            }

            return IsAssistantConfigured ? "ONLINE" : "OFFLINE";
        }
    }

    public string AssistantInputHint =>
        IsAssistantConfigured
            ? "Enter sends. Shift+Enter for a newline."
            : "Local CAD commands still work. Remote AI activates when a provider key is available.";

    public string WorkspaceModeBadge => IsSketchMode ? "SKETCH" : "3D";

    public string WorkspaceModeSummary =>
        IsSketchMode
            ? $"Sketch editing on {AssistantPlaneSummary.ToLowerInvariant()}"
            : "3D solid modeling workspace";

    public bool IsSketchMode
    {
        get => _isSketchMode;
        set
        {
            if (!SetProperty(ref _isSketchMode, value))
            {
                return;
            }

            if (value)
            {
                IsThreeDMode = false;
            }

            RaisePropertyChanged(nameof(WorkspaceModeBadge));
            RaisePropertyChanged(nameof(WorkspaceModeSummary));
        }
    }

    public bool IsThreeDMode
    {
        get => _isThreeDMode;
        set
        {
            if (!SetProperty(ref _isThreeDMode, value))
            {
                return;
            }

            if (value)
            {
                IsSketchMode = false;
            }

            RaisePropertyChanged(nameof(WorkspaceModeBadge));
            RaisePropertyChanged(nameof(WorkspaceModeSummary));
        }
    }

    public void RefreshAssistantConfiguration()
    {
        var keyOverride = _assistantKeyInput.Trim().Length > 0 ? _assistantKeyInput.Trim() : null;
        _assistantConfiguration = _assistantChatService.ReadConfiguration(
            SelectedAssistantProvider,
            SelectedAssistantModel,
            keyOverride);
        IsAssistantConfigured = _assistantConfiguration.IsConfigured;

        UpdateAssistantShellSummary();
        AssistantStatus = IsAssistantBusy ? $"Running ({SelectedAssistantMode})" : BuildIdleStatusText();
        RaisePropertyChanged(nameof(AssistantInactiveSummary));
        RaisePropertyChanged(nameof(ShowAssistantInactiveState));
    }

    public void ClearAssistantHistory()
    {
        AssistantMessages.Clear();
        ClearPendingCommand();
        SeedAssistantHistory();
        RaisePropertyChanged(nameof(HasAssistantMessages));
        RaisePropertyChanged(nameof(ShowAssistantInactiveState));
    }

    public async Task CreatePrimitiveAsync(string primitiveKind, CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.AddPrimitive, PrimitiveKind: primitiveKind),
            switchToProperties: true,
            cancellationToken);
    }

    public void RecordRecentPrimitive(string primitiveKind)
    {
        if (string.IsNullOrWhiteSpace(primitiveKind))
        {
            return;
        }

        _recentPrimitiveKinds.RemoveAll(kind => string.Equals(kind, primitiveKind, StringComparison.OrdinalIgnoreCase));
        _recentPrimitiveKinds.Insert(0, primitiveKind);

        while (_recentPrimitiveKinds.Count > 3)
        {
            _recentPrimitiveKinds.RemoveAt(_recentPrimitiveKinds.Count - 1);
        }

        foreach (var fallback in PrimitiveCatalog.Select(item => item.Kind))
        {
            if (_recentPrimitiveKinds.Count >= 3)
            {
                break;
            }

            if (_recentPrimitiveKinds.Any(kind => string.Equals(kind, fallback, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _recentPrimitiveKinds.Add(fallback);
        }

        RefreshRecentPrimitiveTools();
    }

    public async Task StartSketchAsync(string? plane = null, CancellationToken cancellationToken = default)
    {
        IsSelectingSketchPlane = false;
        HasExplicitSketchBaseSelection = false;
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.StartSketch, Plane: plane),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task StartSketchOnPlaneAsync(Guid planeId, string planeName, CancellationToken cancellationToken = default)
    {
        // Select the plane so the engine knows which plane to sketch on, then start.
        _workspaceController.SelectReferencePlane(planeId, planeName);
        await StartSketchAsync(planeName, cancellationToken);
    }

    public IReadOnlyList<(Guid Id, string Name)> GetAvailablePlanes()
    {
        var project = _workspaceController.CurrentState.Project;
        return project.Scene.ReferencePlanes
            .Where(p => p.Visible)
            .Select(p => (p.Id, p.Name))
            .ToList();
    }

    public void BeginSketchPlaneSelection()
    {
        if (IsSketchMode)
        {
            return;
        }

        IsSelectingSketchPlane = true;
        HasExplicitSketchBaseSelection = false;
        AppendToLog("Start Sketch: select a reference plane in the viewport or feature tree.");
        ShowPropertiesPanel();
    }

    public void CancelSketchPlaneSelection()
    {
        IsSelectingSketchPlane = false;
        HasExplicitSketchBaseSelection = false;
    }

    public async Task EditSketchAsync(Guid sketchId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.EditSketch(sketchId);
        if (!result.Success)
        {
            AddMessage("system", result.Message);
            ShowAssistantPanel();
        }

        await Task.CompletedTask;
    }

    public async Task ApplySketchConstraintAsync(string constraintName, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.ApplySketchConstraint(constraintName);
        if (!result.Success)
        {
            AddMessage("system", result.Message);
            ShowAssistantPanel();
        }

        await Task.CompletedTask;
    }

    public void DeleteSketchConstraint(int index)
    {
        var result = _workspaceController.DeleteSketchConstraint(index);
        if (!result.Success)
        {
            AddMessage("system", result.Message);
            ShowAssistantPanel();
        }
    }

    public async Task ToggleConstructionModeAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.ToggleConstructionMode();
        IsConstructionModeActive = _workspaceController.IsConstructionModeActive;

        if (!result.Success)
        {
            AddMessage("system", result.Message);
            ShowAssistantPanel();
        }

        await Task.CompletedTask;
    }

    public async Task EditSketchDimensionAsync(Guid entityId, string key, double value, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.EditSketchEntityValue(entityId, key, value);
        if (!result.Success)
        {
            AddMessage("system", result.Message);
            ShowAssistantPanel();
        }

        await Task.CompletedTask;
    }

    public async Task ApplySketchEntityDeleteAsync(Guid entityId)
    {
        var result = _workspaceController.DeleteSketchEntity(entityId);
        if (!result.Success)
        {
            AddMessage("system", result.Message);
            ShowAssistantPanel();
        }

        await Task.CompletedTask;
    }

    public async Task ApplySketchEntityConstructionAsync(Guid entityId)
    {
        var result = _workspaceController.SetSketchEntityConstruction(entityId);
        if (!result.Success)
        {
            AddMessage("system", result.Message);
            ShowAssistantPanel();
        }

        await Task.CompletedTask;
    }

    public (string EntityType, IReadOnlyList<CadParameter> Parameters)? GetSketchEntityParameters(Guid entityId)
        => _workspaceController.GetSketchEntityParameters(entityId);

    public async Task RefreshViewportAsync()
    {
        await Task.CompletedTask;
    }

    public StudioWorkspaceController Workspace => _workspaceController;

    public async Task FinishSketchAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.FinishSketch),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task CancelSketchAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.CancelSketch),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task SelectSketchToolAsync(string sketchTool, CancellationToken cancellationToken = default)
    {
        await SelectSketchToolAsync(sketchTool, 0d, cancellationToken);
    }

    public async Task SelectSketchToolAsync(string sketchTool, double toolParam, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sketchTool))
        {
            return;
        }

        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.SetSketchTool, SketchTool: sketchTool, Distance: toolParam),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task HandleViewportSketchPlacementAsync(double u, double v, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteWorkspaceCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.PlaceSketchAt, U: u, V: v),
            cancellationToken);
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }
    }

    public async Task HandleViewportSketchPreviewAsync(double u, double v, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        SketchCursorPosition = $"U {u:0.##}  V {v:0.##}";

        await ExecuteWorkspaceCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.UpdateSketchPreview, U: u, V: v),
            cancellationToken);
    }

    public async Task ClearViewportSketchPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        SketchCursorPosition = string.Empty;

        await ExecuteWorkspaceCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.ClearSketchPreview),
            cancellationToken);
    }

    public async Task CancelSketchStepAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await ExecuteWorkspaceCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.CancelSketchStep),
            cancellationToken);
    }

    public ViewportRenderBody? ComputeExtrudePreview(double distance, bool reverseDirection) =>
        _workspaceController.ComputeExtrudePreview(distance, reverseDirection);

    public async Task ExtrudeSelectedSketchAsync(CancellationToken cancellationToken = default)
    {
        await ExtrudeSelectedSketchAsync(8d, cancellationToken);
    }

    public async Task ExtrudeSelectedSketchAsync(double distance, CancellationToken cancellationToken = default)
    {
        await ExtrudeSelectedSketchAsync(distance, CadExtrudeOperation.NewBody, Guid.Empty, cancellationToken);
    }

    public async Task ExtrudeSelectedSketchAsync(double distance, CadExtrudeOperation operation, Guid targetBodyId, CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(
                CadViewportCommandKind.ExtrudeSketch,
                Distance: distance,
                ExtrudeOp: operation.ToString(),
                TargetBodyId: targetBodyId),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task RevolveSelectedSketchAsync(CancellationToken cancellationToken = default)
    {
        await RevolveSelectedSketchAsync(360d, "y", cancellationToken);
    }

    public async Task RevolveSelectedSketchAsync(double angle, CancellationToken cancellationToken = default)
    {
        await RevolveSelectedSketchAsync(angle, "y", cancellationToken);
    }

    public async Task RevolveSelectedSketchAsync(double angle, string axis, CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.RevolveSketch, Distance: angle, Axis: axis),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task SweepSelectedSketchAsync(double distance = 20d, double twistDegrees = 0d, CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.SweepSketch, Distance: distance, U: twistDegrees),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task FilletSelectedBodyAsync(double radius = 2d, CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.FilletBody, Distance: radius),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task ChamferSelectedBodyAsync(double distance = 2d, CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.ChamferBody, Distance: distance),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task LinearPatternSelectedBodyAsync(int count = 3, double spacing = 20d, string axis = "x", CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.LinearPatternBody, Distance: count, U: spacing, Axis: axis),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task CircularPatternSelectedBodyAsync(int count = 4, double totalAngle = 360d, string axis = "y", CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.CircularPatternBody, Distance: count, U: totalAngle, Axis: axis),
            switchToProperties: true,
            cancellationToken);
    }

    /// <summary>Returns current params of a CircularPatternFeature by ID, or null if not found.</summary>
    public (int Count, double TotalAngle, string Axis)? GetCircularPatternParams(Guid featureId)
    {
        return _workspaceController.FindCircularPatternParams(featureId);
    }

    public async Task UpdateCircularPatternAsync(Guid featureId, int count, double totalAngle, string axis, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.EditCircularPatternFeature(featureId, count, totalAngle, axis);
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }
    }

    public double? GetFilletRadius(Guid featureId) => _workspaceController.FindFilletRadius(featureId);

    public async Task UpdateFilletAsync(Guid featureId, double radius)
    {
        var result = _workspaceController.EditFilletFeature(featureId, radius);
        if (!result.Success) { ShowAssistantPanel(); AddMessage("system", result.Message); }
    }

    public double? GetChamferDistance(Guid featureId) => _workspaceController.FindChamferDistance(featureId);

    public async Task UpdateChamferAsync(Guid featureId, double distance)
    {
        var result = _workspaceController.EditChamferFeature(featureId, distance);
        if (!result.Success) { ShowAssistantPanel(); AddMessage("system", result.Message); }
    }

    public double? GetShellThickness(Guid featureId) => _workspaceController.FindShellThickness(featureId);

    public async Task UpdateShellAsync(Guid featureId, double thickness)
    {
        var result = _workspaceController.EditShellFeature(featureId, thickness);
        if (!result.Success) { ShowAssistantPanel(); AddMessage("system", result.Message); }
    }

    public (double Depth, bool ReverseDirection, CadExtrudeOperation Operation, Guid TargetBodyId)? GetExtrudeParams(Guid featureId)
        => _workspaceController.FindExtrudeParams(featureId);

    public async Task UpdateExtrudeAsync(Guid featureId, double depth, bool reverseDirection, CadExtrudeOperation operation, Guid targetBodyId)
    {
        var result = _workspaceController.EditExtrudeFeature(featureId, depth, reverseDirection, operation, targetBodyId);
        if (!result.Success) { ShowAssistantPanel(); AddMessage("system", result.Message); }
    }

    public (double AngleDegrees, string Axis)? GetRevolveParams(Guid featureId)
        => _workspaceController.FindRevolveParams(featureId);

    public string? GetMirrorParams(Guid featureId)
        => _workspaceController.FindMirrorParams(featureId);

    public async Task UpdateMirrorAsync(Guid featureId, string axis)
    {
        var result = _workspaceController.EditMirrorFeature(featureId, axis);
        if (!result.Success) { ShowAssistantPanel(); AddMessage("system", result.Message); }
    }

    public IReadOnlyList<(Guid Id, string Name)> GetClosedSketchProfiles()
        => _workspaceController.GetAvailableClosedSketches();

    public async Task LoftFromProfilesAsync(Guid profileAId, Guid profileBId, double distance, CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.LoftProfiles, Distance: distance, EntityIdA: profileAId, EntityIdB: profileBId),
            switchToProperties: true,
            cancellationToken);
    }

    public (Guid ProfileAId, string ProfileAName, Guid ProfileBId, string ProfileBName, double Distance)? GetLoftParams(Guid featureId)
        => _workspaceController.FindLoftParams(featureId);

    public async Task UpdateLoftAsync(Guid featureId, Guid profileAId, Guid profileBId, double distance)
    {
        var result = _workspaceController.EditLoftFeature(featureId, profileAId, profileBId, distance);
        if (!result.Success) { ShowAssistantPanel(); AddMessage("system", result.Message); }
    }

    public (double Distance, double TwistDegrees)? GetSweepParams(Guid featureId)
        => _workspaceController.FindSweepParams(featureId);

    public async Task UpdateSweepAsync(Guid featureId, double distance, double twistDegrees)
    {
        var result = _workspaceController.EditSweepFeature(featureId, distance, twistDegrees);
        if (!result.Success) { ShowAssistantPanel(); AddMessage("system", result.Message); }
    }

    public async Task UpdateRevolveAsync(Guid featureId, double angleDegrees, string axis)
    {
        var result = _workspaceController.EditRevolveFeature(featureId, angleDegrees, axis);
        if (!result.Success) { ShowAssistantPanel(); AddMessage("system", result.Message); }
    }

    public (int Count, double Spacing, string Axis)? GetLinearPatternParams(Guid featureId)
    {
        return _workspaceController.FindLinearPatternParams(featureId);
    }

    public async Task UpdateLinearPatternAsync(Guid featureId, int count, double spacing, string axis, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.EditLinearPatternFeature(featureId, count, spacing, axis);
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }
    }

    public async Task HoleSelectedBodyAsync(double diameter = 10d, double centerOffsetX = 0d, double centerOffsetY = 0d, string depthInfo = "ThroughAll", CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.HoleBody, Distance: diameter, U: centerOffsetX, V: centerOffsetY, Axis: depthInfo),
            switchToProperties: true,
            cancellationToken);
    }

    public (double Diameter, string DepthKind, double DepthValue, double CenterOffsetX, double CenterOffsetY)? GetHoleParams(Guid featureId)
    {
        return _workspaceController.FindHoleParams(featureId);
    }

    public async Task UpdateHoleAsync(Guid featureId, double diameter, string depthKind, double depthValue, double centerOffsetX, double centerOffsetY, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.EditHoleFeature(featureId, diameter, depthKind, depthValue, centerOffsetX, centerOffsetY);
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }
    }

    public async Task ShellSelectedBodyAsync(double thickness = 2d, CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.ShellBody, Distance: thickness),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task MirrorSelectedBodyAsync(string axis = "x", CancellationToken cancellationToken = default)
    {
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(CadViewportCommandKind.MirrorBody, Axis: axis),
            switchToProperties: true,
            cancellationToken);
    }

    public async Task BooleanApplyAsync(Guid bodyAId, Guid bodyBId, string operation, CancellationToken cancellationToken = default)
    {
        var kind = operation.ToLowerInvariant() switch
        {
            "subtract" => CadViewportCommandKind.BooleanSubtract,
            "intersect" => CadViewportCommandKind.BooleanIntersect,
            _ => CadViewportCommandKind.BooleanUnion
        };
        await ExecuteToolbarCommandAsync(
            new CadViewportCommand(kind, EntityIdA: bodyAId, EntityIdB: bodyBId),
            switchToProperties: true,
            cancellationToken);
    }

    public List<(Guid Id, string Name)> GetBodyList()
    {
        var visibleBodies = _workspaceController.CurrentState.Project.Scene.Bodies
            .Where(body => body.Visible)
            .ToDictionary(body => body.Id, body => body.Name);

        return _workspaceController.CurrentState.CompileResult.Bodies
            .Where(compiled => visibleBodies.ContainsKey(compiled.BodyId))
            .Select(compiled => (compiled.BodyId, visibleBodies[compiled.BodyId]))
            .ToList();
    }

    public List<(Guid Id, string Name)> GetExtrudeTargetBodyList()
    {
        var project = _workspaceController.CurrentState.Project;
        var solidBodyIds = _workspaceController.CurrentState.CompileResult.Bodies
            .Select(body => body.BodyId)
            .ToHashSet();

        return project.Scene.Bodies
            .Where(body => body.Visible && solidBodyIds.Contains(body.Id))
            .Select(body => (body.Id, body.Name))
            .ToList();
    }

    public Guid ResolvePreferredExtrudeTargetBodyId(IReadOnlyList<(Guid Id, string Name)> candidates)
    {
        if (candidates.Count == 0)
        {
            return Guid.Empty;
        }

        var selectedBodyId = CurrentSelectedBodyId;
        if (selectedBodyId != Guid.Empty && candidates.Any(candidate => candidate.Id == selectedBodyId))
        {
            return selectedBodyId;
        }

        return candidates[0].Id;
    }

    public Guid CurrentSelectedBodyId =>
        _workspaceController.CurrentState.Project.Selection.Kind == CadEntityKind.Body
            ? _workspaceController.CurrentState.Project.Selection.EntityId
            : Guid.Empty;

    private CadFacePlane? _selectedFacePlane;

    private IReadOnlyList<Guid> _multiSelectedBodyIds = [];
    public IReadOnlyList<Guid> MultiSelectedBodyIds => _multiSelectedBodyIds;

    public void HandleFaceClicked(ViewportFaceClickedEventArgs e)
    {
        _selectedFacePlane = new CadFacePlane
        {
            BodyId = e.BodyId,
            OriginX = e.Ox, OriginY = e.Oy, OriginZ = e.Oz,
            NormalX = e.Nx, NormalY = e.Ny, NormalZ = e.Nz,
            UAxisX = e.Ux, UAxisY = e.Uy, UAxisZ = e.Uz,
            VAxisX = e.Vx, VAxisY = e.Vy, VAxisZ = e.Vz
        };
        _workspaceController.SetSelectedFacePlane(_selectedFacePlane);
        RaisePropertyChanged(nameof(CanStartSketchFromCurrentSelection));
        RaisePropertyChanged(nameof(StatusBarText));
    }

    public void HandleBoxSelect(IReadOnlyList<Guid> ids, bool append)
    {
        if (append)
        {
            var merged = new HashSet<Guid>(_multiSelectedBodyIds);
            if (CurrentSelectedBodyId != Guid.Empty) merged.Add(CurrentSelectedBodyId);
            foreach (var id in ids) merged.Add(id);
            _multiSelectedBodyIds = [.. merged];
        }
        else
        {
            _multiSelectedBodyIds = ids;
        }
        RaisePropertyChanged(nameof(MultiSelectedBodyIds));
        RaisePropertyChanged(nameof(StatusBarText));
    }

    public void HandleSelectionCleared()
    {
        _multiSelectedBodyIds = [];
        _selectedFacePlane = null;
        _workspaceController.ClearSelection();
        RaisePropertyChanged(nameof(MultiSelectedBodyIds));
        RaisePropertyChanged(nameof(CurrentSelectedBodyId));
        RaisePropertyChanged(nameof(CanStartSketchFromCurrentSelection));
        RaisePropertyChanged(nameof(StatusBarText));
    }

    public void UndoAsync()
    {
        _workspaceController.Undo();
        RaisePropertyChanged(nameof(CanUndo));
        RaisePropertyChanged(nameof(CanRedo));
    }

    public void RedoAsync()
    {
        _workspaceController.Redo();
        RaisePropertyChanged(nameof(CanUndo));
        RaisePropertyChanged(nameof(CanRedo));
    }

    public async Task RenameBodyAsync(Guid bodyId, string newName)
    {
        var result = _workspaceController.RenameBody(bodyId, newName);
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }

        await Task.CompletedTask;
    }

    public async Task DuplicateSelectedBodyAsync()
    {
        var result = _workspaceController.DuplicateSelectedBody();
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }

        await Task.CompletedTask;
    }

    public async Task SetBodyColorAsync(Guid bodyId, string? color)
    {
        var result = _workspaceController.SetBodyColor(bodyId, color);
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }

        await Task.CompletedTask;
    }

    public async Task ToggleBodyVisibilityAsync(Guid bodyId)
    {
        var result = _workspaceController.ToggleBodyVisibility(bodyId);
        if (!result.Success)
        {
            AddMessage("system", result.Message);
        }

        await Task.CompletedTask;
    }

    public void NewDocument()
    {
        _workspaceController.NewDocument();
        CurrentProjectPath = string.Empty;
        ProjectTitleBase = "My3DApp Project / unsaved.my3dapp";
        RaisePropertyChanged(nameof(DocumentName));
        RaisePropertyChanged(nameof(WindowTitle));
        IsSelectingSketchPlane = false;
        HasExplicitSketchBaseSelection = false;
        ClearAssistantHistory();
        ShowNotification("New document created.", NotificationSeverity.Info);
        AppendToLog("New document created.");
    }

    public bool SaveProject(string path)
    {
        try
        {
            _workspaceController.SaveProject(path);
            CurrentProjectPath = path;
            ProjectTitleBase = BuildProjectTitle(path);
            _appSettings.AddRecentFile(path);
            RaisePropertyChanged(nameof(RecentFiles));
            AppendToLog($"Project saved: {Path.GetFileName(path)}");
            ShowNotification($"Saved: {Path.GetFileName(path)}", NotificationSeverity.Info);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            AppendToLog($"Save failed: {ex.Message}");
            ShowNotification($"Save failed: {ex.Message}", NotificationSeverity.Error);
            AddMessage("system", $"Save failed: {ex.Message}");
            ShowAssistantPanel();
            return false;
        }
    }

    public bool OpenProject(string path)
    {
        var result = _workspaceController.OpenProject(path);
        if (result.Success)
        {
            CurrentProjectPath = path;
            ProjectTitleBase = BuildProjectTitle(path);
            _appSettings.AddRecentFile(path);
            RaisePropertyChanged(nameof(RecentFiles));
            IsSelectingSketchPlane = false;
            HasExplicitSketchBaseSelection =
                _workspaceController.CurrentState.Project.Selection.Kind == CadEntityKind.ReferencePlane;
            AppendToLog($"Project opened: {Path.GetFileName(path)}");
            ShowPropertiesPanel();
            return true;
        }

        AppendToLog($"Open failed: {result.Message}");
        ShowNotification($"Open failed: {result.Message}", NotificationSeverity.Error);
        AddMessage("system", result.Message);
        ShowAssistantPanel();
        return false;
    }

    public IReadOnlyList<string> RecentFiles => _appSettings.RecentFiles;
    public bool HasRecentFiles => _appSettings.RecentFiles.Count > 0;

    public void ExportStl(string path)
    {
        try
        {
            _workspaceController.ExportStl(path);
        }
        catch (Exception ex)
        {
            AddMessage("system", $"STL export failed: {ex.Message}");
            ShowAssistantPanel();
        }
    }

    public void CreateDatumPlane(CadReferencePlaneKind sourceKind, double offset, string name)
    {
        var result = _workspaceController.CreateDatumPlane(sourceKind, offset, name);
        if (!result.Success)
        {
            AddMessage("system", result.Message);
            ShowAssistantPanel();
        }
    }

    public void ExportObj(string path)
    {
        try
        {
            _workspaceController.ExportObj(path);
        }
        catch (Exception ex)
        {
            AddMessage("system", $"OBJ export failed: {ex.Message}");
            ShowAssistantPanel();
        }
    }

    public void ExportFormat(string format, bool allBodies, Guid selectedBodyId, string path)
    {
        var isStl = string.Equals(format, "STL", StringComparison.OrdinalIgnoreCase);
        if (allBodies)
        {
            if (isStl) _workspaceController.ExportStl(path);
            else _workspaceController.ExportObj(path);
        }
        else
        {
            if (isStl) _workspaceController.ExportStlBody(selectedBodyId, path);
            else _workspaceController.ExportObjBody(selectedBodyId, path);
        }
    }

    private static string BuildProjectTitle(string path)
    {
        var fileName = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(fileName)
            ? "My3DApp Project / unsaved.my3dapp"
            : $"My3DApp Project / {fileName}";
    }

    public async Task SelectTreeNodeAsync(FeatureNodeViewModel? node, CancellationToken cancellationToken = default)
    {
        if (node is null || !node.IsSelectable)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.SelectEntity(node.EntityId);
        if (result.Success && node.EntityKind is not CadEntityKind.ReferencePlane and not CadEntityKind.None)
        {
            HasExplicitSketchBaseSelection = false;
            ShowPropertiesPanel();
        }
        else if (result.Success && node.EntityKind == CadEntityKind.ReferencePlane)
        {
            HasExplicitSketchBaseSelection = true;
            if (IsSelectingSketchPlane)
            {
                await StartSketchAsync(cancellationToken: cancellationToken);
                return;
            }

            ShowPropertiesPanel();
        }
    }

    public async Task CommitParameterEditAsync(ParameterItemViewModel? item, CancellationToken cancellationToken = default)
    {
        if (item is null || !item.IsEditable || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.ApplySelectionParameter(item.Key, item.Value);
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }
    }

    public IReadOnlyList<(string Name, string Key, string Value)> GetEditableParameterSnapshot()
    {
        return Parameters
            .Where(item => item.IsEditable && IsNumericParameterValue(item.Value))
            .Select(item => (item.Name, item.Key, item.Value))
            .ToList();
    }

    public Task ApplyParameterEditsAsync(
        IReadOnlyDictionary<string, string> valuesByKey,
        CancellationToken cancellationToken = default)
    {
        foreach (var (key, value) in valuesByKey)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            var result = _workspaceController.ApplySelectionParameter(key, value);
            if (!result.Success)
            {
                ShowAssistantPanel();
                AddMessage("system", result.Message);
                AppendToLog($"Parameter edit failed: {result.Message}");
                return Task.CompletedTask;
            }
        }

        AppendToLog($"Updated {valuesByKey.Count.ToString(CultureInfo.InvariantCulture)} parameter(s).");
        ShowPropertiesPanel();
        return Task.CompletedTask;
    }

    public async Task HandleViewportSelectionAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        if (entityId == Guid.Empty || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.SelectEntity(entityId);
        if (result.Success)
        {
            if (IsSelectingSketchPlane
                && _workspaceController.CurrentState.Project.Selection.Kind == CadEntityKind.ReferencePlane)
            {
                HasExplicitSketchBaseSelection = true;
                await StartSketchAsync(cancellationToken: cancellationToken);
                return;
            }

            HasExplicitSketchBaseSelection = _workspaceController.CurrentState.Project.Selection.Kind == CadEntityKind.ReferencePlane;
            ShowPropertiesPanel();
        }
    }

    public async Task HandleViewportDeleteAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.DeleteSelection();
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }
    }

    public async Task HandleSketchEntityTransformAsync(Guid sketchId, double du, double dv, CancellationToken cancellationToken = default)
    {
        if (sketchId == Guid.Empty || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.ApplySketchEntityTranslation(sketchId, du, dv);
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }
    }

    public async Task HandleSketchEntityRotationAsync(Guid sketchId, double pivotU, double pivotV, double angleDeg, CancellationToken cancellationToken = default)
    {
        if (sketchId == Guid.Empty || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.ApplySketchEntityRotation(sketchId, pivotU, pivotV, angleDeg);
        if (!result.Success)
        {
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }
    }

    public async Task HandleViewportBodyTransformAsync(Guid bodyId, double x, double y, double z, CancellationToken cancellationToken = default)
    {
        if (bodyId == Guid.Empty || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = _workspaceController.ApplyBodyTranslation(bodyId, x, y, z);
        if (result.Success)
        {
            ShowPropertiesPanel();
            return;
        }

        ShowAssistantPanel();
        AddMessage("system", result.Message);
    }

    public async Task ApplyPendingCommandAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingCommand is null)
        {
            return;
        }

        if (!ModeExecutesCommands(SelectedAssistantMode))
        {
            AddMessage("system", "Switch assistant mode to Auto, Do, or Think&Do to execute commands.");
            return;
        }

        var command = _pendingCommand;
        var result = await ExecuteWorkspaceCommandAsync(command, cancellationToken);
        if (result.Success)
        {
            AddMessage("system", $"Applied pending command: {result.Message}");
            ClearPendingCommand();
            AssistantStatus = BuildIdleStatusText();
            ShowPropertiesPanel();
            return;
        }

        AddMessage("system", $"Failed to apply pending command: {result.Message}");
        AssistantStatus = "Command failed";
        ShowAssistantPanel();
    }

    public async Task SendAssistantAsync(CancellationToken cancellationToken = default)
    {
        var userInput = AssistantInput.Trim();
        if (string.IsNullOrWhiteSpace(userInput) || IsAssistantBusy)
        {
            return;
        }
        AssistantInput = string.Empty;

        var localSequence = _commandParser.ParseSequence(userInput);
        if (localSequence.Count > 1 &&
            localSequence.All(step => step.Result.IsSuccess && step.Result.Commands.Count > 0))
        {
            AddMessage("user", userInput);
            foreach (var step in localSequence)
            {
                foreach (var command in step.Result.Commands)
                {
                    await HandleParsedCommandAsync(command, $"Command step {step.Index}", cancellationToken);
                }
            }

            return;
        }

        var localCommand = _commandParser.Parse(userInput);
        if (localCommand.IsSuccess && localCommand.Commands.Count > 0)
        {
            AddMessage("user", userInput);
            foreach (var command in localCommand.Commands)
            {
                await HandleParsedCommandAsync(command, "Parsed command", cancellationToken);
            }

            return;
        }

        if (!IsAssistantConfigured)
        {
            AssistantStatus = BuildIdleStatusText();
            ShowAssistantPanel();
            return;
        }

        AddMessage("user", userInput);
        IsAssistantBusy = true;
        AssistantStatus = $"Running ({SelectedAssistantMode})";

        try
        {
            var conversation = AssistantMessages
                .Where(message => message.Role is "user" or "assistant")
                .Select(message => new AssistantPromptMessage(message.Role, message.Content))
                .ToArray();

            var sel = _workspaceController.CurrentState.Project.Selection;
            var selectedFeatureNames = sel.IsEmpty
                ? Array.Empty<string>()
                : new[] { sel.Name ?? sel.Kind.ToString() };
            var context = _workspaceController.GetAssistantContext(
                appMode: IsSketchMode ? "Sketch" : "3D",
                activeTool: IsSketchMode ? SelectedSketchTool : null,
                selectedPlane: IsSketchMode ? AssistantPlaneSummary : null,
                assistantMode: SelectedAssistantMode,
                selectedFeatures: selectedFeatureNames);

            var result = await _assistantChatService.SendAsync(
                _assistantConfiguration,
                conversation,
                context,
                cancellationToken);

            if (result.IsSuccess)
            {
                AddMessage("assistant", result.Reply);
                if (!string.IsNullOrWhiteSpace(result.Command))
                {
                    var parsedSuggestionSequence = _commandParser.ParseSequence(result.Command);
                    var allOk = parsedSuggestionSequence.Count > 0 &&
                                parsedSuggestionSequence.All(step => step.Result.IsSuccess && step.Result.Commands.Count > 0);

                    if (allOk)
                    {
                        foreach (var step in parsedSuggestionSequence)
                        {
                            foreach (var command in step.Result.Commands)
                            {
                                await HandleParsedCommandAsync(command, $"Assistant step {step.Index}", cancellationToken);
                            }
                        }
                    }
                    else
                    {
                        var shortCmd = result.Command.Length > 80
                            ? result.Command[..77] + "…"
                            : result.Command;
                        AddMessage("system",
                            $"The assistant returned a command the parser couldn't execute: \"{shortCmd}\". " +
                            "Try rephrasing, or use the Recipe library for standard parts.");
                    }
                }

                AssistantStatus = BuildIdleStatusText();
                ShowAssistantPanel();
            }
            else
            {
                AddMessage("system", result.Reply);
                AssistantStatus = "Last request failed";
                ShowAssistantPanel();
            }
        }
        catch (Exception ex)
        {
            AddMessage("system", $"Assistant request failed: {ex.Message}");
            AssistantStatus = "Error";
            ShowAssistantPanel();
        }
        finally
        {
            IsAssistantBusy = false;
        }
    }

    private void OnWorkspaceChanged(object? sender, StudioWorkspaceState state)
    {
        ApplyWorkspaceState(state);
    }

    private void ApplyWorkspaceState(StudioWorkspaceState state)
    {
        CodeText = state.CodeText;
        LogText = state.LogText;

        RebuildFeatureTree(state.Project, state.CompileResult);
        RebuildProperties(state.Project, state.CompileResult);
        UpdateWorkspaceSummaries(state.Project, state.CompileResult);
        UpdateToolAvailability(state.Project, state.CompileResult);
        ApplyWorkspaceMode(state.CompileResult.Mode);
        IsConstructionModeActive = state.Project.ActiveSketchSession?.IsConstructionModeActive ?? false;

        RaisePropertyChanged(nameof(StatusBarText));
        RaisePropertyChanged(nameof(HasUnsavedChanges));
        RaisePropertyChanged(nameof(ProjectTitle));
        RaisePropertyChanged(nameof(WindowTitle));
        RaisePropertyChanged(nameof(CanUndo));
        RaisePropertyChanged(nameof(CanRedo));

        ViewportStateChanged?.Invoke(this, state.ViewportState);
    }

    private async Task HandleParsedCommandAsync(
        CadViewportCommand command,
        string source,
        CancellationToken cancellationToken)
    {
        if (!ModeExecutesCommands(SelectedAssistantMode))
        {
            SetPendingCommand(command, $"{source} queued");
            AddMessage("system", $"{source} available: {command.Describe()}. Use Apply or switch to Auto/Do/Think&Do.");
            AssistantStatus = BuildIdleStatusText();
            ShowAssistantPanel();
            return;
        }

        var result = await ExecuteWorkspaceCommandAsync(command, cancellationToken);
        if (result.Success)
        {
            AddMessage("system", $"{source} executed: {result.Message}");
            ClearPendingCommand();
            AssistantStatus = BuildIdleStatusText();
            ShowPropertiesPanel();
            return;
        }

        SetPendingCommand(command, $"{source} failed");
        AddMessage("system", $"{source} could not be executed. {result.Message}");
        AssistantStatus = "Command failed";
        ShowAssistantPanel();
    }

    private async Task ExecuteToolbarCommandAsync(
        CadViewportCommand command,
        bool switchToProperties,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteWorkspaceCommandAsync(command, cancellationToken);
        if (result.Success && switchToProperties)
        {
            ShowPropertiesPanel();
        }
        else if (!result.Success)
        {
            ShowNotification(result.Message, NotificationSeverity.Warning);
            ShowAssistantPanel();
            AddMessage("system", result.Message);
        }
    }

    public async Task<string> ExecuteLocalCommandTextAsync(string input, CancellationToken cancellationToken = default)
    {
        var commandText = input.Trim();
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return "Type a CAD command first.";
        }

        var steps = _commandParser.ParseSequence(commandText);
        var report = new StringBuilder();
        AppendToLog($"Command palette sequence started: {commandText}");

        foreach (var step in steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                AppendToLog("Command palette sequence canceled.");
                return "Command sequence canceled.";
            }

            if (!step.Result.IsSuccess || step.Result.Commands.Count == 0)
            {
                var parseFailure = $"Step {step.Index}: cannot parse '{step.Text}' ({step.Result.Message})";
                AppendToLog($"Command palette rejected: {parseFailure}");
                report.AppendLine(parseFailure);
                return report.ToString().TrimEnd();
            }

            var subStep = 1;
            foreach (var command in step.Result.Commands)
            {
                var result = await ExecuteWorkspaceCommandAsync(command, cancellationToken);
                if (!result.Success)
                {
                    var failure = $"Step {step.Index}: failed - {result.Message}";
                    AppendToLog($"Command palette failed: {command.Describe()} -> {result.Message}");
                    report.AppendLine(failure);
                    return report.ToString().TrimEnd();
                }

                AppendToLog($"Command palette step {step.Index}.{subStep}: {command.Describe()} -> {result.Message}");
                subStep++;
            }

            report.AppendLine($"Step {step.Index}: done - {step.Text}");
        }

        ClearPendingCommand();
        AssistantStatus = BuildIdleStatusText();
        ShowPropertiesPanel();
        AppendToLog($"Command palette sequence completed: {steps.Count.ToString(CultureInfo.InvariantCulture)} step(s).");
        return report.ToString().TrimEnd();
    }

    private Task<StudioWorkspaceActionResult> ExecuteWorkspaceCommandAsync(CadViewportCommand command, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new StudioWorkspaceActionResult(false, false, "Command canceled.", false));
        }

        var result = _workspaceController.ExecuteCommand(command);
        if (result.RequestFocusSelection)
        {
            FocusSelectionRequested?.Invoke(this, EventArgs.Empty);
        }

        return Task.FromResult(result);
    }

    private void SeedAssistantHistory()
    {
        if (IsAssistantConfigured)
        {
            AddMessage(
                "system",
                "CAD assistant ready. Try commands like: create box, start sketch on top plane; rectangle 0 0 20 10; finish sketch; extrude 8.");
        }
    }

    private void AddMessage(string role, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        AssistantMessages.Add(new AssistantMessageViewModel(role, content.Trim(), DateTimeOffset.Now));
        RaisePropertyChanged(nameof(HasAssistantMessages));
        RaisePropertyChanged(nameof(ShowAssistantInactiveState));
    }

    private void SetPendingCommand(CadViewportCommand command, string caption)
    {
        _pendingCommand = command;
        PendingCommandSummary = $"{caption}: {command.Describe()}";
        RaisePropertyChanged(nameof(HasPendingCommand));
        RaisePropertyChanged(nameof(CanApplyPendingCommand));
    }

    private void ClearPendingCommand()
    {
        _pendingCommand = null;
        PendingCommandSummary = "No pending command.";
        RaisePropertyChanged(nameof(HasPendingCommand));
        RaisePropertyChanged(nameof(CanApplyPendingCommand));
    }

    private string BuildIdleStatusText()
    {
        if (!IsAssistantConfigured)
        {
            return "Local CAD mode";
        }

        return $"{SelectedAssistantProvider} ready ({SelectedAssistantMode})";
    }

    private void ShowPropertiesPanel() => SelectedInspectorTabIndex = 0;

    private void ShowAssistantPanel() => SelectedInspectorTabIndex = 1;

    private static bool ModeExecutesCommands(string mode) =>
        mode is "Auto" or "Do" or "Think&Do";

    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "missing";
        }

        if (apiKey.Length <= 8)
        {
            return "configured";
        }

        return $"{apiKey[..4]}...{apiKey[^4..]}";
    }

    private void RefreshRecentPrimitiveTools()
    {
        RecentPrimitiveTools.Clear();

        foreach (var kind in _recentPrimitiveKinds)
        {
            var item = PrimitiveCatalog.FirstOrDefault(candidate =>
                string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase));
            if (item is not null)
            {
                RecentPrimitiveTools.Add(item);
            }
        }
    }

    private void RebuildFeatureTree(CadProject project, CadCompileResult compileResult)
    {
        FeatureNodes.Clear();

        var geometryRoot = new FeatureNodeViewModel(
            "Default geometry",
            "reference-group",
            string.Empty,
            string.Empty,
            isExpanded: true);
        geometryRoot.Children.Add(new FeatureNodeViewModel("Origin", "origin", string.Empty, string.Empty));

        foreach (var plane in project.Scene.ReferencePlanes)
        {
            var nodeType = plane.Kind == CadReferencePlaneKind.Datum ? "datum-plane" : "plane";
            geometryRoot.Children.Add(new FeatureNodeViewModel(
                plane.Name,
                nodeType,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                CadEntityKind.ReferencePlane,
                plane.Id,
                isSelectable: true));
        }

        var editingSketchId = project.ActiveSketchSession?.EditingSketchId ?? Guid.Empty;
        var sketchRoot = new FeatureNodeViewModel(
            "Sketches",
            "sketch-group",
            string.Empty,
            string.Empty,
            isExpanded: true);
        var featureRoot = new FeatureNodeViewModel(
            "Features",
            "feature-group",
            string.Empty,
            string.Empty,
            isExpanded: true);
        var roots = new List<FeatureNodeViewModel> { geometryRoot };

        foreach (var body in project.Scene.Bodies)
        {
            foreach (var feature in body.Features)
            {
                var node = BuildFeatureNode(project, body, feature, editingSketchId, body.Visible);
                if (feature.Kind == CadFeatureKind.Sketch)
                {
                    sketchRoot.Children.Add(node);
                }
                else
                {
                    featureRoot.Children.Add(node);
                }
            }
        }

        if (sketchRoot.Children.Count > 0)
        {
            roots.Add(sketchRoot);
        }

        if (featureRoot.Children.Count > 0)
        {
            roots.Add(featureRoot);
        }

        var allCompiled = compileResult.Bodies.ToList();
        var partsLabel = allCompiled.Count > 0
            ? $"Parts ({allCompiled.Count.ToString(CultureInfo.InvariantCulture)})"
            : "Parts";

        if (allCompiled.Count > 0)
        {
            var partsRoot = new FeatureNodeViewModel(
                partsLabel,
                "part-group",
                string.Empty,
                string.Empty,
                isExpanded: true);

            foreach (var compiledBody in allCompiled)
            {
                var bodyVisible = project.Scene.Bodies.FirstOrDefault(b => b.Id == compiledBody.BodyId)?.Visible ?? true;
                partsRoot.Children.Add(BuildPartNode(project, compiledBody, bodyVisible));
            }

            roots.Add(partsRoot);
        }

        foreach (var root in ApplyTreeFilter(roots, FeatureTreeFilterText))
        {
            FeatureNodes.Add(root);
        }
    }

    private void RebuildProperties(CadProject project, CadCompileResult compileResult)
    {
        Parameters.Clear();

        var selection = project.Selection;
        if (selection.IsEmpty)
        {
            AddParameter("Project", "Project", project.Name, false);
            AddParameter("Units", "Units", project.Units, false);
            AddParameter("Mode", "Mode", compileResult.Mode.ToString(), false);
            return;
        }

        if (selection.Kind == CadEntityKind.ReferencePlane)
        {
            var plane = project.Scene.ReferencePlanes.FirstOrDefault(item => item.Id == selection.EntityId);
            if (plane is not null)
            {
                AddParameter("Name", "Name", plane.Name, false);
                AddParameter("Type", "Type", "Reference Plane", false);
                AddParameter("Plane", "Plane", plane.Kind.ToString(), false);
                AddParameter("Visible", "Visible", plane.Visible ? "Yes" : "No", false);
                return;
            }
        }

        if (selection.Kind == CadEntityKind.Body)
        {
            var body = project.Scene.Bodies.FirstOrDefault(item => item.Id == selection.EntityId);
            var compiledBody = compileResult.Bodies.FirstOrDefault(item => item.BodyId == selection.EntityId);
            if (body is not null)
            {
                AddParameter("Name", "Name", body.Name, false);
                AddParameter("Type", "Type", "Part", false);
                AddParameter("Features", "Features", body.Features.Count.ToString(CultureInfo.InvariantCulture), false);

                foreach (var parameter in body.BaseFeature?.GetParameters() ?? [])
                {
                    AddParameter(parameter.Name, parameter.Name, parameter.DisplayValue, parameter.Editable);
                }

                var translation = GetBodyTranslation(body);
                AddParameter("X", "X", FormatNumber(translation.X), true);
                AddParameter("Y", "Y", FormatNumber(translation.Y), true);
                AddParameter("Z", "Z", FormatNumber(translation.Z), true);

                if (compiledBody is not null)
                {
                    AddParameter("Kind", "Kind", FormatFeatureName(compiledBody.SourceKind.ToString()), false);
                }

                return;
            }
        }

        if (selection.Kind == CadEntityKind.SketchEntity)
        {
            var selectedSketchEntity = FindSelectedSketchEntity(project, selection.EntityId);
            if (selectedSketchEntity.Entity is not null)
            {
                AddParameter("Name", "Name", selectedSketchEntity.Entity.EntityType, false);
                AddParameter("Type", "Type", "Sketch Entity", false);

                if (selectedSketchEntity.ParentSketch is not null)
                {
                    AddParameter("Sketch", "Sketch", selectedSketchEntity.ParentSketch.Name, false);
                    AddParameter("Plane", "Plane", selectedSketchEntity.ParentSketch.PlaneName, false);
                }

                foreach (var parameter in selectedSketchEntity.Entity.GetParameters())
                {
                    AddParameter(parameter.Name, parameter.Name, parameter.DisplayValue, true);
                }

                return;
            }
        }

        var selectedFeature = FindSelectedFeature(project, selection.EntityId);
        if (selectedFeature is not null)
        {
            AddParameter("Name", "Name", selectedFeature.Name, false);
            AddParameter("Type", "Type", FormatFeatureName(selectedFeature.Kind.ToString()), false);

            foreach (var parameter in selectedFeature.GetParameters())
            {
                AddParameter(parameter.Name, parameter.Name, parameter.DisplayValue, parameter.Editable);
            }

            return;
        }

        AddParameter("Selection", "Selection", selection.Name, false);
        AddParameter("Type", "Type", selection.Kind.ToString(), false);
    }

    private void AddParameter(string name, string key, string value, bool editable)
    {
        Parameters.Add(new ParameterItemViewModel(name, key, value, editable));
    }

    private static bool IsNumericParameterValue(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out _);
    }

    private void UpdateWorkspaceSummaries(CadProject project, CadCompileResult compileResult)
    {
        var featureCount = project.Scene.Bodies.Sum(body => body.Features.Count);
        var partCount = compileResult.Bodies.Count;
        FeatureTreeSummary =
            $"{featureCount.ToString(CultureInfo.InvariantCulture)} history items | {partCount.ToString(CultureInfo.InvariantCulture)} parts";
        FeatureListHeader = $"Features ({featureCount.ToString(CultureInfo.InvariantCulture)})";

        AssistantWorkspaceSummary = compileResult.Mode == CadMode.Sketch
            ? "Sketch workspace"
            : "3D part studio";
        AssistantPlaneSummary = $"{compileResult.ActivePlaneName} plane";
        AssistantSelectionSummary = DescribeSelection(project.Selection);
        SelectedProfileSummary = BuildSelectedProfileSummary(project);
        SelectedProfilePlaneSummary = BuildSelectedProfilePlaneSummary(project, compileResult);
        UpdateSketchHints(project);
        RebuildSketchPanels(project);
        RaisePropertyChanged(nameof(SketchPrimarySummary));
        RaisePropertyChanged(nameof(ActiveSketchTaskTitle));
        RaisePropertyChanged(nameof(ActiveSketchTaskSummary));
        RaisePropertyChanged(nameof(ActiveSketchTaskDetails));
        RaisePropertyChanged(nameof(SketchWorkflowHint));
        RaisePropertyChanged(nameof(WorkspaceModeSummary));
        RaisePropertyChanged(nameof(ViewportSketchEntryLabel));
        RaisePropertyChanged(nameof(ViewportSketchSessionTitle));
        RaisePropertyChanged(nameof(ViewportSketchSessionSummary));
    }

    private void UpdateToolAvailability(CadProject project, CadCompileResult compileResult)
    {
        CanPrimitiveTools = project.ActiveSketchSession is null;
        CanStartSketch = project.ActiveSketchSession is null && project.Selection.Kind == CadEntityKind.ReferencePlane;
        CanFinishSketch = project.ActiveSketchSession is not null;
        CanSketchTools = project.ActiveSketchSession is not null;
        CanProfileTools = ResolveSelectedSketch(project) is { } sketch && CanExtrudeSketch(sketch);
        CanLoftTools = project.ActiveSketchSession is null
            && _workspaceController.GetAvailableClosedSketches().Count >= 2;
        CanBooleanTools = project.ActiveSketchSession is null
            && project.Scene.Bodies.Count(b => b.Visible) >= 2;
        CanEdgeTools = project.ActiveSketchSession is null
            && project.Selection.Kind == CadEntityKind.Body
            && compileResult.Bodies.Any(b => b.BodyId == project.Selection.EntityId);
        if (project.ActiveSketchSession is not null)
        {
            SelectedSketchTool = project.ActiveSketchSession.ActiveTool.ToString();
            SketchDofValue = CadProjectStore.ComputeSketchDof(project.ActiveSketchSession);
        }
        else
        {
            SketchDofValue = 0;
        }

        RaisePropertyChanged(nameof(CanSketchPrimaryAction));
        RaisePropertyChanged(nameof(CanStartSketchFromCurrentSelection));
        RaisePropertyChanged(nameof(CanCancelSketch));
        RaisePropertyChanged(nameof(SketchPrimaryGlyph));
        RaisePropertyChanged(nameof(SketchPrimaryLabel));
        RaisePropertyChanged(nameof(SketchPrimaryToolTip));
        RaisePropertyChanged(nameof(SketchPrimarySummary));
        RaisePropertyChanged(nameof(ActiveSketchTaskTitle));
        RaisePropertyChanged(nameof(ActiveSketchTaskSummary));
        RaisePropertyChanged(nameof(ActiveSketchTaskDetails));
        RaisePropertyChanged(nameof(SketchWorkflowHint));
        RaisePropertyChanged(nameof(ViewportSketchEntryLabel));
        RaisePropertyChanged(nameof(ViewportSketchSessionTitle));
        RaisePropertyChanged(nameof(ViewportSketchSessionSummary));
        RaisePropertyChanged(nameof(SketchDofLabel));
        RaisePropertyChanged(nameof(SketchDofStatusClass));
        RaisePropertyChanged(nameof(SketchDofBrush));
    }

    private void ApplyWorkspaceMode(CadMode mode)
    {
        if (mode == CadMode.Sketch)
        {
            IsSketchMode = true;
            RaisePropertyChanged(nameof(WorkspaceModeBadge));
            RaisePropertyChanged(nameof(WorkspaceModeSummary));
            return;
        }

        IsThreeDMode = true;
        RaisePropertyChanged(nameof(WorkspaceModeBadge));
        RaisePropertyChanged(nameof(WorkspaceModeSummary));
    }

    private static string BuildPlaneSummary(CadProject project, CadReferencePlane plane)
    {
        var isActive = project.Selection.Kind == CadEntityKind.ReferencePlane && project.Selection.EntityId == plane.Id;
        return isActive ? "Selected reference plane" : "Primary sketch plane";
    }

    private static FeatureNodeViewModel BuildFeatureNode(CadProject project, CadBody body, CadFeature feature, Guid editingSketchId = default, bool isBodyVisible = true)
    {
        var iconKind = feature.Kind switch
        {
            CadFeatureKind.Sketch          => "sketch",
            CadFeatureKind.Extrude         => "extrude",
            CadFeatureKind.Revolve         => "revolve",
            CadFeatureKind.Sweep           => "sweep",
            CadFeatureKind.Loft            => "loft",
            CadFeatureKind.Fillet          => "fillet",
            CadFeatureKind.Chamfer         => "chamfer",
            CadFeatureKind.Shell           => "shell",
            CadFeatureKind.Mirror          => "mirror",
            CadFeatureKind.LinearPattern   => "linear-pattern",
            CadFeatureKind.CircularPattern => "circular-pattern",
            CadFeatureKind.Hole            => "hole",
            CadFeatureKind.Move            => "move",
            CadFeatureKind.Box             => "box",
            CadFeatureKind.Cylinder        => "cylinder",
            CadFeatureKind.Sphere          => "sphere",
            CadFeatureKind.Cone            => "cone",
            CadFeatureKind.Torus           => "torus",
            CadFeatureKind.Prism           => "prism",
            CadFeatureKind.Pyramid         => "pyramid",
            CadFeatureKind.Hemisphere      => "hemisphere",
            CadFeatureKind.Capsule         => "capsule",
            CadFeatureKind.Icosphere       => "icosphere",
            CadFeatureKind.Wedge           => "wedge",
            CadFeatureKind.Arrow           => "arrow-primitive",
            CadFeatureKind.Tetrahedron     => "tetrahedron",
            CadFeatureKind.Octahedron      => "octahedron",
            CadFeatureKind.Icosahedron     => "icosahedron",
            CadFeatureKind.Ellipsoid       => "ellipsoid",
            CadFeatureKind.BooleanBody     => "boolean",
            _ => "feature"
        };
        var typeLabel = feature.Role switch
        {
            CadFeatureRole.Create => "Create",
            CadFeatureRole.Operation => "Transform",
            CadFeatureRole.Sketch => "Sketch",
            CadFeatureRole.Derived => "Derived",
            CadFeatureRole.Placeholder => "Future",
            _ => "Feature"
        };

        var selectionKind = feature.Kind == CadFeatureKind.Sketch ? CadEntityKind.Sketch : CadEntityKind.Feature;
        var isEditing = feature.Kind == CadFeatureKind.Sketch && feature.Id == editingSketchId;
        var tooltipText = string.Empty;
        var secondaryText = isEditing ? "editing…" : $"{body.Name} | {BuildFeatureSummary(feature)}";
        var sketchStatusClass = string.Empty;
        if (feature is SketchFeature sketchFeature)
        {
            tooltipText = BuildSketchDefinitionTooltip(project, sketchFeature, isEditing);
            var definitionLabel = BuildSketchDefinitionStateLabel(project, sketchFeature, isEditing);
            sketchStatusClass = BuildSketchDefinitionClass(project, sketchFeature, isEditing);
            var entityCount = sketchFeature.Entities.Count;
            var entityLabel = $"{entityCount.ToString(CultureInfo.InvariantCulture)} entit{(entityCount == 1 ? "y" : "ies")}";
            secondaryText = isEditing
                ? $"editing… | {definitionLabel} | {entityLabel}"
                : $"{body.Name} | {definitionLabel} | {entityLabel}";
        }

        var node = new FeatureNodeViewModel(
            feature.Name,
            iconKind,
            typeLabel,
            secondaryText,
            tooltipText,
            sketchStatusClass,
            selectionKind,
            feature.Id,
            isSelectable: true,
            isBodyVisible: isBodyVisible)
        {
            IsEditingSketch = isEditing
        };

        if (feature is SketchFeature sketch)
        {
            for (var index = 0; index < sketch.Entities.Count; index++)
            {
                var entity = sketch.Entities[index];
                node.Children.Add(new FeatureNodeViewModel(
                    entity.IsConstruction
                        ? $"{entity.EntityType} {index + 1} (construction)"
                        : $"{entity.EntityType} {index + 1}",
                    "sketch-entity",
                    entity.IsConstruction ? "Reference" : "Entity",
                    BuildSketchEntitySummary(entity),
                    BuildSketchEntitySummary(entity),
                    string.Empty,
                    CadEntityKind.SketchEntity,
                    entity.Id,
                    isSelectable: true));
            }

            foreach (var dimension in sketch.Dimensions)
            {
                node.Children.Add(new FeatureNodeViewModel(
                    dimension.Label,
                    "dimension",
                    "Dimension",
                    BuildSketchDimensionSummary(dimension),
                    BuildSketchDimensionSummary(dimension)));
            }

            foreach (var constraint in sketch.Constraints)
            {
                node.Children.Add(new FeatureNodeViewModel(
                    constraint.Kind.ToString(),
                    "constraint",
                    "Constraint",
                    constraint.Description,
                    constraint.Description));
            }
        }

        return node;
    }

    private static FeatureNodeViewModel BuildPartNode(CadProject project, CadCompiledBody compiledBody, bool isBodyVisible = true)
    {
        var body = project.Scene.Bodies.FirstOrDefault(item => item.Id == compiledBody.BodyId);
        var translation = body is null ? new Vector3D(0, 0, 0) : GetBodyTranslation(body);

        return new FeatureNodeViewModel(
            compiledBody.Name,
            "part",
            "Part",
            $"{FormatFeatureName(compiledBody.SourceKind.ToString())} | X {FormatNumber(translation.X)} | Y {FormatNumber(translation.Y)} | Z {FormatNumber(translation.Z)}",
            $"{compiledBody.Name} part body",
            string.Empty,
            CadEntityKind.Body,
            compiledBody.BodyId,
            isSelectable: true,
            isBodyVisible: isBodyVisible);
    }

    private static string BuildFeatureSummary(CadFeature feature)
    {
        return feature switch
        {
            BoxFeature box => $"Box | W {FormatNumber(box.Width)} | D {FormatNumber(box.Depth)} | H {FormatNumber(box.Height)}",
            SphereFeature sphere => $"Sphere | R {FormatNumber(sphere.Radius)}",
            CylinderFeature cylinder => $"Cylinder | R {FormatNumber(cylinder.Radius)} | H {FormatNumber(cylinder.Height)}",
            ConeFeature cone => $"Cone | R {FormatNumber(cone.Radius)} | H {FormatNumber(cone.Height)}",
            TorusFeature torus => $"Torus | MR {FormatNumber(torus.MajorRadius)} | r {FormatNumber(torus.MinorRadius)}",
            PyramidFeature pyramid => $"Pyramid | W {FormatNumber(pyramid.BaseWidth)} | D {FormatNumber(pyramid.BaseDepth)} | H {FormatNumber(pyramid.Height)}",
            WedgeFeature wedge => $"Wedge | W {FormatNumber(wedge.Width)} | D {FormatNumber(wedge.Depth)} | H {FormatNumber(wedge.Height)}",
            EllipsoidFeature ellipsoid => $"Ellipsoid | RX {FormatNumber(ellipsoid.RadiusX)} | RY {FormatNumber(ellipsoid.RadiusY)} | RZ {FormatNumber(ellipsoid.RadiusZ)}",
            CapsuleFeature capsule => $"Capsule | R {FormatNumber(capsule.Radius)} | H {FormatNumber(capsule.Height)}",
            HemisphereFeature hemisphere => $"Hemisphere | R {FormatNumber(hemisphere.Radius)}",
            PrismFeature prism => $"Prism | R {FormatNumber(prism.Radius)} | H {FormatNumber(prism.Height)} | S {prism.Sides}",
            ArrowFeature arrow => $"Arrow | Shaft {FormatNumber(arrow.ShaftRadius)} x {FormatNumber(arrow.ShaftHeight)} | Head {FormatNumber(arrow.HeadRadius)} x {FormatNumber(arrow.HeadHeight)}",
            IcosphereFeature icosphere => $"Icosphere | R {FormatNumber(icosphere.Radius)} | Sub {icosphere.Subdivisions}",
            TetrahedronFeature tetrahedron => $"Tetrahedron | R {FormatNumber(tetrahedron.Radius)}",
            OctahedronFeature octahedron => $"Octahedron | R {FormatNumber(octahedron.Radius)}",
            IcosahedronFeature icosahedron => $"Icosahedron | R {FormatNumber(icosahedron.Radius)}",
            MoveFeature move => $"Move | X {FormatNumber(move.X)} | Y {FormatNumber(move.Y)} | Z {FormatNumber(move.Z)}",
            SketchFeature sketch =>
                $"{sketch.PlaneName} | {DescribeSketchProfile(sketch)} | " +
                $"{sketch.Constraints.Count.ToString(CultureInfo.InvariantCulture)} constraints | " +
                $"{sketch.Dimensions.Count.ToString(CultureInfo.InvariantCulture)} dimensions | " +
                $"{(sketch.IsClosedProfile ? "closed" : "open")}",
            ExtrudeFeature extrude =>
                BuildExtrudeLabel(extrude),
            RevolveFeature revolve => $"Revolve | {FormatNumber(revolve.AngleDegrees)}°",
            SweepFeature sweep => $"Sweep | {FormatNumber(sweep.Distance)}" + (sweep.TwistDegrees != 0d ? $" | twist {FormatNumber(sweep.TwistDegrees)}°" : ""),
            LoftFeature loft => $"Loft | {loft.ProfileASketchName} → {loft.ProfileBSketchName} | d={FormatNumber(loft.Distance)}",
            FilletFeature fillet => $"Fillet | R {FormatNumber(fillet.Radius)}",
            ShellFeature shell => $"Shell | T {FormatNumber(shell.Thickness)}",
            MirrorFeature mirror => $"Mirror | {mirror.Axis} axis",
            LinearPatternFeature lp => $"Linear Pattern | {lp.Count}x | spacing {FormatNumber(lp.Spacing)} | {lp.Axis}",
            CircularPatternFeature cp => $"Circular Pattern | {cp.Count}x | {FormatNumber(cp.TotalAngle)}° | {cp.Axis}",
            HoleFeature hole => $"Hole | ⌀{FormatNumber(hole.Diameter)} × {hole.DepthLabel}",
            BooleanFeature boolean => $"{boolean.Operation} | {boolean.BodyAName} + {boolean.BodyBName}",
            PlaceholderFeature placeholder => $"Planned | {placeholder.PlaceholderKind}",
            _ => "CAD feature"
        };
    }

    private static string BuildSketchDefinitionTooltip(CadProject project, SketchFeature sketch, bool isEditing)
    {
        var session = isEditing && project.ActiveSketchSession is not null
            ? project.ActiveSketchSession
            : new CadSketchSession
            {
                PlaneId = sketch.Id,
                PlaneName = sketch.PlaneName,
                DraftEntities = sketch.Entities.ToList(),
                ManualConstraints = sketch.Constraints.ToList(),
                ManualDimensions = sketch.Dimensions.ToList()
            };

        var dof = CadProjectStore.ComputeSketchDof(session);
        return dof switch
        {
            0 => "Sketch fully defined.",
            < 0 => "Sketch overdefined.",
            _ => "Sketch not fully defined."
        };
    }

    private static string BuildSketchDefinitionStateLabel(CadProject project, SketchFeature sketch, bool isEditing)
    {
        var session = isEditing && project.ActiveSketchSession is not null
            ? project.ActiveSketchSession
            : new CadSketchSession
            {
                PlaneId = sketch.Id,
                PlaneName = sketch.PlaneName,
                DraftEntities = sketch.Entities.ToList(),
                ManualConstraints = sketch.Constraints.ToList(),
                ManualDimensions = sketch.Dimensions.ToList()
            };

        var dof = CadProjectStore.ComputeSketchDof(session);
        return dof switch
        {
            0 => "Fully defined",
            < 0 => "Overdefined",
            _ => "Underdefined"
        };
    }

    private static string BuildSketchDefinitionClass(CadProject project, SketchFeature sketch, bool isEditing)
    {
        var session = isEditing && project.ActiveSketchSession is not null
            ? project.ActiveSketchSession
            : new CadSketchSession
            {
                PlaneId = sketch.Id,
                PlaneName = sketch.PlaneName,
                DraftEntities = sketch.Entities.ToList(),
                ManualConstraints = sketch.Constraints.ToList(),
                ManualDimensions = sketch.Dimensions.ToList()
            };

        var dof = CadProjectStore.ComputeSketchDof(session);
        return dof switch
        {
            0 => "SketchFullyDefined",
            < 0 => "SketchOverdefined",
            _ => "SketchUnderdefined"
        };
    }

    private static string BuildExtrudeLabel(ExtrudeFeature extrude)
    {
        var modeLabel = extrude.Operation switch
        {
            CadExtrudeOperation.Join when !string.IsNullOrEmpty(extrude.TargetBodyName) =>
                $" (Join ← {extrude.TargetBodyName})",
            CadExtrudeOperation.Join => " (Join)",
            CadExtrudeOperation.Cut when !string.IsNullOrEmpty(extrude.TargetBodyName) =>
                $" (Cut ← {extrude.TargetBodyName})",
            CadExtrudeOperation.Cut => " (Cut)",
            _ when extrude.Symmetric => " (Symmetric)",
            _ => string.Empty
        };
        var taper = extrude.TaperAngleDegrees > 0.001 ? $" | Taper {FormatNumber(extrude.TaperAngleDegrees)}°" : string.Empty;
        return $"Extrude{modeLabel} | Depth {FormatNumber(extrude.Depth)}{taper}";
    }

    private static string DescribeSelection(CadSelection selection)
    {
        if (selection.IsEmpty)
        {
            return "No active selection";
        }

        var kind = selection.Kind switch
        {
            CadEntityKind.ReferencePlane => "Plane",
            CadEntityKind.Body => "Part",
            CadEntityKind.Feature => "Feature",
            CadEntityKind.Sketch => "Sketch",
            _ => "Selection"
        };

        return $"{kind}: {selection.Name}";
    }

    private static string BuildSelectedProfileSummary(CadProject project)
    {
        var sketch = ResolveSelectedSketch(project);
        if (sketch is null)
        {
            return "Select a closed sketch profile";
        }

        return sketch.IsClosedProfile
            ? $"{DescribeSketchProfile(sketch)} profile"
            : $"{DescribeSketchProfile(sketch)} (open)";
    }

    private static string BuildSelectedProfilePlaneSummary(CadProject project, CadCompileResult compileResult)
    {
        var sketch = ResolveSelectedSketch(project);
        if (sketch is null)
        {
            return $"{compileResult.ActivePlaneName} plane";
        }

        return $"{sketch.PlaneName} plane";
    }

    private static IEnumerable<FeatureNodeViewModel> ApplyTreeFilter(IEnumerable<FeatureNodeViewModel> roots, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            return roots;
        }

        var filter = filterText.Trim();
        var filtered = new List<FeatureNodeViewModel>();
        foreach (var root in roots)
        {
            var match = FilterNode(root, filter);
            if (match is not null)
            {
                filtered.Add(match);
            }
        }

        return filtered;
    }

    private static FeatureNodeViewModel? FilterNode(FeatureNodeViewModel node, string filter)
    {
        var childMatches = node.Children
            .Select(child => FilterNode(child, filter))
            .Where(child => child is not null)
            .Cast<FeatureNodeViewModel>()
            .ToList();

        var selfMatches =
            node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            node.TypeLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            node.SecondaryText.Contains(filter, StringComparison.OrdinalIgnoreCase);

        if (!selfMatches && childMatches.Count == 0)
        {
            return null;
        }

        var clone = new FeatureNodeViewModel(
            node.Name,
            node.IconKind,
            node.TypeLabel,
            node.SecondaryText,
            node.TooltipText,
            node.SketchStatusClass,
            node.EntityKind,
            node.EntityId,
            node.IsSelectable,
            node.IsExpanded,
            node.IsBodyVisible)
        {
            IsEditingSketch = node.IsEditingSketch
        };

        foreach (var child in childMatches)
        {
            clone.Children.Add(child);
        }

        return clone;
    }

    private static CadFeature? FindSelectedFeature(CadProject project, Guid featureId)
    {
        foreach (var body in project.Scene.Bodies)
        {
            var feature = body.Features.FirstOrDefault(item => item.Id == featureId);
            if (feature is not null)
            {
                return feature;
            }
        }

        return null;
    }

    private static SketchFeature? ResolveSelectedSketch(CadProject project)
    {
        if (project.Selection.Kind == CadEntityKind.Sketch)
        {
            return FindSelectedFeature(project, project.Selection.EntityId) as SketchFeature;
        }

        if (project.Selection.Kind != CadEntityKind.SketchEntity)
        {
            return null;
        }

        return FindSelectedSketchEntity(project, project.Selection.EntityId).ParentSketch;
    }

    private static bool CanExtrudeSketch(SketchFeature sketch) =>
        sketch.IsClosedProfile && ProfileBuilder.TryBuild(sketch, out _, out _);

    private static Vector3D GetBodyTranslation(CadBody body)
    {
        var x = 0d;
        var y = 0d;
        var z = 0d;

        foreach (var move in body.Features.OfType<MoveFeature>())
        {
            x += move.X;
            y += move.Y;
            z += move.Z;
        }

        return new Vector3D(x, y, z);
    }

    private static string FormatFeatureName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Feature";
        }

        var text = raw.Trim();
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string DescribeSketchProfile(SketchFeature sketch)
    {
        if (sketch.Entities.Count != 1)
        {
            var lineCount = sketch.Entities.Count(entity => entity is CadSketchLine);
            var rectangleCount = sketch.Entities.Count(entity => entity is CadSketchRectangle);
            var circleCount = sketch.Entities.Count(entity => entity is CadSketchCircle);
            var arcCount = sketch.Entities.Count(entity => entity is CadSketchArc);
            var pointCount = sketch.Entities.Count(entity => entity is CadSketchPoint);
            var parts = new List<string>();

            if (lineCount > 0)
            {
                parts.Add($"{lineCount.ToString(CultureInfo.InvariantCulture)} line{(lineCount == 1 ? string.Empty : "s")}");
            }

            if (rectangleCount > 0)
            {
                parts.Add($"{rectangleCount.ToString(CultureInfo.InvariantCulture)} rectangle{(rectangleCount == 1 ? string.Empty : "s")}");
            }

            if (circleCount > 0)
            {
                parts.Add($"{circleCount.ToString(CultureInfo.InvariantCulture)} circle{(circleCount == 1 ? string.Empty : "s")}");
            }

            if (arcCount > 0)
            {
                parts.Add($"{arcCount.ToString(CultureInfo.InvariantCulture)} arc{(arcCount == 1 ? string.Empty : "s")}");
            }

            if (pointCount > 0)
            {
                parts.Add($"{pointCount.ToString(CultureInfo.InvariantCulture)} point{(pointCount == 1 ? string.Empty : "s")}");
            }

            return parts.Count > 0
                ? string.Join(" + ", parts)
                : $"{sketch.Entities.Count.ToString(CultureInfo.InvariantCulture)} entities";
        }

        return sketch.Entities[0] switch
        {
            CadSketchLine => "Line",
            CadSketchRectangle => "Rectangle",
            CadSketchCircle => "Circle",
            CadSketchArc => "Arc",
            CadSketchPoint => "Point",
            _ => "Profile"
        };
    }

    private static string BuildSketchEntitySummary(CadSketchEntity entity)
    {
        return entity switch
        {
            CadSketchLine line =>
                $"({FormatNumber(line.StartX)}, {FormatNumber(line.StartY)}) -> ({FormatNumber(line.EndX)}, {FormatNumber(line.EndY)})",
            CadSketchRectangle rectangle =>
                $"X {FormatNumber(rectangle.X)} | Y {FormatNumber(rectangle.Y)} | W {FormatNumber(rectangle.Width)} | H {FormatNumber(rectangle.Height)}",
            CadSketchCircle circle =>
                $"CX {FormatNumber(circle.CenterX)} | CY {FormatNumber(circle.CenterY)} | R {FormatNumber(circle.Radius)}",
            CadSketchArc arc =>
                $"CX {FormatNumber(arc.CenterX)} | CY {FormatNumber(arc.CenterY)} | R {FormatNumber(arc.Radius)} | {FormatNumber(arc.StartAngleDegrees)}° → {FormatNumber(arc.EndAngleDegrees)}°",
            CadSketchPoint point =>
                $"X {FormatNumber(point.X)} | Y {FormatNumber(point.Y)}",
            _ => entity.EntityType
        };
    }

    private static string BuildSketchDimensionSummary(CadSketchDimension dimension)
    {
        var unitSuffix = dimension.Kind is CadSketchDimensionKind.Length or CadSketchDimensionKind.Radius or CadSketchDimensionKind.Diameter
            ? " mm"
            : string.Empty;
        return $"{dimension.Kind} | {FormatNumber(dimension.Value)}{unitSuffix}";
    }

    private static (SketchFeature? ParentSketch, CadSketchEntity? Entity) FindSelectedSketchEntity(CadProject project, Guid entityId)
    {
        foreach (var body in project.Scene.Bodies)
        {
            foreach (var sketch in body.Features.OfType<SketchFeature>())
            {
                var entity = sketch.Entities.FirstOrDefault(item => item.Id == entityId);
                if (entity is not null)
                {
                    return (sketch, entity);
                }
            }
        }

        return (null, null);
    }

    private void ResetAssistantModelsForProvider(string provider, bool preserveSelection)
    {
        var previousSelection = _selectedAssistantModel;
        AssistantModels.Clear();

        var options = string.Equals(provider, "Anthropic", StringComparison.Ordinal)
            ? new[] { "Claude Sonnet 4.5" }
            : new[] { "GPT-5.4 mini", "GPT-5.4" };

        foreach (var option in options)
        {
            AssistantModels.Add(option);
        }

        var nextSelection = preserveSelection && options.Contains(previousSelection, StringComparer.Ordinal)
            ? previousSelection
            : options[0];

        if (!string.Equals(_selectedAssistantModel, nextSelection, StringComparison.Ordinal))
        {
            _selectedAssistantModel = nextSelection;
            RaisePropertyChanged(nameof(SelectedAssistantModel));
        }
    }

    private void UpdateAssistantShellSummary()
    {
        if (IsAssistantConfigured)
        {
            AssistantConfigurationSummary =
                $"Connected via {SelectedAssistantProvider} | {SelectedAssistantModel} | key {MaskApiKey(_assistantConfiguration.ApiKey)}";
            return;
        }

        if (!string.IsNullOrWhiteSpace(_assistantConfiguration.ValidationError))
        {
            AssistantConfigurationSummary =
                $"{SelectedAssistantProvider} selected | {SelectedAssistantModel}. {_assistantConfiguration.MissingKeyHint}";
            return;
        }

        AssistantConfigurationSummary =
            $"{SelectedAssistantProvider} selected | {SelectedAssistantModel}. Local CAD commands remain available.";
    }

    private void UpdateSketchHints(CadProject project)
    {
        var activeSketch = ResolveSelectedSketch(project)
            ?? project.Scene.Bodies
                .SelectMany(body => body.Features.OfType<SketchFeature>())
                .LastOrDefault();

        if (project.ActiveSketchSession is not null)
        {
            SketchConstraintHint = "Constraints: Horizontal · Vertical · Coincident · Equal · Fix · Parallel · Perpendicular · Concentric · Tangent";
            SketchDimensionHint = "Driving dimensions solve geometry; reference dimensions report it.";
            return;
        }

        if (activeSketch is null)
        {
            SketchConstraintHint = "Constraints: Horizontal · Vertical · Coincident · Equal · Fix · Parallel · Perpendicular · Concentric · Tangent";
            SketchDimensionHint = "Dimensions appear when a sketch is selected or committed.";
            return;
        }

        var constraintKinds = activeSketch.Constraints
            .Select(item => item.Kind.ToString())
            .Distinct(StringComparer.Ordinal)
            .Take(4)
            .ToArray();
        SketchConstraintHint = constraintKinds.Length > 0
            ? $"Constraints: {string.Join(" · ", constraintKinds)}"
            : "Constraints: none inferred yet";

        var dimensionLabels = activeSketch.Dimensions
            .Select(item => item.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Take(4)
            .ToArray();
        SketchDimensionHint = dimensionLabels.Length > 0
            ? $"Dimensions: {string.Join(" · ", dimensionLabels)}"
            : "Dimensions: none available yet";
    }

    private void RebuildSketchPanels(CadProject project)
    {
        ActiveSketchConstraints.Clear();
        ActiveSketchDimensions.Clear();

        IReadOnlyList<CadSketchConstraint> constraints;
        IReadOnlyList<CadSketchDimension> dimensions;
        IReadOnlyList<CadSketchEntity> entities;
        bool editable;

        if (project.ActiveSketchSession is { } session && session.DraftEntities.Count > 0)
        {
            var probe = new SketchFeature { Entities = session.DraftEntities.ToList() };
            constraints = MergeDisplayConstraints(probe.GetBasicConstraints(), session.ManualConstraints);
            dimensions = [..probe.GetBasicDimensions(), ..session.ManualDimensions];
            entities = session.DraftEntities;
            editable = false;
        }
        else if (project.Selection.Kind == CadEntityKind.Sketch
                 && FindSelectedSketchFeature(project) is { } selected)
        {
            constraints = MergeDisplayConstraints(selected.GetBasicConstraints(), selected.Constraints);
            dimensions = selected.GetBasicDimensions();
            entities = selected.Entities;
            editable = true;
        }
        else
        {
            RaisePropertyChanged(nameof(HasActiveSketchConstraints));
            RaisePropertyChanged(nameof(HasActiveSketchDimensions));
            return;
        }

        for (var i = 0; i < constraints.Count; i++)
        {
            var c = constraints[i];
            ActiveSketchConstraints.Add(new SketchConstraintDisplayItem
            {
                Index = i,
                Icon = ConstraintIcon(c.Kind),
                Description = c.Description
            });
        }

        foreach (var dim in dimensions)
        {
            var entity = entities.FirstOrDefault(e => dim.EntityIds.Contains(e.Id));
            var key = dim.ParameterKey ?? dim.Label;
            var isEditable = editable && !string.IsNullOrEmpty(dim.ParameterKey) && entity is not null;
            ActiveSketchDimensions.Add(new SketchDimensionDisplayItem
            {
                EntityId = entity?.Id ?? Guid.Empty,
                Key = key,
                Label = dim.Label,
                Value = dim.Value,
                IsEditable = isEditable,
                Unit = dim.Kind == CadSketchDimensionKind.Angle ? "°" : "mm"
            });
        }

        RaisePropertyChanged(nameof(HasActiveSketchConstraints));
        RaisePropertyChanged(nameof(HasActiveSketchDimensions));
        RaisePropertyChanged(nameof(ActiveSketchConstraintCountLabel));
        RaisePropertyChanged(nameof(ActiveSketchDimensionCountLabel));
    }

    public string ActiveSketchConstraintCountLabel => $"CONSTRAINTS ({ActiveSketchConstraints.Count})";

    public string ActiveSketchDimensionCountLabel => $"DIMENSIONS ({ActiveSketchDimensions.Count})";

    private static SketchFeature? FindSelectedSketchFeature(CadProject project)
    {
        if (project.Selection.Kind != CadEntityKind.Sketch)
        {
            return null;
        }

        foreach (var body in project.Scene.Bodies)
        {
            var sketch = body.Features.OfType<SketchFeature>()
                .FirstOrDefault(f => f.Id == project.Selection.EntityId);
            if (sketch is not null)
            {
                return sketch;
            }
        }

        return null;
    }

    private static string ConstraintIcon(CadSketchConstraintKind kind) => kind switch
    {
        CadSketchConstraintKind.Horizontal => "H",
        CadSketchConstraintKind.Vertical => "V",
        CadSketchConstraintKind.Coincident => "◉",
        CadSketchConstraintKind.EqualRadius => "=",
        CadSketchConstraintKind.EqualLength => "=",
        CadSketchConstraintKind.Fixed => "F",
        _ => "?"
    };

    private static IReadOnlyList<CadSketchConstraint> MergeDisplayConstraints(
        IEnumerable<CadSketchConstraint> inferred,
        IEnumerable<CadSketchConstraint> manual)
    {
        var merged = new List<CadSketchConstraint>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var constraint in inferred.Concat(manual))
        {
            if (seen.Add(ConstraintDisplayKey(constraint)))
            {
                merged.Add(constraint);
            }
        }

        return merged;
    }

    private static string ConstraintDisplayKey(CadSketchConstraint constraint)
    {
        var ids = constraint.EntityIds
            .Select(id => id.ToString("N"))
            .OrderBy(id => id, StringComparer.Ordinal);
        return $"{constraint.Kind}:{string.Join(",", ids)}";
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _workspaceController.WorkspaceChanged -= OnWorkspaceChanged;
        _workspaceController.DocumentChanged -= OnDocumentChanged;
        _autoDismissCts?.Cancel();
        _isDisposed = true;
    }
}

public enum NotificationSeverity { Info, Warning, Error }

internal sealed record StudioNotification(string Text, NotificationSeverity Severity);

public sealed class SketchConstraintDisplayItem
{
    public int Index { get; init; }
    public string Icon { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class SketchDimensionDisplayItem
{
    public Guid EntityId { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public double Value { get; init; }
    public bool IsEditable { get; init; }
    public string Unit { get; init; } = "mm";
}

public sealed record PartStudioTabItem(string Name, bool IsActive);
