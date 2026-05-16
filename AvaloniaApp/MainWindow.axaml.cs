using System.Collections.Specialized;
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
    private WebViewportHost? _viewportHost;
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
        Patterns = ["*.my3dapp", "*.json"],
        MimeTypes = ["application/json"]
    };

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
        disposableViewModel?.Dispose();
        base.OnClosed(e);
    }

    private static void LogHandlerFailure(string handlerName, Exception ex)
    {
        RuntimeLog.Write("MainWindow", $"{handlerName} failed.", ex);
    }

    private void ResolveNamedControls()
    {
        _viewportHost = this.FindControl<WebViewportHost>("ViewportHost");
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
        RequestFeatureTreeExpansion();
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
        _wiredViewModel = null;
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

    private async Task ApplyViewportStateAsync(ViewportRenderState state)
    {
        if (_viewportHost is null)
        {
            return;
        }

        await _viewportHost.SetSceneAsync(state);
    }

    private async Task ApplyViewportThemeAsync(StudioThemeMode mode)
    {
        if (_viewportHost is null)
        {
            return;
        }

        await _viewportHost.SetThemeAsync(mode);
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (_viewportHost is null)
        {
            return;
        }

        try
        {
            await Task.Delay(250);
            await _viewportHost.EnsureInitializedAsync();
        }
        catch (Exception ex)
        {
            LogHandlerFailure(nameof(OnWindowOpened), ex);
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

        _wiredViewModel.OpenProject(path);
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
                SuggestedFileName = "part-studio.my3dapp",
                DefaultExtension = "my3dapp",
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
            var dialog = new HoleFeatureDialog(10d, "ThroughAll", 20d, 0d, 0d);
            var result = await ShowAnchoredDialogAsync<HoleFeatureDialogResult?>(dialog);
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
        var result = await ShowAnchoredDialogAsync<ExtrudeFeatureDialogResult?>(dialog);
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
                var result = await dialog.ShowDialog<HoleFeatureDialogResult?>(this);
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
                var lpResult = await lpDialog.ShowDialog<LinearPatternDialogResult?>(this);
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
                var cpResult = await cpDialog.ShowDialog<CircularPatternDialogResult?>(this);
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
                var result = await dialog.ShowDialog<FilletFeatureDialogResult?>(this);
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
}
