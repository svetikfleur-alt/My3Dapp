using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FormaCore.Engine;
using My3DApp.AvaloniaApp.Dialogs;
using My3DApp.AvaloniaApp.Controls;
using My3DApp.AvaloniaApp.Services;
using My3DApp.AvaloniaApp.ViewModels;
using AApplication = Avalonia.Application;
using AButton = Avalonia.Controls.Button;
using AMenuItem = Avalonia.Controls.MenuItem;
using ANumericUpDown = Avalonia.Controls.NumericUpDown;
using ATextBox = Avalonia.Controls.TextBox;

namespace My3DApp.AvaloniaApp;

public sealed partial class MainWindow : Window
{
    private StudioShellViewModel? _wiredViewModel;
    private StudioNativeViewport? _viewportHost;
    private ExactSceneController? _exactScene;
    private AutosaveService? _autosaveService;
    private ToggleButton? _lightThemeButton;
    private ToggleButton? _darkThemeButton;
    private ToggleButton? _moveToolButton;
    private AButton? _recentFilesButton;
    private ToggleButton? _sketchTransformToolButton;
    private ToggleButton? _sketchRotateToolButton;
    private ToggleButton? _gridToggleButton;
    private ToggleButton? _measureToolToggle;
    private ToggleButton? _sectionViewToggle;
    private Avalonia.Controls.ComboBox? _sectionAxisCombo;
    private ANumericUpDown? _sectionOffsetSpin;
    private Avalonia.Controls.TreeView? _featureTree;
    private bool _allowImmediateClose;
    private readonly List<CommandUiBinding> _commandUiBindings = [];
    private static readonly Dictionary<string, string> SketchToolHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Line"]             = "Line — click to place segment endpoints  ·  Esc to finish",
        ["Rectangle"]        = "Rectangle — click two corners",
        ["Circle"]           = "Circle — click center, then drag radius",
        ["Arc"]              = "Arc — click center, start, then end point",
        ["Point"]            = "Point — click to place a reference point",
        ["Polygon"]          = "Polygon — click center, then circumscribed radius",
        ["Slot"]             = "Slot — click center, width endpoint, then height",
        ["Spline"]           = "Spline — click control points  ·  Esc to finish",
        ["Mirror"]           = "Mirror — select entities, then pick a mirror axis",
        ["Trim"]             = "Trim — click a segment to trim at intersections",
        ["Offset"]           = "Offset — generates an offset copy of all sketch entities",
        ["Fillet2d"]         = "Corner Fillet — click a corner vertex to round it",
        ["Transform"]        = "Transform — drag entities to move them",
        ["Rotate"]           = "Rotate — drag entities to rotate them",
        ["AngleDimension"]   = "Angle Dimension — click two lines to dimension the angle",
        ["LinearDimension"]  = "Linear Dimension — click two points, then place the leader",
        ["RadiusDimension"]  = "Radius Dimension — click a circle or arc",
    };

    private static FilePickerFileType ProjectFileType => new("My3DApp Project")
    {
        Patterns = ["*.umxproj", "*.my3dapp", "*.json"],
        MimeTypes = ["application/json"]
    };

    private sealed record CommandUiBinding(string CommandId, Avalonia.Controls.Control Control);

    public MainWindow()
    {
        InitializeComponent();
        ResolveNamedControls();
        AttachViewportEvents();
        DataContextChanged += OnDataContextChanged;
        Opened += OnWindowOpened;
        AttachViewModelBridge();
        Dispatcher.UIThread.Post(InitializeThemeToggleState, DispatcherPriority.Loaded);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        var disposableViewModel = DataContext as IDisposable;
        Opened -= OnWindowOpened;
        DetachViewModelBridge();
        DetachViewportEvents();
        _autosaveService?.Dispose();
        _autosaveService = null;
        disposableViewModel?.Dispose();
        base.OnClosed(e);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_allowImmediateClose)
        {
            AutosaveService.DeleteAutosave();
            base.OnClosing(e);
            return;
        }

        if (_wiredViewModel is { HasUnsavedChanges: true })
        {
            e.Cancel = true;
            var choice = await PromptSaveChangesAsync("Close My3DApp",
                "You have unsaved changes. Save the current document before closing?");
            if (choice == SaveChangesChoice.Save)
            {
                await SaveProjectAsync(forcePicker: false);
                if (_wiredViewModel is { HasUnsavedChanges: false })
                {
                    AutosaveService.DeleteAutosave();
                    _allowImmediateClose = true;
                    Close();
                }
            }
            else if (choice == SaveChangesChoice.DontSave)
            {
                AutosaveService.DeleteAutosave();
                _allowImmediateClose = true;
                Close();
            }
            return;
        }
        AutosaveService.DeleteAutosave();
        base.OnClosing(e);
    }

    private async Task<bool> ConfirmDiscardAsync(string title, string message)
    {
        var dialog = new Avalonia.Controls.Window
        {
            Title = title,
            Width = 380,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = BuildConfirmPanel(message, out var yesButton, out var noButton)
        };
        var tcs = new TaskCompletionSource<bool>();
        yesButton.Click += (_, _) => { dialog.Close(); tcs.TrySetResult(true); };
        noButton.Click += (_, _) => { dialog.Close(); tcs.TrySetResult(false); };
        dialog.Closed += (_, _) => tcs.TrySetResult(false);
        await dialog.ShowDialog(this);
        return await tcs.Task;
    }

    private static Avalonia.Controls.Control BuildConfirmPanel(string message,
        out Avalonia.Controls.Button yesButton, out Avalonia.Controls.Button noButton)
    {
        var text = new Avalonia.Controls.TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(20, 20, 20, 16)
        };
        var yes = new Avalonia.Controls.Button { Content = "Discard & Continue", Margin = new Avalonia.Thickness(0, 0, 8, 0) };
        var no = new Avalonia.Controls.Button { Content = "Cancel" };
        var buttons = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(20, 0, 20, 20),
            Spacing = 8
        };
        buttons.Children.Add(yes);
        buttons.Children.Add(no);
        var panel = new Avalonia.Controls.StackPanel();
        panel.Children.Add(text);
        panel.Children.Add(buttons);
        yesButton = yes;
        noButton = no;
        return panel;
    }

    private enum SaveChangesChoice
    {
        Cancel,
        Save,
        DontSave
    }

    private sealed record NewProjectTypeChoice(string Type, string Label, string Description)
    {
        public override string ToString() => Label;
    }

    private async Task<SaveChangesChoice> PromptSaveChangesAsync(string title, string message)
    {
        var dialog = new Avalonia.Controls.Window
        {
            Title = title,
            Width = 420,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var saveButton = new Avalonia.Controls.Button { Content = "Save", MinWidth = 86 };
        var dontSaveButton = new Avalonia.Controls.Button { Content = "Don't Save", MinWidth = 96 };
        var cancelButton = new Avalonia.Controls.Button { Content = "Cancel", MinWidth = 86 };
        var tcs = new TaskCompletionSource<SaveChangesChoice>();

        saveButton.Click += (_, _) => { dialog.Close(); tcs.TrySetResult(SaveChangesChoice.Save); };
        dontSaveButton.Click += (_, _) => { dialog.Close(); tcs.TrySetResult(SaveChangesChoice.DontSave); };
        cancelButton.Click += (_, _) => { dialog.Close(); tcs.TrySetResult(SaveChangesChoice.Cancel); };
        dialog.Closed += (_, _) => tcs.TrySetResult(SaveChangesChoice.Cancel);

        dialog.Content = new Avalonia.Controls.StackPanel
        {
            Children =
            {
                new Avalonia.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(20, 20, 20, 16)
                },
                new Avalonia.Controls.StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Margin = new Avalonia.Thickness(20, 0, 20, 20),
                    Spacing = 8,
                    Children = { saveButton, dontSaveButton, cancelButton }
                }
            }
        };

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }

    private static void LogHandlerFailure(string handlerName, Exception ex)
    {
        RuntimeLog.Write("MainWindow", $"{handlerName} failed.", ex);
    }

    // -----------------------------------------------------------------
    // Native exact-CAD viewport wiring (the new viewport contract).

    private void OnExactViewportReady(object? sender, EventArgs e)
    {
        var host = _viewportHost?.ExactHost;
        if (host is null)
        {
            return;
        }

        _exactScene = new ExactSceneController(host);
        host.SetSelectionMode(FormaCore.Engine.Exact.TopologyKind.Face, true);
        host.SetSelectionMode(FormaCore.Engine.Exact.TopologyKind.Edge, true);
        
        if (_wiredViewModel != null)
        {
            _wiredViewModel.AclWorkspace.BuildCoordinator.Session.SetKernel(_exactScene.Kernel);
            _wiredViewModel.AclWorkspace.BuildCompleted += (s, graph) => 
            {
                if (graph != null)
                {
                    _exactScene.DisplayFeatureGraph(graph);
                }
                else
                {
                    _exactScene.ClearFeatureGraph();
                }
            };
            
            // Build the initial default ACL document to display something
            _wiredViewModel.AclWorkspace.Build();
        }

        // Developer Mode scripted check: --export-validation-step <path> shows the
        // validation body and exports it through the product kernel path.
        if (ExactMigration.IsDeveloperMode)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "--export-validation-step");
            if (i >= 0 && i + 1 < args.Length)
            {
                try
                {
                    _exactScene.ShowValidationBody();
                    _exactScene.ExportValidationStep(args[i + 1]);
                    RuntimeLog.Write("MainWindow", $"Validation STEP exported to {args[i + 1]}");
                }
                catch (Exception ex)
                {
                    LogHandlerFailure(nameof(OnExactViewportReady), ex);
                }
            }
        }
        host.SelectionChanged += (_, sel) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            _wiredViewModel?.SetExactSelectionSummary(
                sel.Kind == FormaCore.Engine.Exact.TopologyKind.Body
                    ? $"Body {sel.BodyId.ToString("N")[..8]}"
                    : $"{sel.Kind} #{sel.TransientIndex} · body {sel.BodyId.ToString("N")[..8]}"));
    }

    private void OnNativeViewClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AButton { Tag: string tag } &&
            Enum.TryParse<FormaCore.Engine.Exact.CadStandardView>(tag, out var view))
        {
            _viewportHost?.ExactHost?.SetView(view);
        }
    }

    private void OnNativeFitClick(object? sender, RoutedEventArgs e)
        => _viewportHost?.ExactHost?.FitAll();

    private void OnNativeDisplayModeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AButton { Tag: string tag } &&
            Enum.TryParse<FormaCore.Engine.Exact.CadDisplayMode>(tag, out var mode))
        {
            _viewportHost?.ExactHost?.SetDisplayMode(mode);
        }
    }

    // Developer Mode only (MY3DAPP_DEVELOPER=1): kernel/viewport validation geometry.

    private void OnDevValidateKernelClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_exactScene is null)
            {
                _wiredViewModel?.ShowNotification("Native viewport is not ready yet.", NotificationSeverity.Warning);
                return;
            }

            var bounds = _exactScene.ShowValidationBody();
            _wiredViewModel?.ShowNotification(
                $"Validation body displayed: {bounds.XMax:0.###} × {bounds.YMax:0.###} × {bounds.ZMax:0.###} mm (developer check, not a document).",
                NotificationSeverity.Success);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnDevValidateKernelClick), ex);
            _wiredViewModel?.ShowNotification($"Kernel validation failed: {ex.Message}", NotificationSeverity.Error);
        }
    }

    private void OnDevClearValidationClick(object? sender, RoutedEventArgs e)
    {
        _exactScene?.ClearValidationBody();
        _wiredViewModel?.SetExactSelectionSummary(string.Empty);
    }

    private async void OnDevExportValidationStepClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_exactScene is null || !_exactScene.HasValidationBody)
            {
                _wiredViewModel?.ShowNotification("Show the validation body first.", NotificationSeverity.Warning);
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export validation STEP (developer)",
                SuggestedFileName = "kernel-validation.step",
                FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("STEP") { Patterns = ["*.step", "*.stp"] }],
            });
            if (file is null)
            {
                return;
            }

            _exactScene.ExportValidationStep(file.Path.LocalPath);
            _wiredViewModel?.ShowNotification($"STEP exported: {file.Path.LocalPath}", NotificationSeverity.Success);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnDevExportValidationStepClick), ex);
            _wiredViewModel?.ShowNotification($"STEP export failed: {ex.Message}", NotificationSeverity.Error);
        }
    }

    private void ResolveNamedControls()
    {
        _commandUiBindings.Clear();
        _viewportHost = this.FindControl<StudioNativeViewport>("ViewportHost");
        if (_viewportHost is not null)
        {
            _viewportHost.ExactHostReady += OnExactViewportReady;
        }
        _lightThemeButton = this.FindControl<ToggleButton>("LightThemeButton");
        _darkThemeButton = this.FindControl<ToggleButton>("DarkThemeButton");
        _moveToolButton = this.FindControl<ToggleButton>("MoveToolButton");
        _recentFilesButton = this.FindControl<AButton>("RecentFilesButton");
        _sketchTransformToolButton = this.FindControl<ToggleButton>("SketchTransformToolButton");
        _sketchRotateToolButton = this.FindControl<ToggleButton>("SketchRotateToolButton");
        _gridToggleButton = this.FindControl<ToggleButton>("GridToggleButton");
        _measureToolToggle = this.FindControl<ToggleButton>("MeasureToolToggle");
        _sectionViewToggle = this.FindControl<ToggleButton>("SectionViewToggle");
        _sectionAxisCombo = this.FindControl<Avalonia.Controls.ComboBox>("SectionAxisCombo");
        _sectionOffsetSpin = this.FindControl<ANumericUpDown>("SectionOffsetSpin");
        _featureTree = this.FindControl<Avalonia.Controls.TreeView>("FeatureTreeView");

        if (_viewportHost is null)
        {
            RuntimeLog.Write("MainWindow", "ViewportHost control was not found in XAML.");
        }

        RegisterCommandBindings();
    }

    private void AttachViewportEvents()
    {
        if (_viewportHost is null)
        {
            return;
        }

        _viewportHost.SelectionChanged += OnViewportSelectionChanged;
        _viewportHost.SketchPlacementRequested += OnViewportSketchPlacementRequested;
        _viewportHost.SketchPreviewRequested += OnViewportSketchPreviewRequested;
        _viewportHost.SketchPreviewCleared += OnViewportSketchPreviewCleared;
        _viewportHost.SketchStepCancelRequested += OnViewportSketchStepCancelRequested;
        _viewportHost.ToggleConstructionModeRequested += OnViewportToggleConstructionModeRequested;
        _viewportHost.DeleteRequested += OnViewportDeleteRequested;
        _viewportHost.BodyTransformCommitted += OnViewportBodyTransformCommitted;
        _viewportHost.MoveToolDeactivated += OnMoveToolDeactivated;
        _viewportHost.SketchTransformToolDeactivated += OnSketchTransformToolDeactivated;
        _viewportHost.SketchEntityTransformCommitted += OnSketchEntityTransformCommitted;
        _viewportHost.SketchRotateToolDeactivated += OnSketchRotateToolDeactivated;
        _viewportHost.SketchEntityRotationCommitted += OnSketchEntityRotationCommitted;
        _viewportHost.BoxSelectCompleted += OnBoxSelectCompleted;
        _viewportHost.SelectionCleared += OnViewportSelectionCleared;
        _viewportHost.FaceClicked += OnFaceClicked;
        _viewportHost.SketchEntityContextMenuRequested += OnSketchEntityContextMenuRequested;
        _viewportHost.MeasureResultReceived += OnMeasureResultReceived;
        _viewportHost.MeasureCancelled += OnMeasureCancelled;
    }

    private void DetachViewportEvents()
    {
        if (_viewportHost is null)
        {
            return;
        }

        _viewportHost.SelectionChanged -= OnViewportSelectionChanged;
        _viewportHost.SketchPlacementRequested -= OnViewportSketchPlacementRequested;
        _viewportHost.SketchPreviewRequested -= OnViewportSketchPreviewRequested;
        _viewportHost.SketchPreviewCleared -= OnViewportSketchPreviewCleared;
        _viewportHost.SketchStepCancelRequested -= OnViewportSketchStepCancelRequested;
        _viewportHost.ToggleConstructionModeRequested -= OnViewportToggleConstructionModeRequested;
        _viewportHost.DeleteRequested -= OnViewportDeleteRequested;
        _viewportHost.BodyTransformCommitted -= OnViewportBodyTransformCommitted;
        _viewportHost.MoveToolDeactivated -= OnMoveToolDeactivated;
        _viewportHost.SketchTransformToolDeactivated -= OnSketchTransformToolDeactivated;
        _viewportHost.SketchEntityTransformCommitted -= OnSketchEntityTransformCommitted;
        _viewportHost.SketchRotateToolDeactivated -= OnSketchRotateToolDeactivated;
        _viewportHost.SketchEntityRotationCommitted -= OnSketchEntityRotationCommitted;
        _viewportHost.BoxSelectCompleted -= OnBoxSelectCompleted;
        _viewportHost.SelectionCleared -= OnViewportSelectionCleared;
        _viewportHost.FaceClicked -= OnFaceClicked;
        _viewportHost.SketchEntityContextMenuRequested -= OnSketchEntityContextMenuRequested;
        _viewportHost.MeasureResultReceived -= OnMeasureResultReceived;
        _viewportHost.MeasureCancelled -= OnMeasureCancelled;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachViewModelBridge();
    }

    private void AttachViewModelBridge()
    {
        DetachViewModelBridge();

        _wiredViewModel = DataContext as StudioShellViewModel;
        if (_wiredViewModel is null)
        {
            return;
        }

        _wiredViewModel.ViewportStateChanged += OnViewportStateChanged;
        _wiredViewModel.FocusSelectionRequested += OnFocusSelectionRequested;
        _wiredViewModel.FeatureNodes.CollectionChanged += OnFeatureNodesChanged;
        _wiredViewModel.PropertyChanged += OnViewModelPropertyChanged;
        RequestFeatureTreeExpansion();
        RefreshCommandPresentation();
        _ = ApplyViewportStateAsync(_wiredViewModel.CurrentViewportState);
    }

    private void DetachViewModelBridge()
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        _wiredViewModel.ViewportStateChanged -= OnViewportStateChanged;
        _wiredViewModel.FocusSelectionRequested -= OnFocusSelectionRequested;
        _wiredViewModel.FeatureNodes.CollectionChanged -= OnFeatureNodesChanged;
        _wiredViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _wiredViewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshCommandPresentation();
    }

    private void OnFeatureNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RequestFeatureTreeExpansion();
    }

    private void RequestFeatureTreeExpansion()
    {
        Dispatcher.UIThread.Post(ApplyFeatureTreeExpansion, DispatcherPriority.Background);
    }

    private void ApplyFeatureTreeExpansion()
    {
        if (_featureTree is null)
        {
            return;
        }

        foreach (var treeItem in _featureTree.GetVisualDescendants().OfType<TreeViewItem>())
        {
            if (treeItem.DataContext is FeatureNodeViewModel node && node.IsExpanded)
            {
                treeItem.IsExpanded = true;
            }
        }
    }

    private async void OnViewportStateChanged(object? sender, ViewportRenderState state)
    {
        try
        {
            await ApplyViewportStateAsync(state);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnViewportStateChanged), ex);
        }
    }

    private Task ApplyViewportStateAsync(ViewportRenderState state)
    {
        // CAD_CANON: mesh scene DTOs must not reach the native OCCT viewport.
        // The legacy engine still emits render states during the migration; they are
        // dropped here (counted, never converted, never displayed as fake geometry).
        lock (StudioNativeViewport.CompatCallCounters)
        {
            StudioNativeViewport.CompatCallCounters["MeshSceneDropped"] =
                StudioNativeViewport.CompatCallCounters.GetValueOrDefault("MeshSceneDropped") + 1;
        }
        return Task.CompletedTask;
    }

    private Task ApplyViewportThemeAsync(StudioThemeMode mode)
    {
        // Native viewport theming is handled inside OcctCore presentation setup.
        return Task.CompletedTask;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (_viewportHost is null)
            return;

        try
        {
            await Task.Delay(250);
            await _viewportHost.EnsureInitializedAsync();

            // Exact-kernel migration: the .umxproj autosave/recovery pipeline is a hidden
            // proprietary persistence path and is disabled per CAD_CANON. Save/open moves
            // to ACL files in the ACL phase. Existing autosave files on disk are untouched.
            _wiredViewModel?.UpdateRecoveryStatus(false, null);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnWindowOpened), ex);
        }
    }

    private void OnNewProjectClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
            return;
        if (_wiredViewModel.HasUnsavedChanges)
        {
            _ = NewProjectWithGuardAsync();
            return;
        }
        _wiredViewModel.NewProject();
    }

    private async void OnNewProjectDialogClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        if (!await EnsureSafeToReplaceCurrentProjectAsync("New project",
                "You have unsaved changes. Save the current document before creating a new project?"))
        {
            return;
        }

        var request = await ShowNewProjectDialogAsync();
        if (request is null)
        {
            return;
        }

        _wiredViewModel.StartNewProject(request.Value.Type, request.Value.Name);
        e.Handled = true;
    }

    private async void OnNewLargeProjectClick(object? sender, RoutedEventArgs e)
    {
        await StartProjectFromHomeAsync("large", "Large Project");
        e.Handled = true;
    }

    private async void OnNewStandardProjectClick(object? sender, RoutedEventArgs e)
    {
        await StartProjectFromHomeAsync("standard", "Standard Project");
        e.Handled = true;
    }

    private async void OnNewQuickDesignClick(object? sender, RoutedEventArgs e)
    {
        await StartProjectFromHomeAsync("quick", "Quick Design");
        e.Handled = true;
    }

    private async Task StartProjectFromHomeAsync(string projectType, string defaultName)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        if (!await EnsureSafeToReplaceCurrentProjectAsync("New project",
                "You have unsaved changes. Save the current document before creating a new project?"))
        {
            return;
        }

        _wiredViewModel.StartNewProject(projectType, defaultName);
    }

    private async Task<(string Type, string Name)?> ShowNewProjectDialogAsync()
    {
        var projectTypes = new[]
        {
            new NewProjectTypeChoice("large", "Large Project", "Creates multiple part studios for assemblies, variants, or longer design sessions."),
            new NewProjectTypeChoice("standard", "Standard Project", "Creates one saved project with one active part studio."),
            new NewProjectTypeChoice("quick", "Quick Design", "Creates a saved local scratch project for fast repair or household parts.")
        };
        var nameBox = new ATextBox
        {
            Text = "Standard Project",
            Watermark = "Project name",
            MinWidth = 280,
            Classes = { "FeatureDialogInput" }
        };
        var typeBox = new Avalonia.Controls.ComboBox
        {
            ItemsSource = projectTypes,
            SelectedItem = projectTypes[1],
            MinWidth = 160,
            Classes = { "FeatureDialogInput" }
        };
        var typeDescription = new Avalonia.Controls.TextBlock
        {
            Text = projectTypes[1].Description,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Classes = { "ToolbarSubtleText" }
        };

        (string Type, string Name)? result = null;
        var createButton = new Avalonia.Controls.Button
        {
            Content = "Create Project",
            Classes = { "FeatureDialogButtonPrimary" },
            MinWidth = 120
        };
        var cancelButton = new Avalonia.Controls.Button
        {
            Content = "Cancel",
            Classes = { "FeatureDialogButtonSecondary" },
            MinWidth = 90
        };

        var dialog = new Avalonia.Controls.Window
        {
            Title = "New Project",
            Width = 440,
            Height = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            Classes = { "FeatureDialogWindow" },
            Content = new Border
            {
                Classes = { "FeatureDialogPanel" },
                Child = new StackPanel
                {
                    Margin = new Avalonia.Thickness(18),
                    Spacing = 12,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock
                        {
                            Text = "Create Project",
                            Classes = { "FeatureDialogTitle" }
                        },
                        new Avalonia.Controls.TextBlock
                        {
                            Text = "Choose the project scale and give the new document a clear name before you start modeling.",
                            Classes = { "FeatureDialogSubtitle" },
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Margin = new Avalonia.Thickness(0, 6, 0, 0)
                        },
                        new Border
                        {
                            Classes = { "FeatureDialogSectionCard" },
                            Padding = new Avalonia.Thickness(12),
                            Child = new StackPanel
                            {
                                Spacing = 10,
                                Children =
                                {
                                    new Avalonia.Controls.TextBlock
                                    {
                                        Text = "Project name",
                                        Classes = { "FeatureDialogFieldLabel" }
                                    },
                                    nameBox,
                                    new Avalonia.Controls.TextBlock
                                    {
                                        Text = "Project type",
                                        Classes = { "FeatureDialogFieldLabel" }
                                    },
                                    typeBox,
                                    typeDescription
                                }
                            }
                        },
                        new Avalonia.Controls.StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { cancelButton, createButton }
                        }
                    }
                }
            }
        };

        typeBox.SelectionChanged += (_, _) =>
        {
            if (typeBox.SelectedItem is not NewProjectTypeChoice choice)
            {
                return;
            }

            typeDescription.Text = choice.Description;
            if (projectTypes.Any(projectType => string.Equals(nameBox.Text, projectType.Label, StringComparison.Ordinal)))
            {
                nameBox.Text = choice.Label;
            }
        };

        createButton.Click += (_, _) =>
        {
            var choice = typeBox.SelectedItem as NewProjectTypeChoice ?? projectTypes[1];
            var type = choice.Type;
            var name = string.IsNullOrWhiteSpace(nameBox.Text) ? choice.Label : nameBox.Text.Trim();
            result = (type, name);
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        await ShowAnchoredDialogAsync(dialog, returnFocus: false);
        return result;
    }

    private async Task NewProjectWithGuardAsync()
    {
        if (_wiredViewModel is null) return;
        var choice = await PromptSaveChangesAsync("New document", "Save the current document before creating a new one?");
        if (choice == SaveChangesChoice.Save)
        {
            await SaveProjectAsync(forcePicker: false);
            if (_wiredViewModel.HasUnsavedChanges)
            {
                return;
            }
            _wiredViewModel.NewProject();
        }
        else if (choice == SaveChangesChoice.DontSave)
        {
            _wiredViewModel.NewProject();
        }
    }

    private void OnDismissNotificationClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.DismissNotification();
    }

    private void OnDeleteSketchConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AButton btn && btn.Tag is int index)
        {
            _wiredViewModel?.DeleteSketchConstraint(index);
        }
    }

    private async void OnFocusSelectionClick(object? sender, RoutedEventArgs e)
    {
        if (_viewportHost is null)
        {
            return;
        }

        try
        {
            await _viewportHost.FocusSelectionAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnFocusSelectionClick), ex);
        }

        e.Handled = true;
    }

    private async void OnFocusSelectionRequested(object? sender, EventArgs e)
    {
        if (_viewportHost is null)
        {
            return;
        }

        try
        {
            await _viewportHost.FocusSelectionAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnFocusSelectionRequested), ex);
        }
    }

    private async void OnViewportSelectionChanged(object? sender, ViewportSelectionChangedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.HandleViewportSelectionAsync(e.EntityId);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnViewportSelectionChanged), ex);
        }
    }

    private async void OnViewportSketchPlacementRequested(object? sender, ViewportSketchPlacementEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.HandleViewportSketchPlacementAsync(e.U, e.V);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnViewportSketchPlacementRequested), ex);
        }
    }

    private async void OnViewportSketchPreviewRequested(object? sender, ViewportSketchPreviewEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.HandleViewportSketchPreviewAsync(e.U, e.V);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnViewportSketchPreviewRequested), ex);
        }
    }

    private async void OnViewportSketchPreviewCleared(object? sender, EventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ClearViewportSketchPreviewAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnViewportSketchPreviewCleared), ex);
        }
    }

    private async void OnViewportSketchStepCancelRequested(object? sender, EventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.CancelSketchStepAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnViewportSketchStepCancelRequested), ex);
        }
    }

    private void OnViewportToggleConstructionModeRequested(object? sender, EventArgs e)
    {
        OnToggleConstructionModeClick(sender, new RoutedEventArgs());
    }

    private async void OnOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
            return;

        if (!await EnsureSafeToReplaceCurrentProjectAsync("Open project",
                "You have unsaved changes. Save the current document before opening another project?"))
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open My3DApp project",
            AllowMultiple = false,
            FileTypeFilter =
            [
                ProjectFileType
            ]
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(path))
        {
            _wiredViewModel.AppendToLog("Open project canceled or no local file was selected.");
            return;
        }

        await OpenProjectWithGuardAsync(path);
    }

    private async Task<bool> EnsureSafeToReplaceCurrentProjectAsync(string actionName, string prompt)
    {
        if (_wiredViewModel is null)
        {
            return false;
        }

        if (_wiredViewModel.HasUnsavedChanges)
        {
            var choice = await PromptSaveChangesAsync(actionName, prompt);
            if (choice == SaveChangesChoice.Save)
            {
                await SaveProjectAsync(forcePicker: false);
                if (_wiredViewModel.HasUnsavedChanges)
                {
                    return false;
                }
            }
            else if (choice == SaveChangesChoice.Cancel)
            {
                return false;
            }
        }

        return true;
    }

    private Task OpenProjectWithGuardAsync(string path)
    {
        if (_wiredViewModel is null || string.IsNullOrWhiteSpace(path))
        {
            return Task.CompletedTask;
        }

        _wiredViewModel.OpenProject(path);
        return Task.CompletedTask;
    }

    private void OnRecentFilesClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        var recents = _wiredViewModel.RecentFiles;
        if (recents.Count == 0)
        {
            return;
        }

        var menu = new Avalonia.Controls.ContextMenu();
        foreach (var path in recents)
        {
            var displayName = System.IO.Path.GetFileName(path);
            var item = new AMenuItem { Header = displayName };
            Avalonia.Controls.ToolTip.SetTip(item, path);
            var capturedPath = path;
            item.Click += (_, _) => _wiredViewModel?.OpenProject(capturedPath);
            menu.Items.Add(item);
        }

        menu.Open((Avalonia.Controls.Control?)_recentFilesButton ?? this);
        e.Handled = true;
    }

    private async void OnSaveProjectClick(object? sender, RoutedEventArgs e)
    {
        await SaveProjectAsync(forcePicker: false);
    }

    private async void OnSaveAsProjectClick(object? sender, RoutedEventArgs e)
    {
        await SaveProjectAsync(forcePicker: true);
    }

    private async Task SaveProjectAsync(bool forcePicker)
    {
        if (_wiredViewModel is null)
            return;

        var path = forcePicker ? null : _wiredViewModel.CurrentProjectPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save My3DApp project",
                SuggestedFileName = "part-studio.umxproj",
                DefaultExtension = "umxproj",
                FileTypeChoices =
                [
                    ProjectFileType
                ]
            });

            path = file?.TryGetLocalPath();
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            _wiredViewModel.AppendToLog("Save project canceled or no local path was selected.");
            return;
        }

        _wiredViewModel.SaveProject(path);
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.UndoAsync();
        e.Handled = true;
    }

    private void OnRedoClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.RedoAsync();
        e.Handled = true;
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
            return;

        var hasSelection = _wiredViewModel.CurrentSelectedBodyId != Guid.Empty;
        var selectedBodyId = _wiredViewModel.CurrentSelectedBodyId;

        var dialog = new ExportDialog(
            hasSelection,
            (format, allBodies, path) =>
            {
                try
                {
                    _wiredViewModel.ExportFormat(format, allBodies, selectedBodyId, path);
                    _wiredViewModel.AppendToLog($"Exported to {System.IO.Path.GetFileName(path)}");
                    return Task.FromResult<string?>(null);
                }
                catch (Exception ex)
                {
                    return Task.FromResult<string?>(ex.Message);
                }
            });

        await dialog.ShowDialog<bool?>(this);
    }

    private void OnQuickExportClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
            return;

        _wiredViewModel.QuickExport();
    }

    private async void OnViewportDeleteRequested(object? sender, EventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.HandleViewportDeleteAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnViewportDeleteRequested), ex);
        }
    }

    private async void OnViewportBodyTransformCommitted(object? sender, ViewportBodyTransformEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.HandleViewportBodyTransformAsync(e.BodyId, e.X, e.Y, e.Z);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnViewportBodyTransformCommitted), ex);
        }
    }

    private void OnSketchEntityContextMenuRequested(object? sender, ViewportSketchEntityContextMenuEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        var info = _wiredViewModel.GetSketchEntityParameters(e.EntityId);
        var isConstruction = info?.Parameters
            .Any(p => string.Equals(p.Name, "IsConstruction", StringComparison.OrdinalIgnoreCase)
                   && string.Equals(p.DisplayValue, "true", StringComparison.OrdinalIgnoreCase))
            ?? false;

        var deleteItem = new AMenuItem { Header = "Delete" };
        deleteItem.Click += async (_, _) =>
        {
            try
            {
                await _wiredViewModel.ApplySketchEntityDeleteAsync(e.EntityId);
            }
            catch (Exception ex)
            {
                LogHandlerFailure("SketchEntityDelete", ex);
            }
        };

        var constructionHeader = isConstruction ? "Remove Construction" : "Set as Construction";
        var constructionItem = new AMenuItem { Header = constructionHeader };
        constructionItem.Click += async (_, _) =>
        {
            try
            {
                await _wiredViewModel.ApplySketchEntityConstructionAsync(e.EntityId);
            }
            catch (Exception ex)
            {
                LogHandlerFailure("SketchEntityConstruction", ex);
            }
        };

        var editItem = new AMenuItem { Header = "Edit value…" };
        editItem.Click += async (_, _) =>
        {
            try
            {
                await ShowSketchEntityEditDialogAsync(e.EntityId, e.EntityType);
            }
            catch (Exception ex)
            {
                LogHandlerFailure("SketchEntityEdit", ex);
            }
        };

        var menu = new Avalonia.Controls.ContextMenu
        {
            Items = { deleteItem, constructionItem, new Avalonia.Controls.Separator(), editItem }
        };

        menu.Open(this);
    }

    private async Task ShowSketchEntityEditDialogAsync(Guid entityId, string entityType)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        var info = _wiredViewModel.GetSketchEntityParameters(entityId);
        if (info is null)
        {
            return;
        }

        var editableParams = info.Value.Parameters
            .Where(p => p.Editable && p.NumericValue.HasValue)
            .Select(p => (p.Name, p.Name, p.DisplayValue))
            .ToList();

        if (editableParams.Count == 0)
        {
            return;
        }

        var dialog = new ParameterEditDialog($"Edit {info.Value.EntityType}", $"Adjust the values for this {info.Value.EntityType}.", editableParams);
        var result = await dialog.ShowDialog<ParameterEditDialogResult?>(this);
        if (result is null)
        {
            return;
        }

        foreach (var (key, valueStr) in result.ValuesByKey)
        {
            if (double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                await _wiredViewModel.EditSketchDimensionAsync(entityId, key, value);
            }
        }
    }

    private void OnMoveToolDeactivated(object? sender, EventArgs e)
    {
        if (_moveToolButton is not null)
        {
            _moveToolButton.IsChecked = false;
        }
    }

    private void OnSketchTransformToolDeactivated(object? sender, EventArgs e)
    {
        if (_sketchTransformToolButton is not null)
        {
            _sketchTransformToolButton.IsChecked = false;
        }

        if (_wiredViewModel is not null)
        {
            _ = _wiredViewModel.SelectSketchToolAsync("Rectangle");
        }
        ApplySketchToolHint("Rectangle");
    }

    private async void OnSketchEntityTransformCommitted(object? sender, ViewportSketchEntityTransformEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.HandleSketchEntityTransformAsync(e.SketchId, e.Du, e.Dv);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchEntityTransformCommitted), ex);
        }
    }

    private async void OnSketchTransformToolClick(object? sender, RoutedEventArgs e)
    {
        if (_viewportHost is null || _wiredViewModel is null)
        {
            return;
        }

        var isActive = _sketchTransformToolButton?.IsChecked == true;
        try
        {
            if (isActive)
            {
                await _wiredViewModel.SelectSketchToolAsync("Transform");
                ApplySketchToolHint("Transform");
            }
            else
            {
                ApplySketchToolHint("Rectangle");
            }

            await _viewportHost.SetSketchTransformToolActiveAsync(isActive);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchTransformToolClick), ex);
        }

        e.Handled = true;
    }

    private void OnSketchRotateToolDeactivated(object? sender, EventArgs e)
    {
        if (_sketchRotateToolButton is not null)
        {
            _sketchRotateToolButton.IsChecked = false;
        }

        if (_wiredViewModel is not null)
        {
            _ = _wiredViewModel.SelectSketchToolAsync("Rectangle");
        }
        ApplySketchToolHint("Rectangle");
    }

    private async void OnSketchEntityRotationCommitted(object? sender, ViewportSketchEntityRotationEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.HandleSketchEntityRotationAsync(e.SketchId, e.PivotU, e.PivotV, e.AngleDeg);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchEntityRotationCommitted), ex);
        }
    }

    private async void OnSketchRotateToolClick(object? sender, RoutedEventArgs e)
    {
        if (_viewportHost is null || _wiredViewModel is null)
        {
            return;
        }

        var isActive = _sketchRotateToolButton?.IsChecked == true;
        try
        {
            if (isActive)
            {
                await _wiredViewModel.SelectSketchToolAsync("Rotate");
                ApplySketchToolHint("Rotate");
            }
            else
            {
                ApplySketchToolHint("Rectangle");
            }

            await _viewportHost.SetSketchRotateToolActiveAsync(isActive);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchRotateToolClick), ex);
        }

        e.Handled = true;
    }

    private void OnFaceClicked(object? sender, ViewportFaceClickedEventArgs e)
    {
        _wiredViewModel?.HandleFaceClicked(e);
    }

    private async void OnBoxSelectCompleted(object? sender, ViewportBoxSelectEventArgs e)
    {
        if (_viewportHost is null || _wiredViewModel is null)
        {
            return;
        }

        try
        {
            _wiredViewModel.HandleBoxSelect(e.Ids, e.Append);
            await _viewportHost.SetBoxSelectionAsync(e.Ids, e.Append);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnBoxSelectCompleted), ex);
        }
    }

    private async void OnViewportSelectionCleared(object? sender, EventArgs e)
    {
        if (_viewportHost is null || _wiredViewModel is null)
            return;

        try
        {
            _wiredViewModel.HandleSelectionCleared();
            await _viewportHost.SetBoxSelectionAsync([], false);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnViewportSelectionCleared), ex);
        }
    }

    private async void OnGridToggleClick(object? sender, RoutedEventArgs e)
    {
        if (_viewportHost is null)
        {
            return;
        }

        var isVisible = _gridToggleButton?.IsChecked == true;
        try
        {
            await _viewportHost.SetGridVisibleAsync(isVisible);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnGridToggleClick), ex);
        }

        e.Handled = true;
    }

    private async void OnSectionViewToggleClick(object? sender, RoutedEventArgs e)
    {
        if (_viewportHost is null) return;
        var enabled = _sectionViewToggle?.IsChecked == true;
        try
        {
            var axis = (_sectionAxisCombo?.SelectedItem as Avalonia.Controls.ComboBoxItem)?.Content?.ToString() ?? "Z";
            var offset = (double)(_sectionOffsetSpin?.Value ?? 0m);
            await _viewportHost.SetSectionPlaneAsync(enabled, axis, offset);
        }
        catch (Exception ex) { LogHandlerFailure(nameof(OnSectionViewToggleClick), ex); }
        e.Handled = true;
    }

    private async void OnSectionOffsetChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
    {
        if (_viewportHost is null || _sectionViewToggle?.IsChecked != true) return;
        try
        {
            var axis = (_sectionAxisCombo?.SelectedItem as Avalonia.Controls.ComboBoxItem)?.Content?.ToString() ?? "Z";
            var offset = (double)(e.NewValue ?? 0m);
            await _viewportHost.SetSectionPlaneAsync(true, axis, offset);
        }
        catch (Exception ex) { LogHandlerFailure(nameof(OnSectionOffsetChanged), ex); }
    }

    private async void OnMeasureToolToggleClick(object? sender, RoutedEventArgs e)
    {
        if (_viewportHost is null) return;
        var active = _measureToolToggle?.IsChecked == true;
        try
        {
            await _viewportHost.SetMeasureModeAsync(active);
        }
        catch (Exception ex) { LogHandlerFailure(nameof(OnMeasureToolToggleClick), ex); }
        e.Handled = true;
    }

    private void OnMeasureResultReceived(object? sender, Controls.ViewportMeasureResultEventArgs e)
    {
        // Distance is already shown in the viewport HUD via setHudText; nothing extra needed here.
        _ = e.Distance; // suppress unused-variable warning
    }

    private void OnMeasureCancelled(object? sender, EventArgs e)
    {
        // Esc in the viewport exited measure mode — untoggle the toolbar button.
        if (_measureToolToggle is not null)
            _measureToolToggle.IsChecked = false;
    }

    /// <summary>Updates the viewport HUD with a context hint for the active sketch tool.</summary>
    private void ApplySketchToolHint(string toolName)
    {
        if (_viewportHost is null)
            return;
        var hint = SketchToolHints.TryGetValue(toolName, out var h) ? h : string.Empty;
        _ = _viewportHost.SetSketchToolHintAsync(hint);
    }

    private async void OnMoveToolClick(object? sender, RoutedEventArgs e)
    {
        if (_viewportHost is null)
        {
            return;
        }

        var isActive = _moveToolButton?.IsChecked == true;
        try
        {
            await _viewportHost.SetMoveToolActiveAsync(isActive);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnMoveToolClick), ex);
        }

        e.Handled = true;
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Developer Mode: F9 = validate exact kernel (same handler as the Developer menu).
        if (!e.Handled && e.Key == Key.F9 && ExactMigration.IsDeveloperMode)
        {
            OnDevValidateKernelClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (!e.Handled && e.KeyModifiers == KeyModifiers.Control && e.Key == Key.N)
        {
            OnNewProjectClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (!e.Handled && e.KeyModifiers == KeyModifiers.Control && e.Key == Key.E)
        {
            OnExportClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (!e.Handled && e.KeyModifiers == KeyModifiers.Control && e.Key == Key.O)
        {
            OnOpenProjectClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (!e.Handled && e.KeyModifiers == KeyModifiers.Control && e.Key == Key.S)
        {
            OnSaveProjectClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (!e.Handled && e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Z)
        {
            _wiredViewModel?.UndoAsync();
            e.Handled = true;
            return;
        }

        if (!e.Handled && e.KeyModifiers == KeyModifiers.Control && e.Key == Key.D)
        {
            _ = _wiredViewModel?.DuplicateSelectedBodyAsync();
            e.Handled = true;
            return;
        }

        if (!e.Handled && e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Y)
        {
            _wiredViewModel?.RedoAsync();
            e.Handled = true;
            return;
        }

        if (!e.Handled && e.KeyModifiers == KeyModifiers.Control && e.Key == Key.K)
        {
            OnCommandPaletteClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Key.Escape && _wiredViewModel?.IsSelectingSketchPlane == true)
        {
            _wiredViewModel.CancelSketchPlaneSelection();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _wiredViewModel?.IsSketchMode == true)
        {
            _ = _wiredViewModel.CancelSketchStepAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.M && _wiredViewModel?.IsSketchMode == false)
        {
            if (_moveToolButton is null || _viewportHost is null)
            {
                return;
            }

            var nextActive = _moveToolButton.IsChecked != true;
            _moveToolButton.IsChecked = nextActive;
            _ = _viewportHost.SetMoveToolActiveAsync(nextActive);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.G && _wiredViewModel?.IsSketchMode == true)
        {
            if (_gridToggleButton is null || _viewportHost is null)
            {
                return;
            }

            var nextVisible = _gridToggleButton.IsChecked != true;
            _gridToggleButton.IsChecked = nextVisible;
            _ = _viewportHost.SetGridVisibleAsync(nextVisible);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Q && _wiredViewModel?.IsSketchMode == true)
        {
            OnToggleConstructionModeClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.V && _wiredViewModel?.IsSketchMode == false)
        {
            var selectedBodyId = _wiredViewModel?.CurrentSelectedBodyId ?? Guid.Empty;
            if (selectedBodyId != Guid.Empty)
            {
                _ = _wiredViewModel!.ToggleBodyVisibilityAsync(selectedBodyId);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.H && _wiredViewModel?.IsSketchMode == false)
        {
            if (_wiredViewModel?.CanEdgeTools == true)
            {
                OnHoleClick(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        // Sketch tool shortcuts — only active while in sketch mode, no modifier
        if (_wiredViewModel?.IsSketchMode == true && e.KeyModifiers == KeyModifiers.None)
        {
            string? sketchTool = e.Key switch
            {
                Key.L => "Line",
                Key.R => "Rectangle",
                Key.C => "Circle",
                Key.A => "Arc",
                Key.P => "Point",
                Key.D => "LinearDimension",
                Key.I => "RadiusDimension",
                _ => null
            };

            if (sketchTool is not null)
            {
                _ = _wiredViewModel.SelectSketchToolAsync(sketchTool);
                ApplySketchToolHint(sketchTool);
                e.Handled = true;
                return;
            }
        }

        // S — Start/Finish Sketch (context-sensitive like the primary sketch button)
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.None && _wiredViewModel is not null)
        {
            OnSketchPrimaryClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // E — Extrude (3D mode only, when profile tools available)
        if (e.Key == Key.E && e.KeyModifiers == KeyModifiers.None
            && _wiredViewModel?.IsSketchMode == false
            && _wiredViewModel?.CanProfileTools == true)
        {
            OnExtrudeClick(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // F — Zoom to fit / Focus selection
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None && _viewportHost is not null)
        {
            _ = _viewportHost.FocusSelectionAsync();
            e.Handled = true;
            return;
        }

        // Space — repeat last command (apply pending or re-run last assistant command)
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None && _wiredViewModel is not null)
        {
            if (_wiredViewModel.HasPendingCommand)
            {
                _ = _wiredViewModel.ApplyPendingCommandAsync();
                e.Handled = true;
            }
        }
    }

    private async void OnCommandPaletteClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            var dialog = new CommandPaletteDialog(_wiredViewModel.ExecuteLocalCommandTextAsync);
            await dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnCommandPaletteClick), ex);
        }

        e.Handled = true;
    }

    private async void OnPrimitiveMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        string? primitiveKind = null;
        switch (sender)
        {
            case AMenuItem menuItem:
                primitiveKind = menuItem.Tag?.ToString();
                break;
            case AButton button:
                primitiveKind = button.Tag?.ToString();
                break;
        }

        if (string.IsNullOrWhiteSpace(primitiveKind))
        {
            return;
        }

        try
        {
            await _wiredViewModel.CreatePrimitiveAsync(primitiveKind);
            _wiredViewModel.RecordRecentPrimitive(primitiveKind);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnPrimitiveMenuItemClick), ex);
        }

        e.Handled = true;
    }

    private async void OnSketchPrimaryClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (_wiredViewModel.CanFinishSketch)
            {
                await _wiredViewModel.FinishSketchAsync();
                await TryAutoExtrudeAfterSketchAsync();
            }
            else
            {
                await StartSketchFromCurrentSelectionOrPlanePick();
            }
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchPrimaryClick), ex);
        }

        e.Handled = true;
    }

    private async Task StartSketchFromCurrentSelectionOrPlanePick()
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        if (_wiredViewModel.CanStartSketchFromCurrentSelection)
        {
            await _wiredViewModel.StartSketchAsync();
            ApplySketchToolHint("Rectangle");
            return;
        }

        _wiredViewModel.BeginSketchPlaneSelection();
        var planes = _wiredViewModel.GetAvailablePlanes();
        if (planes.Count == 0)
        {
            _wiredViewModel.CancelSketchPlaneSelection();
            await ShowCommandBlockedDialogAsync(
                "Start Sketch",
                "No sketch base is available yet.",
                "Show at least one visible reference plane or select a planar face.",
                "Use Top, Front, or Right from the tree, or create a datum plane first.");
            return;
        }

        var dialog = new SelectPlaneDialog(planes);
        var selectedPlane = await ShowAnchoredDialogAsync<SelectPlaneDialogResult?>(dialog);
        if (selectedPlane is not null)
        {
            await _wiredViewModel.StartSketchOnPlaneAsync(selectedPlane.PlaneId, selectedPlane.PlaneName);
            ApplySketchToolHint("Rectangle");
            return;
        }

        ReturnFocusToViewport();
        ApplySketchToolHint("Rectangle");
    }

    private async void OnSketchStartClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await StartSketchFromCurrentSelectionOrPlanePick();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchStartClick), ex);
        }

        e.Handled = true;
    }

    private async void OnWorkspace3DClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (_wiredViewModel.IsSelectingSketchPlane)
            {
                _wiredViewModel.CancelSketchPlaneSelection();
            }
            else if (_wiredViewModel.IsSketchMode)
            {
                await _wiredViewModel.FinishSketchAsync();
            }

            _wiredViewModel.SelectWorkspace("PartStudio");
            ReturnFocusToViewport();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnWorkspace3DClick), ex);
        }

        e.Handled = true;
    }

    private async void OnWorkspaceSketchClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (_wiredViewModel.IsSketchMode)
            {
                ReturnFocusToViewport();
            }
            else
            {
                await StartSketchFromCurrentSelectionOrPlanePick();
            }

            _wiredViewModel.SelectWorkspace("Sketch");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnWorkspaceSketchClick), ex);
        }

        e.Handled = true;
    }

    private async void OnApplyHorizontalConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySketchConstraintAsync("Horizontal");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyHorizontalConstraintClick), ex);
        }
    }

    private async void OnApplyVerticalConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySketchConstraintAsync("Vertical");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyVerticalConstraintClick), ex);
        }
    }

    private async void OnApplyCoincidentConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySketchConstraintAsync("Coincident");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyCoincidentConstraintClick), ex);
        }
    }

    private async void OnApplyEqualConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySketchConstraintAsync("Equal");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyEqualConstraintClick), ex);
        }
    }

    private async void OnApplyFixedConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySketchConstraintAsync("Fixed");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyFixedConstraintClick), ex);
        }
    }

    private async void OnApplyTangentConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySketchConstraintAsync("Tangent");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyTangentConstraintClick), ex);
        }
    }

    private async void OnApplyParallelConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySketchConstraintAsync("Parallel");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyParallelConstraintClick), ex);
        }
    }

    private async void OnApplyPerpendicularConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySketchConstraintAsync("Perpendicular");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyPerpendicularConstraintClick), ex);
        }
    }

    private async void OnApplyConcentricConstraintClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySketchConstraintAsync("Concentric");
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyConcentricConstraintClick), ex);
        }
    }

    private async void OnToggleConstructionModeClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ToggleConstructionModeAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnToggleConstructionModeClick), ex);
        }
    }

    private async void OnSketchDimensionValueChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not ANumericUpDown nud)
        {
            return;
        }

        if (nud.Tag is not SketchDimensionDisplayItem item || !item.IsEditable)
        {
            return;
        }

        var newValue = (double)(e.NewValue ?? 0m);
        if (newValue <= 0 || Math.Abs(newValue - item.Value) < 1e-6)
        {
            return;
        }

        try
        {
            await _wiredViewModel.EditSketchDimensionAsync(item.EntityId, item.Key, newValue);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchDimensionValueChanged), ex);
        }
    }

    private async void OnSketchFinishClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.FinishSketchAsync();
            await TryAutoExtrudeAfterSketchAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchFinishClick), ex);
        }

        e.Handled = true;
    }

    private async Task TryAutoExtrudeAfterSketchAsync()
    {
        if (_wiredViewModel is null || !_wiredViewModel.CanProfileTools)
        {
            return;
        }

        var result = await ShowExtrudeFeatureDialogAsync();
        ReturnFocusToViewport();
        if (result is null)
        {
            return;
        }

        await _wiredViewModel.ExtrudeSelectedSketchAsync(result.Value.Distance, result.Value.Operation, result.Value.TargetBodyId);
    }

    private async void OnSketchCancelClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (_wiredViewModel.IsSelectingSketchPlane)
            {
                _wiredViewModel.CancelSketchPlaneSelection();
                return;
            }

            await _wiredViewModel.CancelSketchAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchCancelClick), ex);
        }

        e.Handled = true;
    }

    private async void OnExtrudeClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanProfileTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Extrude",
                    "Select a closed normal sketch profile first.",
                    "Finish the active sketch if it is still open.",
                    "Use a closed rectangle, circle, or other valid loop.",
                    "Construction geometry and open chains cannot be extruded.");
                e.Handled = true;
                return;
            }

            var result = await ShowExtrudeFeatureDialogAsync();
            ReturnFocusToViewport();
            if (result is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.ExtrudeSelectedSketchAsync(result.Value.Distance, result.Value.Operation, result.Value.TargetBodyId);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnExtrudeClick), ex);
        }

        e.Handled = true;
    }

    private async void OnRevolveClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanProfileTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Revolve",
                    "Select a closed normal sketch profile before revolving.",
                    "Finish the current sketch if needed.",
                    "Pick a profile such as a circle or closed loop.",
                    "Then choose the revolve axis and angle in the dialog.");
                e.Handled = true;
                return;
            }

            var result = await ShowRevolveFeatureDialogAsync();
            ReturnFocusToViewport();
            if (result is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.RevolveSelectedSketchAsync(result.Value.Angle, result.Value.Axis);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnRevolveClick), ex);
        }

        e.Handled = true;
    }

    private async void OnSweepClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanProfileTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Sweep",
                    "Sweep needs a valid selected sketch profile in this MVP.",
                    "Finish the active sketch if it is still open.",
                    "Select a closed normal profile first.",
                    "Then set sweep distance and twist in the feature dialog.");
                e.Handled = true;
                return;
            }

            var result = await ShowSweepFeatureDialogAsync();
            ReturnFocusToViewport();
            if (result is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.SweepSelectedSketchAsync(result.Value.Distance, result.Value.TwistDegrees);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSweepClick), ex);
        }

        e.Handled = true;
    }

    private async void OnSketchToolClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        var sketchTool = sender switch
        {
            AMenuItem menuItem => menuItem.Tag?.ToString(),
            InputElement inputElement => inputElement.GetValue(TagProperty)?.ToString(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(sketchTool))
        {
            return;
        }

        try
        {
            if (_sketchTransformToolButton?.IsChecked == true)
            {
                _sketchTransformToolButton.IsChecked = false;
                if (_viewportHost is not null)
                {
                    await _viewportHost.SetSketchTransformToolActiveAsync(false);
                }
            }

            if (_sketchRotateToolButton?.IsChecked == true)
            {
                _sketchRotateToolButton.IsChecked = false;
                if (_viewportHost is not null)
                {
                    await _viewportHost.SetSketchRotateToolActiveAsync(false);
                }
            }

            var toolParam = 0d;
            if (TryBuildSketchToolDialogBody(sketchTool, out var dialogTitle, out var dialogSubtitle, out var dialogBody))
            {
                var confirmed = await ShowToolDialogAsync(dialogTitle, dialogSubtitle, dialogBody);
                ReturnFocusToViewport();
                if (!confirmed)
                {
                    e.Handled = true;
                    return;
                }

                if (dialogBody is ISketchToolOptionsView optView)
                {
                    toolParam = optView.ToolParam;
                }
            }

            await _wiredViewModel.SelectSketchToolAsync(sketchTool, toolParam);
            ApplySketchToolHint(sketchTool);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnSketchToolClick), ex);
        }

        e.Handled = true;
    }

    private async void OnFilletClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanEdgeTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Fillet",
                    "Select a solid body before filleting.",
                    "Choose a visible body in the tree or viewport.",
                    "Then set the edge radius in the dialog.");
                e.Handled = true;
                return;
            }

            var dialog = new FilletFeatureDialog(_wiredViewModel.AssistantSelectionSummary, 2d);
            var result = await ShowAnchoredDialogAsync<FilletFeatureDialogResult?>(dialog);
            if (result is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.FilletSelectedBodyAsync(result.Radius);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnFilletClick), ex);
        }

        e.Handled = true;
    }

    private async void OnShellClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanEdgeTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Shell",
                    "Select a solid body before shelling it.",
                    "Choose a visible body in the viewport or tree.",
                    "Then define the wall thickness in the shell dialog.");
                e.Handled = true;
                return;
            }

            var thickness = await ShowNumericFeatureDialogAsync(
                "Shell",
                "Create a hollow body from the current solid.",
                "Wall Thickness",
                2d,
                0.1d,
                250d,
                "mm");
            ReturnFocusToViewport();
            if (thickness is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.ShellSelectedBodyAsync(thickness.Value);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnShellClick), ex);
        }

        e.Handled = true;
    }

    private async void OnChamferClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanEdgeTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Chamfer",
                    "Select a solid body before applying a chamfer.",
                    "Pick a body in the tree or viewport first.",
                    "Then set the chamfer distance in the dialog.");
                e.Handled = true;
                return;
            }

            var distance = await ShowNumericFeatureDialogAsync(
                "Chamfer",
                "Bevel the selected solid using a single-distance chamfer.",
                "Distance",
                1d,
                0.1d,
                250d,
                "mm");
            ReturnFocusToViewport();
            if (distance is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.ChamferSelectedBodyAsync(distance.Value);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnChamferClick), ex);
        }

        e.Handled = true;
    }

    private async void OnLinearPatternClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanEdgeTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Linear Pattern",
                    "Select a body before creating a linear pattern.",
                    "Patterns duplicate the currently selected body.",
                    "Then set count, spacing, and axis in the dialog.");
                e.Handled = true;
                return;
            }

            var dialog = new LinearPatternDialog(3, 20d, "x");
            var result = await ShowAnchoredDialogAsync<LinearPatternDialogResult?>(dialog);
            if (result is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.LinearPatternSelectedBodyAsync(result.Count, result.Spacing, result.Axis);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnLinearPatternClick), ex);
        }

        e.Handled = true;
    }

    private async void OnCircularPatternClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanEdgeTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Circular Pattern",
                    "Select a body before creating a circular pattern.",
                    "Patterns duplicate the currently selected body.",
                    "Then set count, total angle, and axis in the dialog.");
                e.Handled = true;
                return;
            }

            var dialog = new CircularPatternDialog(4, 360d, "y");
            var result = await ShowAnchoredDialogAsync<CircularPatternDialogResult?>(dialog);
            if (result is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.CircularPatternSelectedBodyAsync(result.Count, result.TotalAngle, result.Axis);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnCircularPatternClick), ex);
        }

        e.Handled = true;
    }

    private async void OnHoleClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanEdgeTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Hole",
                    "Select a solid body before creating a hole feature.",
                    "Choose a visible body in the tree or viewport.",
                    "Then set diameter, offsets, and depth in the dialog.");
                e.Handled = true;
                return;
            }

            var dialog = new HoleFeatureDialog(10d, "ThroughAll", 20d, 0d, 0d);
            dialog.PreviewRequested += (s, args) =>
            {
                var graph = _wiredViewModel.PreviewHole(args.Diameter, args.CenterOffsetX, args.CenterOffsetY, args.DepthKind == "Blind" ? $"Blind:{args.DepthValue}" : "ThroughAll");
                if (graph != null) _exactScene?.DisplayFeatureGraph(graph);
            };
            var result = await ShowAnchoredDialogAsync<HoleFeatureDialogResult?>(dialog);
            if (result == null && _wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph != null) _exactScene?.DisplayFeatureGraph(_wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph);
            if (result is null)
            {
                e.Handled = true;
                return;
            }

            var depthInfo = string.Equals(result.DepthKind, "Blind", StringComparison.OrdinalIgnoreCase)
                ? $"Blind:{result.DepthValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : "ThroughAll";
            await _wiredViewModel.HoleSelectedBodyAsync(
                result.Diameter,
                result.CenterOffsetX,
                result.CenterOffsetY,
                depthInfo);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnHoleClick), ex);
        }

        e.Handled = true;
    }

    private async void OnBooleanUnionClick(object? sender, RoutedEventArgs e)
    {
        await ShowBooleanDialogAsync("Union");
        e.Handled = true;
    }

    private async void OnBooleanSubtractClick(object? sender, RoutedEventArgs e)
    {
        await ShowBooleanDialogAsync("Subtract");
        e.Handled = true;
    }

    private async void OnBooleanIntersectClick(object? sender, RoutedEventArgs e)
    {
        await ShowBooleanDialogAsync("Intersect");
        e.Handled = true;
    }

    private async Task ShowBooleanDialogAsync(string initialOperation)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            var bodies = _wiredViewModel.GetBodyList();
            if (bodies.Count < 2)
            {
                await ShowCommandBlockedDialogAsync(
                    "Boolean Operation",
                    "At least two visible bodies are required.",
                    "Create or generate another body first.",
                    "Then select the two bodies you want to union, subtract, or intersect.");
                return;
            }

            var selectedId = _wiredViewModel.CurrentSelectedBodyId;
            var bodyAId = selectedId != Guid.Empty ? selectedId : bodies[0].Id;
            var bodyBId = bodies.FirstOrDefault(b => b.Id != bodyAId).Id;
            if (bodyBId == Guid.Empty)
            {
                bodyBId = bodies.Count > 1 ? bodies[1].Id : bodies[0].Id;
            }

            var dialog = new BooleanBodyDialog(bodies, bodyAId, bodyBId, initialOperation);
            var result = await ShowAnchoredDialogAsync<BooleanBodyDialogResult?>(dialog);
            if (result is null)
            {
                return;
            }

            await _wiredViewModel.BooleanApplyAsync(result.BodyAId, result.BodyBId, result.Operation);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(ShowBooleanDialogAsync), ex);
        }
    }

    private async void OnMirrorMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (!_wiredViewModel.CanEdgeTools)
            {
                await ShowCommandBlockedDialogAsync(
                    "Mirror",
                    "Select a body before creating a mirror feature.",
                    "Choose a visible body in the viewport or tree.",
                    "Then select the mirror axis in the dialog.");
                e.Handled = true;
                return;
            }

            var dialog = new MirrorFeatureDialog("x");
            var result = await ShowAnchoredDialogAsync<MirrorFeatureDialogResult?>(dialog);
            if (result is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.MirrorSelectedBodyAsync(result.Axis);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnMirrorMenuItemClick), ex);
        }

        e.Handled = true;
    }

    private async void OnCreateDatumPlaneClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null) return;
        try
        {
            var dialog = new DatumPlaneDialog();
            await ShowAnchoredDialogAsync(dialog);
            if (dialog.Confirmed)
            {
                _wiredViewModel.CreateDatumPlane(
                    dialog.ResultSourceKind,
                    dialog.ResultOffset,
                    dialog.ResultPlaneName);
            }
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnCreateDatumPlaneClick), ex);
        }
        e.Handled = true;
    }

    private async Task<double?> ShowNumericFeatureDialogAsync(
        string title,
        string subtitle,
        string valueLabel,
        double initialValue,
        double minimum,
        double maximum,
        string unit)
    {
        var dialog = new NumericFeatureDialog(
            title,
            subtitle,
            valueLabel,
            initialValue,
            minimum,
            maximum,
            unit);
        var result = await ShowAnchoredDialogAsync<string?>(dialog);
        return double.TryParse(result, out var parsed)
            ? parsed
            : null;
    }

    private async Task<(double Distance, CadExtrudeOperation Operation, Guid TargetBodyId)?> ShowExtrudeFeatureDialogAsync()
    {
        if (_wiredViewModel is null)
        {
            return null;
        }

        var bodies = _wiredViewModel.GetExtrudeTargetBodyList();
        var preferredBodyId = _wiredViewModel.ResolvePreferredExtrudeTargetBodyId(bodies);
        var dialog = new ExtrudeFeatureDialog(
            _wiredViewModel.SelectedProfileSummary,
            _wiredViewModel.SelectedProfilePlaneSummary,
            8d,
            bodies,
            preferredBodyId);
        dialog.PreviewRequested += (s, args) =>
        {
            var graph = _wiredViewModel.PreviewExtrude(args.Distance, args.Operation, args.TargetBodyId);
            if (graph != null) _exactScene?.DisplayFeatureGraph(graph);
        };
        var result = await ShowAnchoredDialogAsync<ExtrudeFeatureDialogResult?>(dialog);
        if (result == null && _wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph != null) _exactScene?.DisplayFeatureGraph(_wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph);
        if (result is null)
        {
            return null;
        }

        return (result.Distance, result.Operation, result.TargetBodyId);
    }

    private async Task<(double Angle, string Axis)?> ShowRevolveFeatureDialogAsync()
    {
        if (_wiredViewModel is null)
        {
            return null;
        }

        var dialog = new RevolveFeatureDialog(
            _wiredViewModel.SelectedProfileSummary,
            _wiredViewModel.SelectedProfilePlaneSummary,
            360d,
            "y");
        var result = await ShowAnchoredDialogAsync<RevolveFeatureDialogResult?>(dialog);
        if (result is null)
        {
            return null;
        }

        return (result.Angle, result.Axis);
    }

    private async void OnLoftClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            var result = await ShowLoftFeatureDialogAsync();
            ReturnFocusToViewport();
            if (result is null)
            {
                e.Handled = true;
                return;
            }

            await _wiredViewModel.LoftFromProfilesAsync(result.ProfileAId, result.ProfileBId, result.Distance);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnLoftClick), ex);
        }

        e.Handled = true;
    }

    private async Task<LoftFeatureDialogResult?> ShowLoftFeatureDialogAsync()
    {
        if (_wiredViewModel is null)
        {
            return null;
        }

        var sketches = _wiredViewModel.GetClosedSketchProfiles();
        if (sketches.Count < 2)
        {
            await ShowCommandBlockedDialogAsync(
                "Loft",
                "Loft needs at least two closed sketch profiles.",
                "Create or finish two valid sketch profiles first.",
                "Then choose the source and target profiles in the loft dialog.");
            return null;
        }

        var dialog = new LoftFeatureDialog(sketches, Guid.Empty, Guid.Empty, 20d);
        return await ShowAnchoredDialogAsync<LoftFeatureDialogResult?>(dialog);
    }

    private async Task<(double Distance, double TwistDegrees)?> ShowSweepFeatureDialogAsync()
    {
        if (_wiredViewModel is null)
        {
            return null;
        }

        var dialog = new SweepFeatureDialog(
            _wiredViewModel.SelectedProfileSummary,
            _wiredViewModel.SelectedProfilePlaneSummary,
            20d,
            0d);
        var result = await ShowAnchoredDialogAsync<SweepFeatureDialogResult?>(dialog);
        if (result is null)
        {
            return null;
        }

        return (result.Distance, result.TwistDegrees);
    }

    private static bool TryBuildSketchToolDialogBody(
        string sketchTool,
        out string title,
        out string subtitle,
        out Avalonia.Controls.Control body)
    {
        switch (sketchTool.Trim().ToLowerInvariant())
        {
            case "line":
                title = "Line";
                subtitle = "Place a chained line by clicking the start point, then each next endpoint.";
                body = new LineSketchToolOptionsView();
                return true;
            case "rectangle":
                title = "Rectangle";
                subtitle = "Click one corner and drag to the opposite corner to place a closed rectangle.";
                body = new RectangleSketchToolOptionsView();
                return true;
            case "circle":
                title = "Circle";
                subtitle = "Click the center point, then click or drag to set the radius.";
                body = new CircleSketchToolOptionsView();
                return true;
            case "arc":
                title = "Arc";
                subtitle = "Place an arc by specifying three points on the curve.";
                body = new ArcSketchToolOptionsView();
                return true;
            case "point":
                title = "Point";
                subtitle = "Place a construction point by clicking the viewport or entering coordinates.";
                body = new PointSketchToolOptionsView();
                return true;
            case "polygon":
                title = "Polygon";
                subtitle = "Click center, then click or drag to set the radius. Sides parameter below.";
                body = new PolygonSketchToolOptionsView();
                return true;
            case "offset":
                title = "Offset";
                subtitle = "Click in the viewport to generate an offset copy of all sketch lines and circles.";
                body = new OffsetSketchToolOptionsView();
                return true;
            case "fillet2d":
                title = "2D Fillet";
                subtitle = "Click near the corner of two intersecting lines to insert a tangent arc.";
                body = new Fillet2dSketchToolOptionsView();
                return true;
            default:
                title = string.Empty;
                subtitle = string.Empty;
                body = null!;
                return false;
        }
    }

    private async Task<bool> ShowToolDialogAsync(
        string title,
        string subtitle,
        Avalonia.Controls.Control body)
    {
        var dialog = new ToolDialogWindow(title, subtitle, body);
        var result = await ShowAnchoredDialogAsync<bool?>(dialog, returnFocus: false);
        return result == true;
    }

    private async Task ShowCommandBlockedDialogAsync(string commandName, string subtitle, params string[] steps)
    {
        var body = new StackPanel { Spacing = 10 };

        if (steps.Length > 0)
        {
            body.Children.Add(new TextBlock
            {
                Text = "What to do next",
                Classes = { "FeatureDialogFieldLabel" }
            });

            foreach (var step in steps.Where(step => !string.IsNullOrWhiteSpace(step)))
            {
                body.Children.Add(new Border
                {
                    Classes = { "FeatureDialogSectionCard" },
                    Padding = new Thickness(10, 8),
                    Child = new TextBlock
                    {
                        Text = "• " + step.Trim(),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Classes = { "FeatureDialogHelpText" }
                    }
                });
            }
        }

        var dialog = new ToolDialogWindow(commandName, subtitle, body)
        {
            ConfirmButtonText = "OK",
            ShowCancelButton = false
        };

        await ShowAnchoredDialogAsync(dialog);
    }

    private async Task<TResult> ShowAnchoredDialogAsync<TResult>(Window dialog, bool returnFocus = true)
    {
        AnchorDialogToViewport(dialog);
        var result = await dialog.ShowDialog<TResult>(this);
        if (returnFocus)
        {
            ReturnFocusToViewport();
        }

        return result;
    }

    private async Task ShowAnchoredDialogAsync(Window dialog, bool returnFocus = true)
    {
        AnchorDialogToViewport(dialog);
        await dialog.ShowDialog(this);
        if (returnFocus)
        {
            ReturnFocusToViewport();
        }
    }

    private void AnchorDialogToViewport(Window dialog)
    {
        try
        {
            Avalonia.Visual? anchorVisual = _viewportHost;
            anchorVisual ??= this;
            var origin = anchorVisual.PointToScreen(new Avalonia.Point(0, 0));
            var width = _viewportHost?.Bounds.Width > 0 ? _viewportHost.Bounds.Width : Bounds.Width;
            var dialogWidth = dialog.Width > 0 ? dialog.Width : (dialog.MinWidth > 0 ? dialog.MinWidth : 440);
            var x = origin.X + Math.Max(16, (int)Math.Round(width - dialogWidth - 20));
            var y = origin.Y + 20;

            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Position = new PixelPoint(x, y);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("MainWindow", "Failed to anchor feature dialog to viewport. Falling back to default placement.", ex);
        }
    }

    private void ReturnFocusToViewport()
    {
        _viewportHost?.Focus();
    }

    private async void OnFeatureTreeKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (_wiredViewModel is null || _featureTree is null)
        {
            return;
        }

        var node = _featureTree.SelectedItem as FeatureNodeViewModel;

        if (e.Key == Key.Enter && node is not null)
        {
            try
            {
                // Simulate double-tap behavior
                await OnFeatureTreeActivateNodeAsync(node);
            }
            catch (Exception ex)
            {
                LogHandlerFailure(nameof(OnFeatureTreeKeyDown), ex);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && node?.CanDeleteModelItem == true)
        {
            try
            {
                await _wiredViewModel.SelectTreeNodeAsync(node);
                await _wiredViewModel.HandleViewportDeleteAsync();
            }
            catch (Exception ex)
            {
                LogHandlerFailure(nameof(OnFeatureTreeKeyDown), ex);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 && node?.CanRenameItem == true)
        {
            try
            {
                var dialog = new RenameDialog(node.Name);
                var newName = await dialog.ShowDialog<string?>(this);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    await _wiredViewModel.RenameBodyAsync(node.EntityId, newName);
                }
            }
            catch (Exception ex)
            {
                LogHandlerFailure(nameof(OnFeatureTreeKeyDown), ex);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            _viewportHost?.Focus();
            e.Handled = true;
        }
    }

    private async Task OnFeatureTreeActivateNodeAsync(FeatureNodeViewModel node)
    {
        if (_wiredViewModel is null || node.EntityId == Guid.Empty)
        {
            return;
        }

        if (node.EntityKind == CadEntityKind.Sketch)
        {
            await _wiredViewModel.EditSketchAsync(node.EntityId);
            return;
        }

        if (node.EntityKind == CadEntityKind.ReferencePlane)
        {
            await _wiredViewModel.SelectTreeNodeAsync(node);
            await _wiredViewModel.StartSketchAsync();
            return;
        }

        // Fall back to parameter edit dialog
        await ShowParameterEditDialogAsync(node);
    }

    private async void OnAssistantSendClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.SendAssistantAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnAssistantSendClick), ex);
        }

        e.Handled = true;
    }

    private void OnAssistantRefreshClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.RefreshAssistantConfiguration();
        e.Handled = true;
    }

    private void OnAssistantClearClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.ClearAssistantHistory();
        e.Handled = true;
    }

    private void OnPartStudioTabClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not AButton button || button.Tag is not string name)
        {
            return;
        }
        _wiredViewModel.ActivatePartStudio(name);
        e.Handled = true;
    }

    private void OnWorkspaceTabClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not AButton button || button.Tag is not string kind)
        {
            return;
        }

        _wiredViewModel.SelectWorkspace(kind);
        e.Handled = true;
    }

    private async void OnPartStudioTabRename(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not AButton button || button.Tag is not string oldName)
        {
            return;
        }
        try
        {
            var dialog = new RenameDialog(oldName);
            var newName = await ShowAnchoredDialogAsync<string?>(dialog);
            if (!string.IsNullOrWhiteSpace(newName) && newName != oldName)
            {
                _wiredViewModel.RenamePartStudioTab(oldName, newName);
            }
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnPartStudioTabRename), ex);
        }
        e.Handled = true;
    }

    private void OnPartStudioTabClose(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not AButton button || button.Tag is not string name)
        {
            return;
        }
        _wiredViewModel.DeletePartStudioTab(name);
        e.Handled = true;
    }

    private void OnAddPartStudioClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.AddPartStudioTab();
        e.Handled = true;
    }

    private void OnTemplateCatalogClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not AButton button || button.Tag is not string templateId)
        {
            return;
        }

        _wiredViewModel.OpenFeaturedTemplate(templateId);
        e.Handled = true;
    }

    private void OnProjectPaneClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.SelectLeftPaneSection("Project");
        e.Handled = true;
    }

    private void OnTemplatesPaneClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.SelectLeftPaneSection("Templates");
        e.Handled = true;
    }

    private void OnLibraryPaneClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.SelectLeftPaneSection("Library");
        e.Handled = true;
    }

    private async void OnRecentDocumentLibraryClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not AButton button || button.Tag is not string projectPath)
        {
            return;
        }

        try
        {
            if (!await EnsureSafeToReplaceCurrentProjectAsync("Open recent project",
                    "You have unsaved changes. Save the current document before opening a recent project?"))
            {
                return;
            }

            await OpenProjectWithGuardAsync(projectPath);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnRecentDocumentLibraryClick), ex);
        }

        e.Handled = true;
    }

    private void OnLibraryTemplateOpenClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not AButton button || button.Tag is not string templateId)
        {
            return;
        }

        _wiredViewModel.SelectMakerTemplate(templateId);
        _wiredViewModel.SelectWorkspace("Templates");
        e.Handled = true;
    }

    private async void OnApplyTemplateClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySelectedTemplateAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyTemplateClick), ex);
        }

        e.Handled = true;
    }

    private void OnApplyTemplatePresetClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not AButton button || button.Tag is not string presetName)
        {
            return;
        }

        _wiredViewModel.ApplyTemplatePreset(presetName);
        e.Handled = true;
    }

    private void OnOpenAssistantWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.SelectWorkspace("Assistant");
        e.Handled = true;
    }

    private void OnOpenTemplatesWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.SelectWorkspace("Templates");
        e.Handled = true;
    }

    private void OnOpenPartStudioWorkspaceClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.SelectWorkspace("PartStudio");
        e.Handled = true;
    }

    private void OnPrepareWorkspaceMenuClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.SelectWorkspace("Prepare");
        e.Handled = true;
    }

    private void OnDismissStartupPageClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.DismissStartupPage();
        e.Handled = true;
    }

    private void OnStartupTemplateClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not Avalonia.Controls.Button button || button.Tag is not string templateId)
        {
            return;
        }

        _wiredViewModel.OpenFeaturedTemplate(templateId);
        e.Handled = true;
    }

    private void OnAssistantSuggestionClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not Avalonia.Controls.Button button || button.Tag is not string prompt)
        {
            return;
        }

        _wiredViewModel.PrimeAssistantPrompt(prompt);
        AssistantInputBox?.Focus();
        e.Handled = true;
    }

    private async void OnRestoreAutosaveClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || !AutosaveService.HasAutosave)
        {
            return;
        }

        if (!await EnsureSafeToReplaceCurrentProjectAsync("Restore autosave",
                "You have unsaved changes. Save the current document before restoring the autosave?"))
        {
            return;
        }

        try
        {
            if (_wiredViewModel.OpenProject(AutosaveService.AutosavePath))
            {
                _wiredViewModel.UpdateRecoveryStatus(false, null);
                _wiredViewModel.ShowNotification("Autosave restored into the current studio.", NotificationSeverity.Info);
            }
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnRestoreAutosaveClick), ex);
        }

        e.Handled = true;
    }

    private async void OnApplyTemplateAndPrepareClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplySelectedTemplateAndOpenPrepareAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnApplyTemplateAndPrepareClick), ex);
        }

        e.Handled = true;
    }

    private void OnTemplateCopilotClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        _wiredViewModel.PrimeSelectedTemplateForAssistant();
        AssistantInputBox?.Focus();
        e.Handled = true;
    }

    private void OnPrepareCopilotClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        _wiredViewModel.PrimePrepareForAssistant();
        AssistantInputBox?.Focus();
        e.Handled = true;
    }

    private void OnDiscardAutosaveClick(object? sender, RoutedEventArgs e)
    {
        AutosaveService.DeleteAutosave();
        if (_wiredViewModel is not null)
        {
            _wiredViewModel.UpdateRecoveryStatus(false, null);
            _wiredViewModel.ShowNotification("Autosave discarded.", NotificationSeverity.Info);
        }
        e.Handled = true;
    }

    private async void OnQuickDesignUseCaseClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not Avalonia.Controls.Button button || button.Tag is not string useCase)
        {
            return;
        }

        if (!await EnsureSafeToReplaceCurrentProjectAsync("New quick design",
                "You have unsaved changes. Save the current document before creating a quick design?"))
        {
            return;
        }

        try
        {
            await _wiredViewModel.CreateQuickDesignAsync(useCase);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnQuickDesignUseCaseClick), ex);
        }

        e.Handled = true;
    }

    private async void OnOpenSampleTemplateClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        if (!await EnsureSafeToReplaceCurrentProjectAsync("Open sample",
                "You have unsaved changes. Save the current document before opening the sample project?"))
        {
            return;
        }

        try
        {
            await _wiredViewModel.CreateSampleProjectAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnOpenSampleTemplateClick), ex);
        }

        e.Handled = true;
    }

    private void OnOpenSampleRecipeClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.PrepareRecipeFromLibrary("plate-with-hole", "Sample recipe");
        e.Handled = true;
    }

    private async void OnRunSampleRecipeClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            if (_wiredViewModel.PrepareRecipeFromLibrary("plate-with-hole", "Empty Part Studio"))
            {
                await _wiredViewModel.RunActiveRecipeAsync();
            }
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnRunSampleRecipeClick), ex);
        }

        e.Handled = true;
    }

    private async void OnRunActiveRecipeClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.RunActiveRecipeAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnRunActiveRecipeClick), ex);
        }

        e.Handled = true;
    }

    private async void OnRunAclScriptClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.RunAclScriptAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnRunAclScriptClick), ex);
        }

        e.Handled = true;
    }

    private async void OnAiSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        await ShowAiSettingsDialogAsync();
        e.Handled = true;
    }

    private async Task ShowAiSettingsDialogAsync()
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        var providerBox = new Avalonia.Controls.ComboBox
        {
            ItemsSource = _wiredViewModel.AssistantProviders,
            SelectedItem = _wiredViewModel.SelectedAssistantProvider,
            MinWidth = 180,
            Classes = { "FeatureDialogInput" }
        };
        var modelBox = new ATextBox
        {
            Text = _wiredViewModel.SelectedAssistantModel,
            Watermark = "Model name, for example deepseek-chat",
            MinWidth = 240,
            Classes = { "FeatureDialogInput" }
        };
        var baseUrlBox = new ATextBox
        {
            Text = _wiredViewModel.SelectedAssistantBaseUrl,
            Watermark = "Optional base URL (blank uses provider default)",
            MinWidth = 380,
            Classes = { "FeatureDialogInput" }
        };
        var keyBox = new ATextBox
        {
            Text = _wiredViewModel.AssistantKeyInput,
            PasswordChar = '*',
            Watermark = "Session API key (not saved)",
            MinWidth = 380,
            Classes = { "FeatureDialogInput" }
        };
        var statusText = new Avalonia.Controls.TextBlock
        {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Classes = { "FeatureDialogHelpText" }
        };
        var kernelBox = new Avalonia.Controls.CheckBox
        {
            Content = "Use Experimental PicoGK Kernel (CSG)",
            IsChecked = _wiredViewModel.UseExperimentalGeometryKernel,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };

        void RefreshPreview()
        {
            var provider = providerBox.SelectedItem as string ?? _wiredViewModel.SelectedAssistantProvider;
            statusText.Text = _wiredViewModel.PreviewAssistantConfiguration(
                provider,
                modelBox.Text ?? string.Empty,
                baseUrlBox.Text ?? string.Empty,
                keyBox.Text ?? string.Empty);
        }

        providerBox.SelectionChanged += (_, _) =>
        {
            if (providerBox.SelectedItem is string provider)
            {
                modelBox.Text = _wiredViewModel.GetDefaultAssistantModelName(provider);
                if (string.Equals(provider, "Local CAD Only", StringComparison.Ordinal))
                {
                    baseUrlBox.Text = string.Empty;
                }
            }

            RefreshPreview();
        };
        modelBox.TextChanged += (_, _) => RefreshPreview();
        baseUrlBox.TextChanged += (_, _) => RefreshPreview();
        keyBox.TextChanged += (_, _) => RefreshPreview();

        var saveButton = new Avalonia.Controls.Button
        {
            Content = "Apply",
            Classes = { "FeatureDialogButtonPrimary" },
            MinWidth = 90
        };
        var cancelButton = new Avalonia.Controls.Button
        {
            Content = "Close",
            Classes = { "FeatureDialogButtonSecondary" },
            MinWidth = 90
        };

        var dialog = new Avalonia.Controls.Window
        {
            Title = "AI Settings",
            Width = 520,
            Height = 470,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            Classes = { "FeatureDialogWindow" },
            Content = new Border
            {
                Classes = { "FeatureDialogPanel" },
                Child = new StackPanel
                {
                    Margin = new Avalonia.Thickness(18),
                    Spacing = 12,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock
                        {
                            Text = "AI Settings",
                            Classes = { "FeatureDialogTitle" }
                        },
                        new Avalonia.Controls.TextBlock
                        {
                            Text = "Configure a real assistant provider for design help, ACL drafting, and feature explanations. API keys remain local.",
                            Classes = { "FeatureDialogSubtitle" },
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Margin = new Avalonia.Thickness(0, 6, 0, 0)
                        },
                        new Border
                        {
                            Classes = { "FeatureDialogSectionCard" },
                            Padding = new Avalonia.Thickness(12),
                            Child = new StackPanel
                            {
                                Spacing = 10,
                                Children =
                                {
                                    new Avalonia.Controls.TextBlock
                                    {
                                        Text = "Provider and model",
                                        Classes = { "FeatureDialogFieldLabel" }
                                    },
                                    new Avalonia.Controls.StackPanel
                                    {
                                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                                        Spacing = 8,
                                        Children = { providerBox, modelBox }
                                    },
                                    new Avalonia.Controls.TextBlock
                                    {
                                        Text = "Optional base URL",
                                        Classes = { "FeatureDialogFieldLabel" }
                                    },
                                    baseUrlBox,
                                    new Avalonia.Controls.TextBlock
                                    {
                                        Text = "Session API key",
                                        Classes = { "FeatureDialogFieldLabel" }
                                    },
                                    keyBox,
                                    kernelBox,
                                    statusText,
                                    new Avalonia.Controls.TextBlock
                                    {
                                        Text = "Provider, model, and base URL are stored locally. Environment variables are preferred for long-term setup; pasted keys are session-only.",
                                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                        Classes = { "FeatureDialogHelpText" }
                                    }
                                }
                            }
                        },
                        new Avalonia.Controls.StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { cancelButton, saveButton }
                        }
                    }
                }
            }
        };

        saveButton.Click += (_, _) =>
        {
            if (providerBox.SelectedItem is string provider)
            {
                _wiredViewModel.SelectedAssistantProvider = provider;
            }

            var model = (modelBox.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(model))
            {
                _wiredViewModel.SelectedAssistantModel = model;
            }

            _wiredViewModel.SelectedAssistantBaseUrl = (baseUrlBox.Text ?? string.Empty).Trim();
            _wiredViewModel.AssistantKeyInput = keyBox.Text ?? string.Empty;
            _wiredViewModel.UseExperimentalGeometryKernel = kernelBox.IsChecked ?? false;
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        RefreshPreview();
        await ShowAnchoredDialogAsync(dialog, returnFocus: false);
    }

    private void OnPrepareExportClick(object? sender, RoutedEventArgs e)
    {
        OnExportClick(sender, e);
    }

    private void OnExportAllFormatsClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
            return;

        _wiredViewModel.ExportAllFormats();
    }

    private void OnAssistantRecipeClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not AButton button || button.Tag is not string recipeName)
        {
            return;
        }

        _wiredViewModel.InsertAssistantRecipe(recipeName);
        AssistantInputBox?.Focus();
        e.Handled = true;
    }

    private async void OnTemplateParameterEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not ATextBox textBox || textBox.DataContext is not ParameterItemViewModel item)
        {
            return;
        }

        try
        {
            await _wiredViewModel.CommitTemplateParameterEditAsync(item);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnTemplateParameterEditorLostFocus), ex);
        }
    }

    private async void OnTemplateParameterEditorKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _wiredViewModel is null || sender is not ATextBox textBox || textBox.DataContext is not ParameterItemViewModel item)
        {
            return;
        }

        try
        {
            await _wiredViewModel.CommitTemplateParameterEditAsync(item);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnTemplateParameterEditorKeyDown), ex);
        }

        e.Handled = true;
    }

    private async void OnAssistantApplyCommandClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.ApplyPendingCommandAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnAssistantApplyCommandClick), ex);
        }

        e.Handled = true;
    }

    private async void OnAssistantInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        if (_wiredViewModel is null || !_wiredViewModel.CanSendAssistant)
        {
            return;
        }

        try
        {
            await _wiredViewModel.SendAssistantAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnAssistantInputKeyDown), ex);
        }

        e.Handled = true;
    }

    private async void OnFeatureTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        var selectedNode = e.AddedItems.OfType<FeatureNodeViewModel>().FirstOrDefault()
            ?? _featureTree?.SelectedItem as FeatureNodeViewModel;

        if (selectedNode is null)
        {
            return;
        }

        try
        {
            await _wiredViewModel.SelectTreeNodeAsync(selectedNode);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnFeatureTreeSelectionChanged), ex);
        }
    }

    private async void OnFeatureTreeDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        var selectedNode = _featureTree?.SelectedItem as FeatureNodeViewModel;
        if (selectedNode is null || selectedNode.EntityId == Guid.Empty)
        {
            return;
        }

        try
        {
            if (selectedNode.EntityKind == CadEntityKind.Sketch)
            {
                await _wiredViewModel.EditSketchAsync(selectedNode.EntityId);
                return;
            }

            var holeParams = _wiredViewModel.GetHoleParams(selectedNode.EntityId);
            if (holeParams is not null)
            {
                var dialog = new HoleFeatureDialog(holeParams.Value.Diameter, holeParams.Value.DepthKind, holeParams.Value.DepthValue, holeParams.Value.CenterOffsetX, holeParams.Value.CenterOffsetY);
            dialog.PreviewRequested += (s, args) =>
            {
                var graph = _wiredViewModel.PreviewHole(args.Diameter, args.CenterOffsetX, args.CenterOffsetY, args.DepthKind == "Blind" ? $"Blind:{args.DepthValue}" : "ThroughAll");
                if (graph != null) _exactScene?.DisplayFeatureGraph(graph);
            };
            var result = await ShowAnchoredDialogAsync<HoleFeatureDialogResult?>(dialog);
            if (result == null && _wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph != null) _exactScene?.DisplayFeatureGraph(_wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph);
                if (result is not null)
                {
                    await _wiredViewModel.UpdateHoleAsync(selectedNode.EntityId, result.Diameter, result.DepthKind, result.DepthValue, result.CenterOffsetX, result.CenterOffsetY);
                }

                return;
            }

            var lpParams = _wiredViewModel.GetLinearPatternParams(selectedNode.EntityId);
            if (lpParams is not null)
            {
                var lpDialog = new LinearPatternDialog(lpParams.Value.Count, lpParams.Value.Spacing, lpParams.Value.Axis);
                lpDialog.PreviewRequested += (s, args) =>
                {
                    var graph = _wiredViewModel.PreviewLinearPattern(args.Count, args.Spacing, args.Axis);
                    if (graph != null) _exactScene?.DisplayFeatureGraph(graph);
                };
                var lpResult = await ShowAnchoredDialogAsync<LinearPatternDialogResult?>(lpDialog);
                if (lpResult == null && _wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph != null) _exactScene?.DisplayFeatureGraph(_wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph);
                if (lpResult is not null)
                {
                    await _wiredViewModel.UpdateLinearPatternAsync(selectedNode.EntityId, lpResult.Count, lpResult.Spacing, lpResult.Axis);
                }

                return;
            }

            var cpParams = _wiredViewModel.GetCircularPatternParams(selectedNode.EntityId);
            if (cpParams is not null)
            {
                var cpDialog = new CircularPatternDialog(cpParams.Value.Count, cpParams.Value.TotalAngle, cpParams.Value.Axis);
                var cpResult = await ShowAnchoredDialogAsync<CircularPatternDialogResult?>(cpDialog);
                if (cpResult is not null)
                {
                    await _wiredViewModel.UpdateCircularPatternAsync(selectedNode.EntityId, cpResult.Count, cpResult.TotalAngle, cpResult.Axis);
                }

                return;
            }

            var filletRadius = _wiredViewModel.GetFilletRadius(selectedNode.EntityId);
            if (filletRadius is not null)
            {
                var dialog = new FilletFeatureDialog(_wiredViewModel.AssistantSelectionSummary, filletRadius.Value);
                var result = await ShowAnchoredDialogAsync<FilletFeatureDialogResult?>(dialog);
                if (result is not null)
                {
                    await _wiredViewModel.UpdateFilletAsync(selectedNode.EntityId, result.Radius);
                }

                return;
            }

            var chamferDist = _wiredViewModel.GetChamferDistance(selectedNode.EntityId);
            if (chamferDist is not null)
            {
                var dialog = new NumericFeatureDialog("Chamfer", "Edit the chamfer distance.", "Distance", chamferDist.Value, 0.1, 100, "mm");
                var raw = await ShowAnchoredDialogAsync<string?>(dialog);
                if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var newDist) && newDist > 0)
                {
                    await _wiredViewModel.UpdateChamferAsync(selectedNode.EntityId, newDist);
                }

                return;
            }

            var shellThick = _wiredViewModel.GetShellThickness(selectedNode.EntityId);
            if (shellThick is not null)
            {
                var dialog = new NumericFeatureDialog("Shell", "Edit the shell wall thickness.", "Thickness", shellThick.Value, 0.1, 500, "mm");
                var raw = await ShowAnchoredDialogAsync<string?>(dialog);
                if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var newThick) && newThick > 0)
                {
                    await _wiredViewModel.UpdateShellAsync(selectedNode.EntityId, newThick);
                }

                return;
            }

            var extrudeParams = _wiredViewModel.GetExtrudeParams(selectedNode.EntityId);
            if (extrudeParams is not null)
            {
                var bodies = _wiredViewModel.GetBodyList();
                var signedDepth = extrudeParams.Value.ReverseDirection ? -extrudeParams.Value.Depth : extrudeParams.Value.Depth;
                var extDialog = new ExtrudeFeatureDialog(selectedNode.Name, "", Math.Abs(extrudeParams.Value.Depth), bodies, extrudeParams.Value.TargetBodyId);
                var extResult = await ShowAnchoredDialogAsync<ExtrudeFeatureDialogResult?>(extDialog);
                if (extResult is not null)
                {
                    await _wiredViewModel.UpdateExtrudeAsync(selectedNode.EntityId, Math.Abs(extResult.Distance), extResult.ReverseDirection, extResult.Operation, extResult.TargetBodyId);
                }

                return;
            }

            var revolveParams = _wiredViewModel.GetRevolveParams(selectedNode.EntityId);
            if (revolveParams is not null)
            {
                var rvDialog = new RevolveFeatureDialog(selectedNode.Name, "", revolveParams.Value.AngleDegrees, revolveParams.Value.Axis);
                var rvResult = await ShowAnchoredDialogAsync<RevolveFeatureDialogResult?>(rvDialog);
                if (rvResult is not null)
                {
                    await _wiredViewModel.UpdateRevolveAsync(selectedNode.EntityId, rvResult.Angle, rvResult.Axis);
                }

                return;
            }

            var sweepParams = _wiredViewModel.GetSweepParams(selectedNode.EntityId);
            if (sweepParams is not null)
            {
                var swDialog = new SweepFeatureDialog(selectedNode.Name, "", sweepParams.Value.Distance, sweepParams.Value.TwistDegrees);
                var swResult = await ShowAnchoredDialogAsync<SweepFeatureDialogResult?>(swDialog);
                if (swResult is not null)
                {
                    await _wiredViewModel.UpdateSweepAsync(selectedNode.EntityId, swResult.Distance, swResult.TwistDegrees);
                }

                return;
            }

            var mirrorAxis = _wiredViewModel.GetMirrorParams(selectedNode.EntityId);
            if (mirrorAxis is not null)
            {
                var mirrorDialog = new MirrorFeatureDialog(mirrorAxis);
                var mirrorResult = await ShowAnchoredDialogAsync<MirrorFeatureDialogResult?>(mirrorDialog);
                if (mirrorResult is not null)
                {
                    await _wiredViewModel.UpdateMirrorAsync(selectedNode.EntityId, mirrorResult.Axis);
                }

                return;
            }

            var loftParams = _wiredViewModel.GetLoftParams(selectedNode.EntityId);
            if (loftParams is not null)
            {
                var sketches = _wiredViewModel.GetClosedSketchProfiles();
                var loftDialog = new LoftFeatureDialog(sketches, loftParams.Value.ProfileAId, loftParams.Value.ProfileBId, loftParams.Value.Distance);
                var loftResult = await ShowAnchoredDialogAsync<LoftFeatureDialogResult?>(loftDialog);
                if (loftResult is not null)
                {
                    await _wiredViewModel.UpdateLoftAsync(selectedNode.EntityId, loftResult.ProfileAId, loftResult.ProfileBId, loftResult.Distance);
                }

                return;
            }

            await ShowParameterEditDialogAsync(selectedNode);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnFeatureTreeDoubleTapped), ex);
        }
    }

    private async void OnFeatureTreeContextMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not Avalonia.Controls.MenuItem menuItem)
        {
            return;
        }

        var node = menuItem.DataContext as FeatureNodeViewModel
            ?? _featureTree?.SelectedItem as FeatureNodeViewModel;
        if (node is null || node.EntityId == Guid.Empty)
        {
            return;
        }

        try
        {
            var tag = menuItem.Tag?.ToString();
            if (tag == "start-sketch" && node.EntityKind == CadEntityKind.ReferencePlane)
            {
                await _wiredViewModel.SelectTreeNodeAsync(node);
                await _wiredViewModel.StartSketchAsync();
            }
            else if (tag == "edit-sketch" && node.EntityKind == CadEntityKind.Sketch)
            {
                await _wiredViewModel.EditSketchAsync(node.EntityId);
            }
            else if (tag == "rename" && node.EntityKind == CadEntityKind.Body)
            {
                var dialog = new RenameDialog(node.Name);
                var newName = await dialog.ShowDialog<string?>(this);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    await _wiredViewModel.RenameBodyAsync(node.EntityId, newName);
                }
            }
            else if (tag == "duplicate" && node.EntityKind == CadEntityKind.Body)
            {
                await _wiredViewModel.SelectTreeNodeAsync(node);
                await _wiredViewModel.DuplicateSelectedBodyAsync();
            }
            else if (tag == "toggle-visibility" && node.EntityKind == CadEntityKind.Body)
            {
                await _wiredViewModel.ToggleBodyVisibilityAsync(node.EntityId);
            }
            else if (tag == "set-color" && node.EntityKind == CadEntityKind.Body)
            {
                var colorDialog = new Dialogs.BodyColorDialog();
                var picked = await colorDialog.ShowDialog<string?>(this);
                // picked is null=cancel, empty=reset, "#rrggbb"=color
                if (picked is not null)
                    await _wiredViewModel.SetBodyColorAsync(node.EntityId, string.IsNullOrEmpty(picked) ? null : picked);
            }
            else if (tag == "edit-parameters")
            {
                await ShowParameterEditDialogAsync(node);
            }
            else if (tag == "delete")
            {
                await _wiredViewModel.SelectTreeNodeAsync(node);
                await _wiredViewModel.HandleViewportDeleteAsync();
            }
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnFeatureTreeContextMenuClick), ex);
        }
    }

    private async Task ShowParameterEditDialogAsync(FeatureNodeViewModel node)
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        await _wiredViewModel.SelectTreeNodeAsync(node);
        var editableParameters = _wiredViewModel.GetEditableParameterSnapshot();
        if (editableParameters.Count == 0)
        {
            _wiredViewModel.AppendToLog($"No editable numeric parameters for {node.Name}.");
            return;
        }

        var dialog = new ParameterEditDialog(
            $"Edit {node.Name}",
            $"Adjust existing parameters for {node.Name}. OK updates the selected model item.",
            editableParameters);
        var result = await ShowAnchoredDialogAsync<ParameterEditDialogResult?>(dialog);
        if (result is null)
        {
            return;
        }

        await _wiredViewModel.ApplyParameterEditsAsync(result.ValuesByKey);
    }

    private async void OnParameterEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_wiredViewModel is null || sender is not ATextBox textBox || textBox.DataContext is not ParameterItemViewModel item)
        {
            return;
        }

        try
        {
            await _wiredViewModel.CommitParameterEditAsync(item);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnParameterEditorLostFocus), ex);
        }
    }

    private async void OnParameterEditorKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _wiredViewModel is null || sender is not ATextBox textBox || textBox.DataContext is not ParameterItemViewModel item)
        {
            return;
        }

        try
        {
            await _wiredViewModel.CommitParameterEditAsync(item);
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnParameterEditorKeyDown), ex);
        }

        e.Handled = true;
    }

    private void OnLightThemeClick(object? sender, RoutedEventArgs e)
    {
        SetTheme(StudioThemeMode.Light);
    }

    private void OnDarkThemeClick(object? sender, RoutedEventArgs e)
    {
        SetTheme(StudioThemeMode.Dark);
    }

    private void OnOpenAclPanelClick(object? sender, RoutedEventArgs e)
    {
        _wiredViewModel?.OpenAclPanel();
        e.Handled = true;
    }

    private void SetTheme(StudioThemeMode mode)
    {
        if (AApplication.Current is App app)
        {
            app.SetStudioTheme(mode);
        }

        UpdateThemeToggleButtons(mode);
        _ = ApplyViewportThemeAsync(mode);
    }

    private void UpdateThemeToggleButtons(StudioThemeMode mode)
    {
        if (_lightThemeButton is not null)
        {
            _lightThemeButton.IsChecked = mode == StudioThemeMode.Light;
        }

        if (_darkThemeButton is not null)
        {
            _darkThemeButton.IsChecked = mode == StudioThemeMode.Dark;
        }
    }

    private void InitializeThemeToggleState()
    {
        if (AApplication.Current is App app)
        {
            UpdateThemeToggleButtons(app.CurrentStudioTheme);
        }
    }

    private void RegisterCommandBindings()
    {
        BindCommand("FileNewMenuItem", "file.new");
        BindCommand("FileOpenMenuItem", "file.open");
        BindCommand("FileRecentMenuItem", "file.recent");
        BindCommand("FileSaveMenuItem", "file.save");
        BindCommand("FileSaveAsMenuItem", "file.saveAs");
        BindCommand("SketchStartMenuItem", "sketch.start");
        BindCommand("SketchFinishMenuItem", "sketch.finish");
        BindCommand("SketchCancelMenuItem", "sketch.cancel");
        BindCommand("SketchLineMenuItem", "sketch.line");
        BindCommand("SketchRectangleMenuItem", "sketch.rectangle");
        BindCommand("SketchCircleMenuItem", "sketch.circle");
        BindCommand("SketchArcMenuItem", "sketch.arc");
        BindCommand("SketchPointMenuItem", "sketch.point");
        BindCommand("SolidExtrudeMenuItem", "solid.extrude");
        BindCommand("TransformMoveMenuItem", "transform.move");
        BindCommand("TransformDatumMenuItem", "transform.datumPlane");
        BindCommand("ViewFitMenuItem", "view.fit");
        BindCommand("ViewCommandPaletteMenuItem", "view.commandPalette");
        BindCommand("AiOpenMenuItem", "ai.openCopilot");
        BindCommand("AiConfigureMenuItem", "ai.configure");
        BindCommand("RecipeOpenAclMenuItem", "recipe.openAcl");
        BindCommand("RecipeOpenSampleMenuItem", "recipe.openSample");
        BindCommand("RecipeRunMenuItem", "recipe.runActive");
        BindCommand("ExportPrepareMenuItem", "export.prepare");
        BindCommand("ExportDialogMenuItem", "export.export");
        BindCommand("SolidRevolveMenuItem", "solid.revolve");
        BindCommand("SolidSweepMenuItem", "solid.sweep");
        BindCommand("SolidLoftMenuItem", "solid.loft");
        BindCommand("ExportQuickExportMenuItem", "export.quick");
        BindCommand("ModifyFilletMenuItem", "solid.fillet");
        BindCommand("ModifyChamferMenuItem", "solid.chamfer");
        BindCommand("ModifyHoleMenuItem", "solid.hole");
        BindCommand("ModifyShellMenuItem", "solid.shell");
        BindCommand("ModifyLinearPatternMenuItem", "solid.linearPattern");
        BindCommand("ModifyCircularPatternMenuItem", "solid.circularPattern");
        BindCommand("ModifyMirrorMenuItem", "solid.mirror");
        BindCommand("ModifyBooleanUnionMenuItem", "solid.booleanUnion");
        BindCommand("ModifyBooleanSubtractMenuItem", "solid.booleanSubtract");
        BindCommand("ModifyBooleanIntersectMenuItem", "solid.booleanIntersect");
        BindCommand("ToolbarSketchStartButton", "sketch.start");
        BindCommand("ToolbarSketchFinishButton", "sketch.finish");
        BindCommand("ToolbarSketchCancelButton", "sketch.cancel");
        BindCommand("ToolbarNewButton", "file.new");
        BindCommand("ToolbarOpenButton", "file.open");
        BindCommand("RecentFilesButton", "file.recent");
        BindCommand("ToolbarSaveButton", "file.save");
        BindCommand("ToolbarSaveAsButton", "file.saveAs");
        BindCommand("ToolbarUndoButton", "edit.undo");
        BindCommand("ToolbarRedoButton", "edit.redo");
        BindCommand("ToolbarExportButton", "export.export");
        BindCommand("ToolbarExtrudeButton", "solid.extrude");
        BindCommand("ToolbarFitViewButton", "view.fit");
        BindCommand("MoveToolButton", "transform.move");
        BindCommand("ToolbarTemplatesButton", "create.templates");
        BindCommand("ToolbarAclButton", "recipe.openAcl");
        BindCommand("ToolbarRecipeButton", "recipe.openSample");
        BindCommand("ToolbarCopilotButton", "ai.openCopilot");
        BindCommand("ToolbarPrepareButton", "export.prepare");
    }

    private void BindCommand(string controlName, string commandId)
    {
        var control = this.FindControl<Avalonia.Controls.Control>(controlName);
        if (control is not null)
        {
            _commandUiBindings.Add(new CommandUiBinding(commandId, control));
        }
    }

    private void RefreshCommandPresentation()
    {
        if (_wiredViewModel is null)
        {
            return;
        }

        foreach (var binding in _commandUiBindings)
        {
            var state = _wiredViewModel.GetCommandState(binding.CommandId);
            binding.Control.IsVisible = state.IsVisible;
            binding.Control.IsEnabled = state.IsEnabled;
            Avalonia.Controls.ToolTip.SetTip(binding.Control, state.Tooltip);
        }
    }
}
