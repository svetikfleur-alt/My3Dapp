#pragma warning disable CA1416
using System.IO;
using System.Text.Json;
using FormaCore.Engine;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using My3DApp.AvaloniaApp;
using My3DApp.AvaloniaApp.Services;
using Avalonia.Controls;
using Avalonia.Platform;
using WinForms = System.Windows.Forms;

namespace My3DApp.AvaloniaApp.Controls;

public sealed class WebViewportHost : NativeControlHost
{
    private const string LocalWebHostName = "my3dapp.local";
    private static readonly string[] RequiredWebAssetRelativePaths =
    [
        Path.Combine("three", "build", "three.module.min.js"),
        Path.Combine("three", "examples", "jsm", "controls", "OrbitControls.js"),
        Path.Combine("three", "examples", "jsm", "controls", "TransformControls.js")
    ];
    private static readonly JsonSerializerOptions RenderStateJsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private WinForms.Panel? _container;
    private WebView2? _webView;
    private SoftwareViewportControl? _softwareViewport;
    private WinForms.Label? _fallbackLabel;
    private readonly Queue<string> _pendingScripts = new();
    private bool _isViewportReady;
    private bool _initializationStarted;
    private bool _usingSoftwareFallback;
    private StudioThemeMode _currentThemeMode = StudioThemeMode.Light;
    private string _currentWebViewUserDataDirectory = string.Empty;

    public event EventHandler<ViewportSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<ViewportSketchPlacementEventArgs>? SketchPlacementRequested;

    public event EventHandler<ViewportSketchPreviewEventArgs>? SketchPreviewRequested;

    public event EventHandler? SketchPreviewCleared;

    public event EventHandler? SketchStepCancelRequested;

    public event EventHandler? ToggleConstructionModeRequested;

    public event EventHandler? DeleteRequested;

    public event EventHandler? MoveToolDeactivated;

    public event EventHandler<ViewportFaceClickedEventArgs>? FaceClicked;

    public event EventHandler<ViewportBoxSelectEventArgs>? BoxSelectCompleted;

    public event EventHandler? SelectionCleared;

    public event EventHandler<ViewportBodyTransformEventArgs>? BodyTransformCommitted;

    public event EventHandler? SketchTransformToolDeactivated;

    public event EventHandler<ViewportSketchEntityTransformEventArgs>? SketchEntityTransformCommitted;

    public event EventHandler? SketchRotateToolDeactivated;

    public event EventHandler<ViewportSketchEntityRotationEventArgs>? SketchEntityRotationCommitted;

    public event EventHandler<ViewportSketchEntityContextMenuEventArgs>? SketchEntityContextMenuRequested;

    public event EventHandler<ViewportMeasureResultEventArgs>? MeasureResultReceived;

    public event EventHandler? MeasureCancelled;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _container = new WinForms.Panel
        {
            Dock = WinForms.DockStyle.Fill,
            BackColor = GetViewportHostBackColor(_currentThemeMode)
        };

        _webView = CreateWebViewControl();

        _fallbackLabel = new WinForms.Label
        {
            Dock = WinForms.DockStyle.Fill,
            Visible = false,
            AutoSize = false,
            BackColor = GetFallbackBackColor(_currentThemeMode),
            ForeColor = GetFallbackForeColor(_currentThemeMode),
            Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Regular),
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Padding = new WinForms.Padding(16),
            Text = "3D viewport initialization failed.\r\nCheck WebView2 runtime and local assets."
        };

        _softwareViewport = new SoftwareViewportControl
        {
            Dock = WinForms.DockStyle.Fill,
            Visible = false
        };
        _softwareViewport.SetTheme(_currentThemeMode);
        _softwareViewport.EntitySelected += HandleSoftwareViewportEntitySelected;
        _softwareViewport.SketchPlacementRequested += HandleSoftwareViewportSketchPlacementRequested;
        _softwareViewport.SketchPreviewRequested += HandleSoftwareViewportSketchPreviewRequested;
        _softwareViewport.SketchPreviewCleared += HandleSoftwareViewportSketchPreviewCleared;

        _container.Controls.Add(_webView);
        _container.Controls.Add(_softwareViewport);
        _container.Controls.Add(_fallbackLabel);

        return new PlatformHandle(_container.Handle, "HWND");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (_container is not null)
        {
            _container.HandleCreated -= HandleContainerCreated;
        }

        if (_webView?.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.WebMessageReceived -= HandleWebMessageReceived;
        }

        if (_webView is not null)
        {
            _webView.NavigationCompleted -= HandleNavigationCompleted;
        }

        _webView?.Dispose();
        if (_softwareViewport is not null)
        {
            _softwareViewport.EntitySelected -= HandleSoftwareViewportEntitySelected;
            _softwareViewport.SketchPlacementRequested -= HandleSoftwareViewportSketchPlacementRequested;
            _softwareViewport.SketchPreviewRequested -= HandleSoftwareViewportSketchPreviewRequested;
            _softwareViewport.SketchPreviewCleared -= HandleSoftwareViewportSketchPreviewCleared;
            _softwareViewport.Dispose();
        }
        _fallbackLabel?.Dispose();
        _container?.Dispose();
        _webView = null;
        _softwareViewport = null;
        _fallbackLabel = null;
        _container = null;
        _pendingScripts.Clear();
        _isViewportReady = false;
        _initializationStarted = false;
        _usingSoftwareFallback = false;
        base.DestroyNativeControlCore(control);
    }

    public async Task SetSceneAsync(ViewportRenderState state)
    {
        if (state is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(state, RenderStateJsonOptions);
        _softwareViewport?.SetScene(state);
        await ExecuteOnViewportAsync($"window.my3dappSetScene?.({payload});");
    }

    public async Task FocusSelectionAsync()
    {
        if (_usingSoftwareFallback)
        {
            _softwareViewport?.FocusSelection();
            return;
        }

        await ExecuteOnViewportAsync("window.my3dappFocusSelection?.();");
    }

    public async Task SetThemeAsync(StudioThemeMode mode)
    {
        _currentThemeMode = mode;
        ApplyHostTheme();
        _softwareViewport?.SetTheme(mode);
        await ExecuteOnViewportAsync($"window.my3dappSetTheme?.('{mode}');");
    }

    public Task EnsureInitializedAsync()
    {
        QueueInitializeWebView();
        return Task.CompletedTask;
    }

    public async Task SetMoveToolActiveAsync(bool active)
    {
        if (_usingSoftwareFallback)
        {
            return;
        }

        var jsValue = active ? "true" : "false";
        await ExecuteOnViewportAsync($"window.my3dappSetMoveTool?.({jsValue});");
    }

    public async Task SetSketchTransformToolActiveAsync(bool active)
    {
        if (_usingSoftwareFallback)
        {
            return;
        }

        var jsValue = active ? "true" : "false";
        await ExecuteOnViewportAsync($"window.my3dappSetSketchTransformTool?.({jsValue});");
    }

    public async Task SetBoxSelectionAsync(IReadOnlyList<Guid> ids, bool append)
    {
        if (_usingSoftwareFallback)
        {
            return;
        }

        var idsJson = "[" + string.Join(",", ids.Select(id => $"'{id}'")) + "]";
        var appendJs = append ? "true" : "false";
        await ExecuteOnViewportAsync($"window.my3dappSetBoxSelection?.({idsJson}, {appendJs});");
    }

    public async Task SetSketchRotateToolActiveAsync(bool active)
    {
        if (_usingSoftwareFallback)
        {
            return;
        }

        var jsValue = active ? "true" : "false";
        await ExecuteOnViewportAsync($"window.my3dappSetSketchRotateTool?.({jsValue});");
    }

    public async Task SetExtrudePreviewAsync(ViewportRenderBody? previewBody)
    {
        if (previewBody is null)
        {
            await ExecuteOnViewportAsync("window.my3dappClearExtrudePreview?.();");
            return;
        }

        var payload = JsonSerializer.Serialize(previewBody, RenderStateJsonOptions);
        await ExecuteOnViewportAsync($"window.my3dappSetExtrudePreview?.({payload});");
    }

    public async Task SetGridVisibleAsync(bool visible)
    {
        if (_usingSoftwareFallback)
        {
            _softwareViewport?.SetGridVisible(visible);
            return;
        }

        var jsValue = visible ? "true" : "false";
        await ExecuteOnViewportAsync($"window.my3dappSetGridVisible?.({jsValue});");
    }

    public async Task SetSectionPlaneAsync(bool enabled, string axis, double offset)
    {
        if (_usingSoftwareFallback)
            return;

        var axisJs = $"\"{axis}\"";
        var offsetJs = offset.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var enabledJs = enabled ? "true" : "false";
        await ExecuteOnViewportAsync($"window.my3dappSetSectionPlane?.({axisJs}, {offsetJs}, {enabledJs});");
    }

    public async Task SetMeasureModeAsync(bool active)
    {
        if (_usingSoftwareFallback)
            return;

        var activeJs = active ? "true" : "false";
        await ExecuteOnViewportAsync($"window.my3dappSetMeasureMode?.({activeJs});");
    }

    public async Task SetSketchToolHintAsync(string hint)
    {
        if (_usingSoftwareFallback)
            return;

        var escaped = hint.Replace("\\", "\\\\").Replace("'", "\\'");
        await ExecuteOnViewportAsync($"window.my3dappSetSketchToolHint?.('{escaped}');");
    }

    private WebView2 CreateWebViewControl()
    {
        var webView = new WebView2
        {
            Dock = WinForms.DockStyle.Fill,
            DefaultBackgroundColor = GetViewportHostBackColor(_currentThemeMode)
        };
        webView.NavigationCompleted += HandleNavigationCompleted;
        return webView;
    }

    private async Task InitializeWebViewAsync()
    {
        if (_webView is null || _initializationStarted)
        {
            return;
        }

        _initializationStarted = true;
        try
        {
            await WaitForViewportHostReadinessAsync();
            await Task.Delay(250);
            const int maxAttempts = 4;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    ResetWebViewControl();
                    if (_webView is null)
                    {
                        throw new InvalidOperationException("WebView2 control is missing.");
                    }

                    await WaitForWebViewControlReadinessAsync(_webView);
                    var webAssetsDirectory = ResolveWebAssetsDirectory();
                    var missingAssets = FindMissingRequiredWebAssets(webAssetsDirectory);
                    if (missingAssets.Count > 0)
                    {
                        var message =
                            "3D viewport assets are incomplete.\r\n" +
                            string.Join("\r\n", missingAssets.Select(path => $"- Missing: {path}"));
                        RuntimeLog.Write("Viewport", message);
                        ShowFallbackMessage(true, message);
                        return;
                    }

                    _currentWebViewUserDataDirectory = ResolveWebViewUserDataDirectory();
                    var environmentOptions = new CoreWebView2EnvironmentOptions("--disable-gpu --disable-gpu-compositing");
                    var environment = await CoreWebView2Environment.CreateAsync(
                        browserExecutableFolder: null,
                        userDataFolder: _currentWebViewUserDataDirectory,
                        options: environmentOptions);
                    await EnsureWebViewInitializedOnControlThreadAsync(_webView, environment);

                    if (_webView.CoreWebView2 is not null)
                    {
                        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                        if (Directory.Exists(webAssetsDirectory))
                        {
                            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                                LocalWebHostName,
                                webAssetsDirectory,
                                CoreWebView2HostResourceAccessKind.Allow);
                            RuntimeLog.Write("Viewport", $"Mapped {LocalWebHostName} -> {webAssetsDirectory}");
                            RuntimeLog.Write("Viewport", $"WebView2 user data -> {_currentWebViewUserDataDirectory}");
                        }
                        else
                        {
                            RuntimeLog.Write("Viewport", $"Assets folder not found: {webAssetsDirectory}");
                        }

                        _webView.CoreWebView2.WebMessageReceived -= HandleWebMessageReceived;
                        _webView.CoreWebView2.WebMessageReceived += HandleWebMessageReceived;
                    }

                    _usingSoftwareFallback = false;
                    if (_softwareViewport is not null)
                    {
                        _softwareViewport.Visible = false;
                    }
                    if (_webView is not WebView2 readyWebView)
                    {
                        throw new InvalidOperationException("WebView2 control is unavailable after initialization.");
                    }

                    readyWebView.Visible = true;
                    ShowFallbackMessage(false, null);
                    readyWebView.NavigateToString(ThreeJsBootstrapHtml);
                    return;
                }
                catch (Exception ex)
                {
                    RuntimeLog.Write("Viewport", $"InitializeWebViewAsync attempt {attempt}/{maxAttempts} failed.", ex);
                    if (attempt >= maxAttempts)
                    {
                        ShowFallbackMessage(
                            false,
                            null);
                        ActivateSoftwareFallback(ex);
                        return;
                    }

                    await Task.Delay(800 * attempt);
                }
            }
        }
        finally
        {
            _initializationStarted = false;
        }
    }

    private void ResetWebViewControl()
    {
        if (_container is null)
        {
            return;
        }

        _isViewportReady = false;

        if (_webView is not null)
        {
            if (_webView.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.WebMessageReceived -= HandleWebMessageReceived;
            }

            _webView.NavigationCompleted -= HandleNavigationCompleted;
            _container.Controls.Remove(_webView);
            _webView.Dispose();
        }

        _webView = CreateWebViewControl();
        _container.Controls.Add(_webView);
        _webView.BringToFront();
        if (_softwareViewport is not null && _usingSoftwareFallback)
        {
            _softwareViewport.BringToFront();
        }
        if (_fallbackLabel is not null && _fallbackLabel.Visible)
        {
            _fallbackLabel.BringToFront();
        }
    }

    private Task EnsureWebViewInitializedOnControlThreadAsync(WebView2 webView, CoreWebView2Environment environment)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        EventHandler<CoreWebView2InitializationCompletedEventArgs>? handler = null;
        handler = (_, args) =>
        {
            webView.CoreWebView2InitializationCompleted -= handler;
            if (args.IsSuccess)
            {
                completion.TrySetResult(true);
                return;
            }

            completion.TrySetException(args.InitializationException ?? new InvalidOperationException("WebView2 initialization failed."));
        };

        void StartInitialization()
        {
            webView.CoreWebView2InitializationCompleted += handler;

            try
            {
                var initTask = webView.EnsureCoreWebView2Async(environment);
                _ = initTask.ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        webView.CoreWebView2InitializationCompleted -= handler;
                        var failure = task.Exception?.InnerException
                            ?? (Exception?)task.Exception
                            ?? new InvalidOperationException("WebView2 initialization task failed.");
                        completion.TrySetException(failure);
                    }
                    else if (task.IsCanceled)
                    {
                        webView.CoreWebView2InitializationCompleted -= handler;
                        completion.TrySetCanceled();
                    }
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                webView.CoreWebView2InitializationCompleted -= handler;
                completion.TrySetException(ex);
            }
        }

        if (_container is not null && _container.InvokeRequired)
        {
            _container.BeginInvoke((Action)StartInitialization);
        }
        else
        {
            StartInitialization();
        }

        return completion.Task;
    }

    private void QueueInitializeWebView()
    {
        if (_container is null)
        {
            return;
        }

        if (_container.IsHandleCreated)
        {
            _container.BeginInvoke((Action)(() => _ = InitializeWebViewAsync()));
            return;
        }

        _container.HandleCreated -= HandleContainerCreated;
        _container.HandleCreated += HandleContainerCreated;
    }

    private void HandleContainerCreated(object? sender, EventArgs e)
    {
        if (_container is null)
        {
            return;
        }

        _container.HandleCreated -= HandleContainerCreated;
        QueueInitializeWebView();
    }

    private async Task WaitForViewportHostReadinessAsync()
    {
        if (_container is null)
        {
            return;
        }

        const int maxIterations = 40;
        for (var i = 0; i < maxIterations; i++)
        {
            if (_container.IsDisposed)
            {
                return;
            }

            if (_container.IsHandleCreated &&
                _container.Width > 32 &&
                _container.Height > 32)
            {
                return;
            }

            await Task.Delay(75);
        }

        RuntimeLog.Write("Viewport", "Host readiness wait timed out; continuing WebView initialization.");
    }

    private static async Task WaitForWebViewControlReadinessAsync(WebView2 webView)
    {
        const int maxIterations = 40;
        for (var i = 0; i < maxIterations; i++)
        {
            if (webView.IsDisposed)
            {
                return;
            }

            if (webView.Parent is not null &&
                webView.Width > 32 &&
                webView.Height > 32)
            {
                return;
            }

            await Task.Delay(75);
        }
    }

    private void ShowFallbackMessage(bool visible, string? message)
    {
        if (_fallbackLabel is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            _fallbackLabel.Text = message;
        }

        _fallbackLabel.Visible = visible;
        if (visible)
        {
            _fallbackLabel.BringToFront();
        }
    }

    private void ApplyHostTheme()
    {
        if (_container is not null)
        {
            _container.BackColor = GetViewportHostBackColor(_currentThemeMode);
        }

        _softwareViewport?.SetTheme(_currentThemeMode);

        if (_fallbackLabel is not null)
        {
            _fallbackLabel.BackColor = GetFallbackBackColor(_currentThemeMode);
            _fallbackLabel.ForeColor = GetFallbackForeColor(_currentThemeMode);
        }

        if (_webView is not null)
        {
            _webView.DefaultBackgroundColor = GetViewportHostBackColor(_currentThemeMode);
        }
    }

    private static System.Drawing.Color GetViewportHostBackColor(StudioThemeMode mode) =>
        mode == StudioThemeMode.Dark
            ? System.Drawing.Color.FromArgb(32, 38, 47)
            : System.Drawing.Color.FromArgb(232, 237, 244);

    private static System.Drawing.Color GetFallbackBackColor(StudioThemeMode mode) =>
        mode == StudioThemeMode.Dark
            ? System.Drawing.Color.FromArgb(58, 43, 43)
            : System.Drawing.Color.FromArgb(255, 248, 248);

    private static System.Drawing.Color GetFallbackForeColor(StudioThemeMode mode) =>
        mode == StudioThemeMode.Dark
            ? System.Drawing.Color.FromArgb(255, 198, 198)
            : System.Drawing.Color.FromArgb(151, 58, 58);

    private static IReadOnlyList<string> FindMissingRequiredWebAssets(string webAssetsDirectory)
    {
        if (!Directory.Exists(webAssetsDirectory))
        {
            return [$"Assets directory not found: {webAssetsDirectory}"];
        }

        var missing = new List<string>();
        foreach (var relativePath in RequiredWebAssetRelativePaths)
        {
            var absolutePath = Path.Combine(webAssetsDirectory, relativePath);
            if (!File.Exists(absolutePath))
            {
                missing.Add(relativePath.Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        return missing;
    }

    private static string ResolveWebAssetsDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Web"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Web")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "Web"))
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private static string ResolveWebViewUserDataDirectory()
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "My3DApp",
                "WebView2",
                "Avalonia");
            Directory.CreateDirectory(root);
            CleanupStaleWebViewDirectories(root);
            var sessionDirectory = Path.Combine(root, $"session-{Environment.ProcessId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(sessionDirectory);
            return sessionDirectory;
        }
        catch
        {
            try
            {
                var fallbackRoot = Path.Combine(AppContext.BaseDirectory, "webview2-data");
                Directory.CreateDirectory(fallbackRoot);
                var fallbackSession = Path.Combine(fallbackRoot, $"session-{Environment.ProcessId}-{Guid.NewGuid():N}");
                Directory.CreateDirectory(fallbackSession);
                return fallbackSession;
            }
            catch
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "My3DApp-WebView2");
                Directory.CreateDirectory(tempRoot);
                var tempSession = Path.Combine(tempRoot, $"session-{Environment.ProcessId}-{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempSession);
                return tempSession;
            }
        }
    }

    private static void CleanupStaleWebViewDirectories(string root)
    {
        var cutoffUtc = DateTime.UtcNow.AddHours(-12);
        foreach (var directory in Directory.EnumerateDirectories(root, "session-*"))
        {
            try
            {
                var info = new DirectoryInfo(directory);
                if (info.LastWriteTimeUtc >= cutoffUtc)
                {
                    continue;
                }

                info.Delete(recursive: true);
            }
            catch
            {
                // Ignore stale cleanup failures. The active session uses a unique folder.
            }
        }
    }

    private async void HandleNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || _webView?.CoreWebView2 is null)
        {
            RuntimeLog.Write("Viewport", $"Navigation failed. IsSuccess={e.IsSuccess}, Status={e.WebErrorStatus}");
            ShowFallbackMessage(
                true,
                "3D viewport navigation failed.\r\n" +
                $"Status: {e.WebErrorStatus}");
            return;
        }

        RuntimeLog.Write("Viewport", "Navigation completed.");
    }

    private async Task ExecuteOnViewportAsync(string script)
    {
        if (_webView?.CoreWebView2 is null || !_isViewportReady)
        {
            _pendingScripts.Enqueue(script);
            return;
        }

        try
        {
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("Viewport", "ExecuteOnViewportAsync failed.", ex);
        }
    }

    private async Task FlushPendingScriptsAsync()
    {
        if (_webView?.CoreWebView2 is null || !_isViewportReady)
        {
            return;
        }

        while (_pendingScripts.Count > 0)
        {
            var script = _pendingScripts.Dequeue();
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("Viewport", "Executing pending viewport script failed.", ex);
            }
        }
    }

    private async void HandleWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            if (!message.RootElement.TryGetProperty("type", out var typeProperty))
            {
                return;
            }

            var messageType = typeProperty.GetString();
            switch (messageType)
            {
                case "viewport-ready":
                    _isViewportReady = true;
                    RuntimeLog.Write("ViewportJS", "Viewport reported ready.");
                    await FlushPendingScriptsAsync();
                    break;

                case "scene-applied":
                {
                    var planeCount = message.RootElement.TryGetProperty("planeCount", out var planeCountProp)
                        ? planeCountProp.GetInt32()
                        : 0;
                    var bodyCount = message.RootElement.TryGetProperty("bodyCount", out var bodyCountProp)
                        ? bodyCountProp.GetInt32()
                        : 0;
                    RuntimeLog.Write("ViewportJS", $"Scene applied. planes={planeCount}, bodies={bodyCount}");
                    break;
                }

                case "viewport-error":
                {
                    var details = message.RootElement.TryGetProperty("message", out var detailsProp)
                        ? detailsProp.GetString()
                        : "Unknown viewport error.";
                    var stack = message.RootElement.TryGetProperty("stack", out var stackProp)
                        ? stackProp.GetString()
                        : string.Empty;

                    RuntimeLog.Write("ViewportJS", $"{details}{Environment.NewLine}{stack}".Trim());
                    ShowFallbackMessage(
                        true,
                        "3D viewport failed to load.\r\n" +
                        (string.IsNullOrWhiteSpace(details) ? "Unknown viewport error." : details));
                    break;
                }

                case "selection-changed":
                {
                    var entityIdText = message.RootElement.TryGetProperty("entityId", out var entityIdProp)
                        ? entityIdProp.GetString()
                        : null;
                    var entityKindText = message.RootElement.TryGetProperty("entityKind", out var entityKindProp)
                        ? entityKindProp.GetString()
                        : null;

                    if (Guid.TryParse(entityIdText, out var entityId) &&
                        TryParseEntityKind(entityKindText, out var entityKind))
                    {
                        SelectionChanged?.Invoke(this, new ViewportSelectionChangedEventArgs(entityKind, entityId));
                    }

                    break;
                }

                case "sketch-point":
                {
                    if (message.RootElement.TryGetProperty("planeId", out var sketchPlaneIdProp) &&
                        Guid.TryParse(sketchPlaneIdProp.GetString(), out var sketchPlaneId) &&
                        message.RootElement.TryGetProperty("planeKind", out var planeKindProp) &&
                        message.RootElement.TryGetProperty("u", out var uProp) &&
                        message.RootElement.TryGetProperty("v", out var vProp))
                    {
                        var planeKind = planeKindProp.GetString() ?? "Top";
                        SketchPlacementRequested?.Invoke(
                            this,
                            new ViewportSketchPlacementEventArgs(
                                sketchPlaneId,
                                planeKind,
                                uProp.GetDouble(),
                                vProp.GetDouble()));
                    }
                    break;
                }

                case "sketch-preview":
                {
                    if (message.RootElement.TryGetProperty("planeId", out var previewPlaneIdProp) &&
                        Guid.TryParse(previewPlaneIdProp.GetString(), out var previewPlaneId) &&
                        message.RootElement.TryGetProperty("planeKind", out var previewPlaneKindProp) &&
                        message.RootElement.TryGetProperty("u", out var previewUProp) &&
                        message.RootElement.TryGetProperty("v", out var previewVProp))
                    {
                        SketchPreviewRequested?.Invoke(
                            this,
                            new ViewportSketchPreviewEventArgs(
                                previewPlaneId,
                                previewPlaneKindProp.GetString() ?? "Top",
                                previewUProp.GetDouble(),
                                previewVProp.GetDouble()));
                    }
                    break;
                }

                case "sketch-clear-preview":
                    SketchPreviewCleared?.Invoke(this, EventArgs.Empty);
                    break;

                case "sketch-cancel-step":
                    SketchStepCancelRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case "toggle-construction-mode":
                    ToggleConstructionModeRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case "delete-requested":
                    DeleteRequested?.Invoke(this, EventArgs.Empty);
                    break;

                case "move-tool-deactivated":
                    MoveToolDeactivated?.Invoke(this, EventArgs.Empty);
                    break;

                case "sketch-transform-deactivated":
                    SketchTransformToolDeactivated?.Invoke(this, EventArgs.Empty);
                    break;

                case "sketch-rotate-deactivated":
                    SketchRotateToolDeactivated?.Invoke(this, EventArgs.Empty);
                    break;

                case "sketch-entity-context":
                {
                    var entityIdText = message.RootElement.TryGetProperty("entityId", out var ctxEntityIdProp)
                        ? ctxEntityIdProp.GetString()
                        : null;
                    var entityTypeText = message.RootElement.TryGetProperty("entityType", out var ctxEntityTypeProp)
                        ? ctxEntityTypeProp.GetString() ?? string.Empty
                        : string.Empty;
                    var sx = message.RootElement.TryGetProperty("screenX", out var sxProp) ? sxProp.GetDouble() : 0d;
                    var sy = message.RootElement.TryGetProperty("screenY", out var syProp) ? syProp.GetDouble() : 0d;

                    if (Guid.TryParse(entityIdText, out var ctxEntityId))
                    {
                        SketchEntityContextMenuRequested?.Invoke(
                            this,
                            new ViewportSketchEntityContextMenuEventArgs(ctxEntityId, entityTypeText, sx, sy));
                    }

                    break;
                }

                case "sketch-entity-transform-committed":
                {
                    var sketchIdText = message.RootElement.TryGetProperty("sketchId", out var sketchIdProp)
                        ? sketchIdProp.GetString()
                        : null;

                    if (!Guid.TryParse(sketchIdText, out var sketchId))
                    {
                        return;
                    }

                    var planeKind = message.RootElement.TryGetProperty("planeKind", out var planeKindProp)
                        ? planeKindProp.GetString() ?? "Top"
                        : "Top";
                    var du = message.RootElement.TryGetProperty("du", out var duProp) ? duProp.GetDouble() : 0d;
                    var dv = message.RootElement.TryGetProperty("dv", out var dvProp) ? dvProp.GetDouble() : 0d;

                    SketchEntityTransformCommitted?.Invoke(
                        this,
                        new ViewportSketchEntityTransformEventArgs(sketchId, planeKind, du, dv));
                    break;
                }

                case "sketch-entity-rotate-committed":
                {
                    var sketchIdText = message.RootElement.TryGetProperty("sketchId", out var rSketchIdProp)
                        ? rSketchIdProp.GetString()
                        : null;

                    if (!Guid.TryParse(sketchIdText, out var rSketchId))
                        break;

                    var rPlaneKind = message.RootElement.TryGetProperty("planeKind", out var rPlaneKindProp)
                        ? rPlaneKindProp.GetString() ?? "Top"
                        : "Top";
                    var pivotU = message.RootElement.TryGetProperty("pivotU", out var puProp) ? puProp.GetDouble() : 0d;
                    var pivotV = message.RootElement.TryGetProperty("pivotV", out var pvProp) ? pvProp.GetDouble() : 0d;
                    var angleDeg = message.RootElement.TryGetProperty("angleDeg", out var adProp) ? adProp.GetDouble() : 0d;

                    SketchEntityRotationCommitted?.Invoke(
                        this,
                        new ViewportSketchEntityRotationEventArgs(rSketchId, rPlaneKind, pivotU, pivotV, angleDeg));
                    break;
                }

                case "face-click":
                {
                    var faceBodyIdText = message.RootElement.TryGetProperty("bodyId", out var fbProp) ? fbProp.GetString() : null;
                    if (!Guid.TryParse(faceBodyIdText, out var faceBodyId)) break;

                    double FaceD(string key) => message.RootElement.TryGetProperty(key, out var p) ? p.GetDouble() : 0d;
                    FaceClicked?.Invoke(this, new ViewportFaceClickedEventArgs(
                        faceBodyId,
                        FaceD("nx"), FaceD("ny"), FaceD("nz"),
                        FaceD("ox"), FaceD("oy"), FaceD("oz"),
                        FaceD("ux"), FaceD("uy"), FaceD("uz"),
                        FaceD("vx"), FaceD("vy"), FaceD("vz")));
                    break;
                }

                case "box-select-result":
                {
                    if (!message.RootElement.TryGetProperty("ids", out var idsProp))
                        break;

                    var ids = new List<Guid>();
                    foreach (var elem in idsProp.EnumerateArray())
                    {
                        if (Guid.TryParse(elem.GetString(), out var gid))
                            ids.Add(gid);
                    }

                    if (ids.Count > 0)
                    {
                        var append = message.RootElement.TryGetProperty("append", out var appendProp) && appendProp.GetBoolean();
                        BoxSelectCompleted?.Invoke(this, new ViewportBoxSelectEventArgs(ids, append));
                    }
                    break;
                }

                case "selection-cleared":
                    SelectionCleared?.Invoke(this, EventArgs.Empty);
                    break;

                case "measure-result":
                {
                    var dist = message.RootElement.TryGetProperty("distance", out var dv) ? dv.GetDouble() : 0;
                    MeasureResultReceived?.Invoke(this, new ViewportMeasureResultEventArgs(dist));
                    break;
                }

                case "measure-cancelled":
                    MeasureCancelled?.Invoke(this, EventArgs.Empty);
                    break;

                case "body-transform-committed":
                {
                    var bodyIdText = message.RootElement.TryGetProperty("bodyId", out var bodyIdProp)
                        ? bodyIdProp.GetString()
                        : null;

                    if (!Guid.TryParse(bodyIdText, out var bodyId))
                    {
                        return;
                    }

                    var x = message.RootElement.TryGetProperty("x", out var xProp) ? xProp.GetDouble() : 0d;
                    var y = message.RootElement.TryGetProperty("y", out var yProp) ? yProp.GetDouble() : 0d;
                    var z = message.RootElement.TryGetProperty("z", out var zProp) ? zProp.GetDouble() : 0d;

                    BodyTransformCommitted?.Invoke(this, new ViewportBodyTransformEventArgs(bodyId, x, y, z));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("Viewport", "Failed to process web message.", ex);
        }
    }

    private static bool TryParseEntityKind(string? raw, out CadEntityKind kind)
    {
        kind = CadEntityKind.None;
        return raw switch
        {
            "Body" => Set(out kind, CadEntityKind.Body),
            "ReferencePlane" => Set(out kind, CadEntityKind.ReferencePlane),
            "Feature" => Set(out kind, CadEntityKind.Feature),
            "Sketch" => Set(out kind, CadEntityKind.Sketch),
            _ => false
        };
    }

    private static bool Set<T>(out T target, T value)
    {
        target = value;
        return true;
    }

    private void ActivateSoftwareFallback(Exception lastError)
    {
        _usingSoftwareFallback = true;
        RuntimeLog.Write("Viewport", $"Falling back to software viewport after WebView2 failure: {lastError.GetType().Name}: {lastError.Message}");

        if (_webView is not null)
        {
            _webView.Visible = false;
        }

        if (_softwareViewport is not null)
        {
            _softwareViewport.Visible = true;
            _softwareViewport.BringToFront();
            _softwareViewport.FocusSelection();
        }
    }

    private void HandleSoftwareViewportEntitySelected(CadEntityKind kind, Guid entityId)
    {
        SelectionChanged?.Invoke(this, new ViewportSelectionChangedEventArgs(kind, entityId));
    }

    private void HandleSoftwareViewportSketchPlacementRequested(object? sender, ViewportSketchPlacementEventArgs e)
    {
        SketchPlacementRequested?.Invoke(this, e);
    }

    private void HandleSoftwareViewportSketchPreviewRequested(object? sender, ViewportSketchPreviewEventArgs e)
    {
        SketchPreviewRequested?.Invoke(this, e);
    }

    private void HandleSoftwareViewportSketchPreviewCleared(object? sender, EventArgs e)
    {
        SketchPreviewCleared?.Invoke(this, EventArgs.Empty);
    }

    private const string ThreeJsBootstrapHtml = """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Three.js Viewport</title>
  <style>
    html, body { margin: 0; width: 100%; height: 100%; overflow: hidden; background: #e8edf4; }
    canvas { width: 100%; height: 100%; display: block; }
    #hud {
      position: fixed;
      left: 12px;
      top: 10px;
      font: 12px Segoe UI, sans-serif;
      color: #5a6472;
      padding: 4px 8px;
      background: rgba(247,250,252,0.82);
      border: 1px solid #d6dce5;
      pointer-events: none;
      user-select: none;
    }
    #viewport-menu {
      position: fixed;
      display: none;
      z-index: 20;
      min-width: 140px;
      background: #ffffff;
      border: 1px solid #ccd3de;
      box-shadow: 0 4px 12px rgba(28, 40, 55, 0.15);
      padding: 4px;
    }
    #viewport-menu button {
      width: 100%;
      border: none;
      background: transparent;
      font: 13px Segoe UI, sans-serif;
      color: #2f3d4f;
      text-align: left;
      padding: 7px 10px;
      cursor: pointer;
    }
    #viewport-menu button:hover {
      background: #eaf3fe;
    }
    #snap-overlay {
      position: fixed;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      pointer-events: none;
      z-index: 5;
    }
    #box-select-overlay {
      position: fixed;
      display: none;
      pointer-events: none;
      z-index: 10;
      border: 1px dashed #4c9ef0;
      background: rgba(76, 158, 240, 0.07);
    }
    #box-select-overlay.crossing {
      border-color: #f0a84c;
      background: rgba(240, 168, 76, 0.07);
    }
    #vcube {
      position: fixed;
      top: 12px;
      right: 12px;
      width: 92px;
      height: 92px;
      z-index: 16;
      cursor: pointer;
      border-radius: 8px;
      overflow: hidden;
      touch-action: none;
    }
    #view-nav {
      position: fixed;
      top: 110px;
      right: 12px;
      display: grid;
      grid-template-columns: repeat(2, 42px);
      gap: 4px;
      z-index: 15;
      pointer-events: auto;
    }
    #view-nav button {
      width: 42px;
      height: 24px;
      border: 1px solid #c2cad5;
      background: rgba(247,250,252,0.88);
      color: #3a4a5e;
      font: 11px/1 Segoe UI, sans-serif;
      cursor: pointer;
      border-radius: 3px;
      user-select: none;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    #view-nav button:hover {
      background: rgba(220, 230, 245, 0.96);
      border-color: #9ab0cb;
    }
    #view-nav button:active {
      background: rgba(190, 210, 240, 0.96);
    }
  </style>
</head>
<body>
  <canvas id="vcube"></canvas>
  <canvas id="snap-overlay"></canvas>
  <div id="box-select-overlay"></div>
  <div id="hud">Reference planes: Top / Front / Right</div>
  <div id="view-nav">
    <button type="button" data-view="iso">Iso</button>
    <button type="button" data-view="iso-ne">NE</button>
    <button type="button" data-view="iso-nw">NW</button>
    <button type="button" data-view="top">Top</button>
    <button type="button" data-view="front">Front</button>
    <button type="button" data-view="right">Right</button>
    <button type="button" data-view="iso-se">SE</button>
    <button type="button" data-view="iso-sw">SW</button>
    <button type="button" data-view="bottom">Bot</button>
    <button type="button" data-view="back">Back</button>
    <button type="button" data-view="left">Left</button>
    <button type="button" id="wf-toggle" data-wf="0">Shd</button>
  </div>
  <div id="viewport-menu">
    <button type="button" data-action="delete">Delete</button>
    <button type="button" data-action="focus">Focus Camera</button>
  </div>
  <script type="module">
    async function importThreeModules() {
      const threeModule = await import('https://my3dapp.local/three/build/three.module.min.js');
      const orbitModule = await import('https://my3dapp.local/three/examples/jsm/controls/OrbitControls.js');
      const transformModule = await import('https://my3dapp.local/three/examples/jsm/controls/TransformControls.js');
      return {
        THREE: threeModule,
        OrbitControls: orbitModule.OrbitControls,
        TransformControls: transformModule.TransformControls
      };
    }

    try {
      const { THREE, OrbitControls, TransformControls } = await importThreeModules();
      const themePalettes = {
        Light: {
          bodyBackground: '#e8edf4',
          sceneBackground: 0xe8edf4,
          hudBackground: 'rgba(247,250,252,0.82)',
          hudBorder: '#d6dce5',
          hudText: '#5a6472',
          originRing: '#223244',
          originFill: '#ffffff',
          originDot: '#223244',
          originShadow: 'rgba(34,50,68,0.18)',
          menuBackground: '#ffffff',
          menuBorder: '#ccd3de',
          menuText: '#2f3d4f',
          menuHover: '#eaf3fe'
        },
        Dark: {
          bodyBackground: '#20262f',
          sceneBackground: 0x20262f,
          hudBackground: 'rgba(41,48,60,0.84)',
          hudBorder: '#495364',
          hudText: '#d7dfeb',
          originRing: '#eef4ff',
          originFill: '#23303d',
          originDot: '#eef4ff',
          originShadow: 'rgba(9,13,18,0.24)',
          menuBackground: '#2a2f38',
          menuBorder: '#495364',
          menuText: '#e5ebf4',
          menuHover: '#374152'
        }
      };

      const renderer = new THREE.WebGLRenderer({ antialias: true });
      renderer.setPixelRatio(window.devicePixelRatio);
      renderer.setSize(window.innerWidth, window.innerHeight);
      renderer.outputColorSpace = THREE.SRGBColorSpace;
      document.body.appendChild(renderer.domElement);

      const scene = new THREE.Scene();
      scene.background = new THREE.Color(0xe8edf4);

      const camera = new THREE.PerspectiveCamera(45, window.innerWidth / window.innerHeight, 0.1, 2400);
      const defaultViewDirection = new THREE.Vector3(1, 0.82, 1).normalize();
      let lastBodyCount = 0;
      camera.position.copy(defaultViewDirection.clone().multiplyScalar(42));

      const controls = new OrbitControls(camera, renderer.domElement);
      controls.enableDamping = true;
      controls.dampingFactor = 0.08;
      controls.target.set(0, 0, 0);
      controls.minDistance = 8;
      controls.maxDistance = 2400;
      controls.update();
      // CAD bindings: left-drag = orbit, middle-drag = pan, scroll-wheel = zoom.
      // Walk mode is deferred — no existing UI entry point; would require a separate
      // first-person controller and key-capture loop that conflicts with sketch mode.
      controls.mouseButtons = { LEFT: THREE.MOUSE.ROTATE, MIDDLE: THREE.MOUSE.PAN, RIGHT: THREE.MOUSE.PAN };

      const hemi = new THREE.HemisphereLight(0xffffff, 0xdfe7f4, 0.88);
      scene.add(hemi);

      const dir = new THREE.DirectionalLight(0xffffff, 0.82);
      dir.position.set(7, 10, 6);
      scene.add(dir);

      const fill = new THREE.DirectionalLight(0xffffff, 0.24);
      fill.position.set(-8, 6, -5);
      scene.add(fill);

      const axesHelper = new THREE.AxesHelper(8);
      scene.add(axesHelper);

      function createOriginTexture(themeName) {
        const palette = themePalettes[String(themeName || 'Light') === 'Dark' ? 'Dark' : 'Light'];
        const canvas = document.createElement('canvas');
        canvas.width = 128;
        canvas.height = 128;

        const context = canvas.getContext('2d');
        if (!context) {
          return null;
        }

        const center = 64;
        context.clearRect(0, 0, 128, 128);

        context.fillStyle = palette.originShadow;
        context.beginPath();
        context.arc(center, center, 46, 0, Math.PI * 2);
        context.fill();

        context.fillStyle = palette.originRing;
        context.beginPath();
        context.arc(center, center, 36, 0, Math.PI * 2);
        context.fill();

        context.fillStyle = palette.originFill;
        context.beginPath();
        context.arc(center, center, 24, 0, Math.PI * 2);
        context.fill();

        context.fillStyle = palette.originDot;
        context.beginPath();
        context.arc(center, center, 10, 0, Math.PI * 2);
        context.fill();

        const texture = new THREE.CanvasTexture(canvas);
        texture.colorSpace = THREE.SRGBColorSpace;
        texture.minFilter = THREE.LinearFilter;
        texture.magFilter = THREE.LinearFilter;
        texture.generateMipmaps = false;
        texture.needsUpdate = true;
        return texture;
      }

      function createSketchPointTexture(isPreview) {
        const canvas = document.createElement('canvas');
        canvas.width = 64;
        canvas.height = 64;

        const context = canvas.getContext('2d');
        if (!context) {
          return null;
        }

        const center = 32;
        context.clearRect(0, 0, 64, 64);

        if (isPreview) {
          // Preview / snap candidate: hollow ring
          context.fillStyle = 'rgba(255,255,255,0.82)';
          context.beginPath();
          context.arc(center, center, 11, 0, Math.PI * 2);
          context.fill();
          context.strokeStyle = 'rgba(34,48,66,0.48)';
          context.lineWidth = 4;
          context.beginPath();
          context.arc(center, center, 11, 0, Math.PI * 2);
          context.stroke();
        } else {
          // Committed sketch point: dense filled dot with thin outline — CAD style
          context.fillStyle = 'rgba(22,36,52,0.90)';
          context.beginPath();
          context.arc(center, center, 10, 0, Math.PI * 2);
          context.fill();
          context.fillStyle = 'rgba(255,255,255,0.62)';
          context.beginPath();
          context.arc(center, center, 5, 0, Math.PI * 2);
          context.fill();
        }

        const texture = new THREE.CanvasTexture(canvas);
        texture.needsUpdate = true;
        return texture;
      }

      const originGeometry = new THREE.BufferGeometry();
      originGeometry.setAttribute('position', new THREE.Float32BufferAttribute([0, 0, 0], 3));
      const originMaterial = new THREE.PointsMaterial({
        map: createOriginTexture('Light'),
        size: 24,
        sizeAttenuation: false,
        transparent: true,
        alphaTest: 0.14,
        depthTest: false,
        depthWrite: false,
        toneMapped: false
      });
      const originMarker = new THREE.Points(originGeometry, originMaterial);
      originMarker.renderOrder = 12;
      originMarker.name = 'Origin';
      originMarker.frustumCulled = false;
      scene.add(originMarker);

      const raycaster = new THREE.Raycaster();
      const pointer = new THREE.Vector2();
      const bodyMap = new Map();
      const edgeMap = new Map();
      const planeMap = new Map();
      const sketchMap = new Map();
      const angleDimMap = new Map();
      const linearDimMap = new Map();
      const radialDimMap = new Map();
      const hud = document.getElementById('hud');
      const menu = document.getElementById('viewport-menu');
      const transformControls = new TransformControls(camera, renderer.domElement);
      transformControls.setMode('translate');
      transformControls.setSize(0.85);
      transformControls.visible = false;
      scene.add(transformControls);

      let isWireframeMode = false;
      let isMeasureMode = false;
      let measurePtA = null;
      const r3 = (v) => Number(v).toFixed(3);
      let extrudePreviewMesh = null;
      let extrudePreviewEdges = null;
      let hoveredBodyId = null;
      let selectedBodyId = null;
      let selectedBodyIds = new Set();
      let selectedPlaneId = null;
      let selectedSketchId = null;
      const boxSelectOverlay = document.getElementById('box-select-overlay');
      let boxSelectActive = false;
      let boxSelectStartX = 0;
      let boxSelectStartY = 0;
      let boxSelectEndX = 0;
      let boxSelectEndY = 0;
      let currentMode = 'DirectPrimitive';
      let activePlaneName = 'Top';
      let previousMode = currentMode;
      let _sketchToolHint = '';
      let previousActivePlaneName = activePlaneName;
      let contextPoint = controls.target.clone();
      let currentTheme = 'Light';
      let moveToolActive = false;
      let isDragging = false;
      let dragStartPosition = null;
      let sketchTransformToolActive = false;
      let sketchTransformDragging = false;
      let sketchTransformTargetId = null;
      let sketchTransformTargetKind = 'Top';
      let sketchTransformStartUV = null;
      let sketchTransformCurrentUV = null;
      let sketchRotateToolActive = false;
      let sketchRotateDragging = false;
      let sketchRotateTargetId = null;
      let sketchRotateTargetKind = 'Top';
      let sketchRotatePivotUV = null;
      let sketchRotateStartAngle = null;
      let sketchRotateCurrentAngle = null;
      let sketchRotateAccumDeg = 0;
      let gridVisible = false;
      let snapCandidates = [];
      let sketchEntitySegments = [];
      let currentSnap = null;
      let sketchEntityBoundsUV = null;
      let _sketchCursorSx = -9999;
      let _sketchCursorSy = -9999;
      const SNAP_RADIUS = 8;
      const ALIGN_TOL = 5;
      const snapOverlay = document.getElementById('snap-overlay');
      const snapCtx = snapOverlay ? snapOverlay.getContext('2d') : null;
      if (snapOverlay) {
        snapOverlay.width = window.innerWidth;
        snapOverlay.height = window.innerHeight;
      }

      transformControls.addEventListener('dragging-changed', (event) => {
        controls.enabled = !event.value;
        isDragging = event.value;
        if (event.value && transformControls.object) {
          dragStartPosition = transformControls.object.position.clone();
        }
        if (!event.value && transformControls.object && transformControls.object.userData?.type === 'body') {
          const mesh = transformControls.object;
          postMessage({
            type: 'body-transform-committed',
            bodyId: mesh.userData.entityId,
            x: round3(mesh.position.x),
            y: round3(mesh.position.y),
            z: round3(mesh.position.z)
          });
          dragStartPosition = null;
        }
      });

      function postMessage(payload) {
        if (window.chrome?.webview?.postMessage) {
          window.chrome.webview.postMessage(payload);
        }
      }

      function setHudText(text) {
        if (hud) {
          hud.textContent = text;
        }
      }

      function applyTheme(themeName) {
        currentTheme = String(themeName || 'Light') === 'Dark' ? 'Dark' : 'Light';
        const palette = themePalettes[currentTheme];
        document.body.style.background = palette.bodyBackground;
        scene.background = new THREE.Color(palette.sceneBackground);
        renderer.setClearColor(palette.sceneBackground, 1);

        if (hud) {
          hud.style.background = palette.hudBackground;
          hud.style.borderColor = palette.hudBorder;
          hud.style.color = palette.hudText;
        }

        const nextOriginTexture = createOriginTexture(currentTheme);
        if (nextOriginTexture) {
          if (originMaterial.map) {
            originMaterial.map.dispose();
          }

          originMaterial.map = nextOriginTexture;
          originMaterial.needsUpdate = true;
        }

        if (menu) {
          menu.style.background = palette.menuBackground;
          menu.style.borderColor = palette.menuBorder;
        }

        if (menu) {
          for (const button of menu.querySelectorAll('button')) {
            button.style.color = palette.menuText;
          }
        }

        // Re-apply edge line colors for new theme
        for (const lines of edgeMap.values()) {
          if (lines.material) {
            lines.material.color.setHex(bodyEdgeColor());
          }
        }

        // Re-style view-nav buttons for theme
        const viewNavEl = document.getElementById('view-nav');
        if (viewNavEl) {
          const isDark = currentTheme === 'Dark';
          for (const btn of viewNavEl.querySelectorAll('button')) {
            btn.style.background = isDark ? 'rgba(41,48,60,0.88)' : 'rgba(247,250,252,0.88)';
            btn.style.borderColor = isDark ? '#495364' : '#c2cad5';
            btn.style.color = isDark ? '#d7dfeb' : '#3a4a5e';
          }
        }
      }

      function round3(value) {
        const number = Number(value);
        if (!Number.isFinite(number)) {
          return 0;
        }

        return Math.round(number * 1000) / 1000;
      }

      function setCameraClipping(distance) {
        camera.near = Math.max(0.1, distance / 250);
        camera.far = Math.max(2400, distance * 32);
        camera.updateProjectionMatrix();
      }

      function applyDefaultView() {
        const defaultDistance = 42;
        camera.position.copy(defaultViewDirection.clone().multiplyScalar(defaultDistance));
        camera.up.set(0, 0, 1);
        controls.target.set(0, 0, 0);
        controls.minDistance = 8;
        controls.maxDistance = 2400;
        setCameraClipping(defaultDistance);
        controls.update();
      }

      function getSceneBodyBounds() {
        const bounds = new THREE.Box3();
        let hasBounds = false;

        for (const mesh of bodyMap.values()) {
          bounds.expandByObject(mesh);
          hasBounds = true;
        }

        return hasBounds && !bounds.isEmpty() ? bounds : null;
      }

      function frameBounds(bounds, padding = 1.55) {
        if (!bounds || bounds.isEmpty()) {
          applyDefaultView();
          return false;
        }

        const center = bounds.getCenter(new THREE.Vector3());
        const size = bounds.getSize(new THREE.Vector3());
        const maxDimension = Math.max(size.x, size.y, size.z, 1);
        const halfFovY = THREE.MathUtils.degToRad(camera.fov * 0.5);
        const halfFovX = Math.atan(Math.tan(halfFovY) * camera.aspect);
        const fitHeightDistance = (size.y * 0.5) / Math.max(Math.tan(halfFovY), 0.01);
        const fitWidthDistance = (size.x * 0.5) / Math.max(Math.tan(halfFovX), 0.01);
        const fitDepthDistance = size.z * 0.9;
        const distance = Math.max(fitHeightDistance, fitWidthDistance, fitDepthDistance, maxDimension) * padding;
        const viewDirection = camera.position.clone().sub(controls.target);

        if (viewDirection.lengthSq() < 0.0001) {
          viewDirection.copy(defaultViewDirection);
        }

        viewDirection.normalize();
        camera.position.copy(center.clone().add(viewDirection.multiplyScalar(distance)));
        controls.target.copy(center);
        controls.minDistance = Math.max(2, maxDimension * 0.18);
        controls.maxDistance = Math.max(2400, distance * 14);
        setCameraClipping(distance);
        controls.update();
        return true;
      }

      function sketchPlaneDirection(planeName) {
        switch (String(planeName || '').toLowerCase()) {
          case 'front':
            return {
              forward: new THREE.Vector3(1, 0, 0),
              up: new THREE.Vector3(0, 0, 1)
            };
          case 'right':
            return {
              forward: new THREE.Vector3(0, 1, 0),
              up: new THREE.Vector3(0, 0, 1)
            };
          default:
            return {
              forward: new THREE.Vector3(0, 0, 1),
              up: new THREE.Vector3(0, 1, 0)
            };
        }
      }

      function applySketchView(planeName, bounds) {
        const orientation = sketchPlaneDirection(planeName);
        const center = bounds && !bounds.isEmpty()
          ? bounds.getCenter(new THREE.Vector3())
          : new THREE.Vector3(0, 0, 0);
        const size = bounds && !bounds.isEmpty()
          ? bounds.getSize(new THREE.Vector3())
          : new THREE.Vector3(12, 12, 12);
        const maxDimension = Math.max(size.x, size.y, size.z, 12);
        const distance = Math.max(18, maxDimension * 1.45);
        camera.up.copy(orientation.up);
        camera.position.copy(center.clone().add(orientation.forward.multiplyScalar(distance)));
        controls.target.copy(center);
        controls.minDistance = Math.max(2, maxDimension * 0.18);
        controls.maxDistance = Math.max(1800, distance * 12);
        setCameraClipping(distance);
        controls.update();
      }

      function applyFacePlaneView(fp, bounds) {
        const normal = new THREE.Vector3(fp.nx, fp.ny, fp.nz).normalize();
        const origin = new THREE.Vector3(fp.ox, fp.oy, fp.oz);
        const up = new THREE.Vector3(fp.ux, fp.uy, fp.uz).normalize();
        if (up.length() < 0.01) up.set(0, 1, 0);
        const center = bounds && !bounds.isEmpty() ? bounds.getCenter(new THREE.Vector3()) : origin.clone();
        const size = bounds && !bounds.isEmpty() ? bounds.getSize(new THREE.Vector3()) : new THREE.Vector3(12, 12, 12);
        const maxDimension = Math.max(size.x, size.y, size.z, 10);
        const distance = Math.max(16, maxDimension * 1.45);
        camera.up.copy(up);
        camera.position.copy(center.clone().addScaledVector(normal, distance));
        controls.target.copy(center);
        controls.minDistance = Math.max(2, maxDimension * 0.18);
        controls.maxDistance = Math.max(1800, distance * 12);
        setCameraClipping(distance);
        controls.update();
      }

      function shouldReframe(bounds, nextBodyCount) {
        if (!bounds) {
          return nextBodyCount === 0 && lastBodyCount !== 0;
        }

        if (lastBodyCount === 0 && nextBodyCount > 0) {
          return true;
        }

        const expandedBounds = bounds.clone().expandByScalar(1.2);
        if (expandedBounds.containsPoint(camera.position)) {
          return true;
        }

        const center = bounds.getCenter(new THREE.Vector3());
        const radius = Math.max(bounds.getSize(new THREE.Vector3()).length() * 0.5, 1);
        const distance = camera.position.distanceTo(center);
        return distance < radius * 1.35;
      }

      function planeColor(kind) {
        switch (String(kind || '').toLowerCase()) {
          case 'top': return 0x8fa5cf;
          case 'front': return 0x97bba6;
          case 'right': return 0xc9a2a2;
          default: return 0xb7c1d2;
        }
      }

      function bodyColor(kind) {
        switch (String(kind || '').toLowerCase()) {
          case 'box': return 0x91a4be;
          case 'sphere': return 0x93a3c1;
          case 'cylinder': return 0x8fa2bd;
          case 'cone': return 0x91a2bb;
          case 'torus': return 0x8f9bb7;
          case 'pyramid': return 0x9aa6bf;
          case 'wedge': return 0x9baac1;
          case 'ellipsoid': return 0x91a7c4;
          case 'capsule': return 0x96a8bf;
          case 'hemisphere': return 0x8fa7b8;
          case 'prism': return 0xa0aabd;
          case 'arrow': return 0x93a0b0;
          case 'icosphere': return 0x8ea1b8;
          case 'tetrahedron': return 0x99a4b5;
          case 'octahedron': return 0x8f98ad;
          case 'icosahedron': return 0x97a1b6;
          default: return 0x90a4bf;
        }
      }

      function sketchColor(sketch, selected = false) {
        if (sketch?.isPreview) {
          return currentTheme === 'Dark' ? 0xd4e8ff : 0x79a8e4;
        }

        if (sketch?.isDraft) {
          return currentTheme === 'Dark' ? 0xa5cdff : 0x3e7ac4;
        }

        return currentTheme === 'Dark' ? 0xb8c4d6 : 0x5f708b;
      }

      function constrainedSketchColor(selected = false) {
        return currentTheme === 'Dark'
          ? (selected ? 0xf2f6fb : 0xe4ebf2)
          : 0x182029;
      }

      function unconstrainedSketchColor(selected = false) {
        return currentTheme === 'Dark'
          ? (selected ? 0x9ed0ff : 0x78b0ff)
          : (selected ? 0x0f75d8 : 0x2660d6);
      }

      function sketchCurveColor(sketch, curve, selected = false) {
        if (curve?.isConstruction === true) {
          return currentTheme === 'Dark' ? 0x7ab8e0 : 0x3a7bbf;
        }

        if (sketch?.isPreview) {
          return sketchColor(sketch, selected);
        }

        if (sketch?.isFullyDefined === true || curve?.isConstrained === true) {
          return constrainedSketchColor(selected);
        }

        return unconstrainedSketchColor(selected);
      }

      function orientPlane(mesh, kind) {
        const normalized = String(kind || '').toLowerCase();
        if (normalized === 'front') {
          mesh.rotation.set(0, Math.PI / 2, 0);
          return;
        }

        if (normalized === 'right') {
          mesh.rotation.set(Math.PI / 2, 0, 0);
          return;
        }

        mesh.rotation.set(0, 0, 0);
      }

      function disposeObject(object) {
        if (!object) {
          return;
        }

        object.traverse?.((node) => {
          if (node.geometry) {
            node.geometry.dispose();
          }

          if (node.material) {
            if (Array.isArray(node.material)) {
              node.material.forEach((item) => item.dispose?.());
            } else {
              node.material.dispose?.();
            }
          }
        });

        scene.remove(object);
      }

      function rebuildPlanes(planes) {
        for (const mesh of planeMap.values()) {
          disposeObject(mesh);
        }
        planeMap.clear();

        for (const plane of planes || []) {
          const planeGroup = new THREE.Group();
          
          // Base plane (semi-transparent)
          const planeMaterial = new THREE.MeshBasicMaterial({
            color: planeColor(plane.kind),
            transparent: true,
            opacity: 0.34,
            side: THREE.DoubleSide,
            depthWrite: false
          });
          const planeMesh = new THREE.Mesh(new THREE.PlaneGeometry(12, 12), planeMaterial);
          planeMesh.userData.type = 'plane-face';
          planeGroup.add(planeMesh);

          planeGroup.name = plane.name;
          planeGroup.userData.type = 'plane';
          planeGroup.userData.entityId = plane.planeId;
          planeGroup.userData.kind = plane.kind;
          planeGroup.userData.baseColor = planeColor(plane.kind);
          planeGroup.userData.surfaceMesh = planeMesh;
          planeGroup.visible = plane.visible !== false;
          orientPlane(planeGroup, plane.kind);
          planeGroup.renderOrder = -2;
          planeMap.set(plane.planeId, planeGroup);
          scene.add(planeGroup);
        }
      }

      function isBodySelected(id) {
        return id === selectedBodyId || selectedBodyIds.has(id);
      }

      function setHoverBody(newId) {
        if (hoveredBodyId === newId) return;
        // Clear previous hover
        if (hoveredBodyId) {
          const prev = bodyMap.get(hoveredBodyId);
          if (prev?.material?.emissive && !isBodySelected(hoveredBodyId)) {
            prev.material.emissive.setHex(0x000000);
            prev.material.emissiveIntensity = 0;
          }
          const prevEdge = edgeMap.get(hoveredBodyId);
          if (prevEdge?.material && !isBodySelected(hoveredBodyId)) {
            prevEdge.material.color.setHex(bodyEdgeColor());
            prevEdge.material.opacity = 0.45;
          }
        }
        hoveredBodyId = newId;
        // Apply hover highlight
        if (hoveredBodyId && !isBodySelected(hoveredBodyId)) {
          const m = bodyMap.get(hoveredBodyId);
          if (m?.material?.emissive) {
            m.material.emissive.setHex(0x3a6fa0);
            m.material.emissiveIntensity = 0.10;
          }
          const edgeLines = edgeMap.get(hoveredBodyId);
          if (edgeLines?.material) {
            edgeLines.material.color.setHex(bodyEdgeColorSelected());
            edgeLines.material.opacity = 0.60;
          }
        }
      }

      function bodyEdgeColor() {
        return currentTheme === 'Dark' ? 0x8a9baf : 0x3a4f68;
      }

      function bodyEdgeColorSelected() {
        return currentTheme === 'Dark' ? 0x82cfff : 0x1a6bb5;
      }

      function rebuildBodies(bodies) {
        for (const mesh of bodyMap.values()) {
          disposeObject(mesh);
        }
        bodyMap.clear();
        for (const lines of edgeMap.values()) {
          disposeObject(lines);
        }
        edgeMap.clear();

        for (const body of bodies || []) {
          const geometry = new THREE.BufferGeometry();
          geometry.setAttribute('position', new THREE.Float32BufferAttribute(body.positions || [], 3));
          geometry.setIndex(body.indices || []);
          geometry.computeVertexNormals();

          // Use custom body color if set, otherwise fall back to kind-based palette
          const resolvedColor = (body.color && body.color.startsWith('#'))
            ? parseInt(body.color.replace('#', '0x'), 16)
            : bodyColor(body.kind);
          const material = new THREE.MeshStandardMaterial({
            color: resolvedColor,
            roughness: 0.52,
            metalness: 0.04
          });

          const mesh = new THREE.Mesh(geometry, material);
          mesh.name = body.name || 'Body';
          mesh.position.set(Number(body.x) || 0, Number(body.y) || 0, Number(body.z) || 0);
          mesh.userData.type = 'body';
          mesh.userData.entityId = body.bodyId;
          mesh.userData.kind = body.kind;
          mesh.material.wireframe = isWireframeMode;
          bodyMap.set(body.bodyId, mesh);
          scene.add(mesh);

          // Edge lines for silhouette/feature-edge clarity
          const edgesGeo = new THREE.EdgesGeometry(geometry, 15);
          const edgesMat = new THREE.LineBasicMaterial({
            color: bodyEdgeColor(),
            transparent: true,
            opacity: 0.45,
            toneMapped: false,
            depthTest: true
          });
          const edgeLines = new THREE.LineSegments(edgesGeo, edgesMat);
          edgeLines.position.copy(mesh.position);
          edgeLines.userData.type = 'body-edges';
          edgeLines.userData.entityId = body.bodyId;
          edgeLines.renderOrder = 1;
          edgeLines.visible = !isWireframeMode;
          edgeMap.set(body.bodyId, edgeLines);
          scene.add(edgeLines);
        }
      }

      function rebuildSketches(sketches) {
        for (const group of sketchMap.values()) {
          disposeObject(group);
        }
        sketchMap.clear();

        for (const sketch of sketches || []) {
          const group = new THREE.Group();
          group.name = sketch.name || 'Sketch';
          group.userData.type = 'sketch';
          group.userData.entityId = sketch.sketchId;
          group.userData.isDraft = sketch.isDraft === true;
          group.userData.isPreview = sketch.isPreview === true;

          for (const curve of sketch.curves || []) {
            if (!Array.isArray(curve.points) || curve.points.length < 3) {
              continue;
            }

            if (String(curve.kind || 'polyline').toLowerCase() === 'point') {
              const isConstructionPoint = curve.isConstruction === true;
              const pointGeometry = new THREE.BufferGeometry();
              pointGeometry.setAttribute('position', new THREE.Float32BufferAttribute(curve.points, 3));
              const pointMaterial = new THREE.PointsMaterial({
                color: sketchCurveColor(sketch, curve, false),
                map: createSketchPointTexture(sketch.isPreview),
                size: sketch.isPreview ? 6 : 4,
                sizeAttenuation: false,
                transparent: true,
                opacity: sketch.isPreview ? 0.72 : (isConstructionPoint ? 0.65 : 0.92),
                alphaTest: 0.18,
                depthWrite: false,
                toneMapped: false
              });
              const points = new THREE.Points(pointGeometry, pointMaterial);
              points.userData.isConstruction = isConstructionPoint;
              points.userData.isConstrained = curve.isConstrained === true;
              points.userData.isPreview = sketch.isPreview === true;
              points.renderOrder = 9;
              points.frustumCulled = false;
              group.add(points);
              continue;
            }

            if (curve.points.length < 6) {
              continue;
            }

            const geometry = new THREE.BufferGeometry();
            geometry.setAttribute('position', new THREE.Float32BufferAttribute(curve.points, 3));
            const isConstructionCurve = curve.isConstruction === true;
            const material = isConstructionCurve
              ? new THREE.LineDashedMaterial({
                  color: currentTheme === 'Dark' ? 0x7ab8e0 : 0x3a7bbf,
                  transparent: true,
                  opacity: 0.65,
                  dashSize: 0.5,
                  gapSize: 0.35,
                  toneMapped: false
                })
              : new THREE.LineBasicMaterial({
                  color: sketchCurveColor(sketch, curve, false),
                  transparent: true,
                  opacity: sketch.isPreview ? 0.72 : (sketch.isDraft ? 0.96 : 0.82),
                  toneMapped: false
                });
            const line = new THREE.Line(geometry, material);
            line.userData.isConstruction = isConstructionCurve;
            line.userData.isConstrained = curve.isConstrained === true;
            line.userData.isPreview = sketch.isPreview === true;
            if (isConstructionCurve) {
              line.computeLineDistances();
            }
            line.renderOrder = 8;
            group.add(line);
          }

          if (group.children.length === 0) {
            disposeObject(group);
            continue;
          }

          group.userData.isFullyDefined = sketch.isFullyDefined === true;
          sketchMap.set(sketch.sketchId, group);
          scene.add(group);
        }

        for (const [key, grp] of angleDimMap.entries()) {
          disposeObject(grp);
        }
        angleDimMap.clear();

        for (const [key, grp] of linearDimMap.entries()) {
          disposeObject(grp);
        }
        linearDimMap.clear();

        for (const [key, grp] of radialDimMap.entries()) {
          disposeObject(grp);
        }
        radialDimMap.clear();

        for (const sketch of sketches || []) {
          // Angle dimensions
          if (Array.isArray(sketch.angleDimensions)) {
            for (const dim of sketch.angleDimensions) {
              const dimGroup = buildAngleDimGroup(dim, sketch);
              if (dimGroup) {
                const key = `a-${sketch.sketchId}-${dim.label}`;
                angleDimMap.set(key, dimGroup);
                scene.add(dimGroup);
              }
            }
          }

          // Linear dimensions
          if (Array.isArray(sketch.linearDimensions)) {
            for (const dim of sketch.linearDimensions) {
              const dimGroup = buildLinearDimGroup(dim);
              if (dimGroup) {
                const key = `l-${sketch.sketchId}-${dim.label}`;
                linearDimMap.set(key, dimGroup);
                scene.add(dimGroup);
              }
            }
          }

          // Radial dimensions
          if (Array.isArray(sketch.radialDimensions)) {
            for (const dim of sketch.radialDimensions) {
              const dimGroup = buildRadialDimGroup(dim);
              if (dimGroup) {
                const key = `r-${sketch.sketchId}-${dim.label}`;
                radialDimMap.set(key, dimGroup);
                scene.add(dimGroup);
              }
            }
          }
        }
      }

      function buildAngleDimGroup(dim, sketch) {
        const cx = dim.cx || 0;
        const cy = dim.cy || 0;
        const cz = dim.cz || 0;
        const startDeg = dim.startDeg || 0;
        const sweepDeg = dim.sweepDeg || 0;
        const radius = Math.max(dim.radius || 1, 0.2);
        const label = dim.label || '?°';

        const isDark = currentTheme === 'Dark';
        const dimColor = isDark ? 0xf0c060 : 0xb85800;

        const group = new THREE.Group();
        group.userData.type = 'angleDim';

        const segments = 24;
        const arcPts = [];
        for (let i = 0; i <= segments; i++) {
          const frac = i / segments;
          const angleDeg = startDeg + sweepDeg * frac;
          const angleRad = angleDeg * Math.PI / 180;

          const planeName = String(sketch.planeName || 'Top').trim().toLowerCase();
          let px, py, pz;
          if (planeName === 'front') {
            px = cx;
            py = cy + Math.cos(angleRad) * radius;
            pz = cz + Math.sin(angleRad) * radius;
          } else if (planeName === 'right') {
            px = cx + Math.cos(angleRad) * radius;
            py = cy;
            pz = cz + Math.sin(angleRad) * radius;
          } else {
            px = cx + Math.cos(angleRad) * radius;
            py = cy + Math.sin(angleRad) * radius;
            pz = cz;
          }
          arcPts.push(px, py, pz);
        }

        const arcGeo = new THREE.BufferGeometry();
        arcGeo.setAttribute('position', new THREE.Float32BufferAttribute(arcPts, 3));
        const arcMat = new THREE.LineBasicMaterial({
          color: dimColor,
          transparent: true,
          opacity: 0.88,
          toneMapped: false
        });
        const arcLine = new THREE.Line(arcGeo, arcMat);
        arcLine.renderOrder = 12;
        group.add(arcLine);

        const canvas = document.createElement('canvas');
        canvas.width = 128;
        canvas.height = 48;
        const ctx2d = canvas.getContext('2d');
        ctx2d.fillStyle = isDark ? 'rgba(30,30,30,0.72)' : 'rgba(255,255,255,0.72)';
        ctx2d.fillRect(0, 0, 128, 48);
        ctx2d.fillStyle = isDark ? '#f0c060' : '#7a3800';
        ctx2d.font = 'bold 22px sans-serif';
        ctx2d.textAlign = 'center';
        ctx2d.textBaseline = 'middle';
        ctx2d.fillText(label, 64, 24);

        const texture = new THREE.CanvasTexture(canvas);
        const spriteMat = new THREE.SpriteMaterial({
          map: texture,
          transparent: true,
          opacity: 0.95,
          toneMapped: false,
          depthTest: false
        });
        const sprite = new THREE.Sprite(spriteMat);
        sprite.renderOrder = 13;

        const midFrac = 0.5;
        const midDeg = (startDeg + sweepDeg * midFrac) * Math.PI / 180;
        const labelR = radius * 1.35;
        const planeName2 = String(sketch.planeName || 'Top').trim().toLowerCase();
        if (planeName2 === 'front') {
          sprite.position.set(cx, cy + Math.cos(midDeg) * labelR, cz + Math.sin(midDeg) * labelR);
        } else if (planeName2 === 'right') {
          sprite.position.set(cx + Math.cos(midDeg) * labelR, cy, cz + Math.sin(midDeg) * labelR);
        } else {
          sprite.position.set(cx + Math.cos(midDeg) * labelR, cy + Math.sin(midDeg) * labelR, cz);
        }
        sprite.scale.set(0.9, 0.34, 1);
        group.add(sprite);

        return group;
      }

      function makeDimSprite(text, isDriven) {
        const isDark = currentTheme === 'Dark';
        const canvas = document.createElement('canvas');
        canvas.width = 160;
        canvas.height = 48;
        const ctx2d = canvas.getContext('2d');
        ctx2d.fillStyle = isDark ? 'rgba(26,26,26,0.72)' : 'rgba(255,255,255,0.72)';
        ctx2d.fillRect(0, 0, 160, 48);
        ctx2d.fillStyle = isDriven
          ? (isDark ? '#90a0b0' : '#607080')
          : (isDark ? '#7acfff' : '#005a9e');
        ctx2d.font = 'bold 22px sans-serif';
        ctx2d.textAlign = 'center';
        ctx2d.textBaseline = 'middle';
        ctx2d.fillText(text, 80, 24);
        const texture = new THREE.CanvasTexture(canvas);
        const mat = new THREE.SpriteMaterial({
          map: texture, transparent: true, opacity: 0.95,
          toneMapped: false, depthTest: false
        });
        const sprite = new THREE.Sprite(mat);
        sprite.renderOrder = 13;
        return sprite;
      }

      function makeDimLine(pts, color) {
        const geo = new THREE.BufferGeometry();
        geo.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
        const mat = new THREE.LineBasicMaterial({
          color, transparent: true, opacity: 0.85, toneMapped: false
        });
        const line = new THREE.Line(geo, mat);
        line.renderOrder = 12;
        return line;
      }

      // Linear dimension: dimension line offset from entity + extension lines + label
      function buildLinearDimGroup(dim) {
        const isDark = currentTheme === 'Dark';
        const dimColor = isDark ? 0x7acfff : 0x005a9e;

        const p1 = new THREE.Vector3(dim.p1x, dim.p1y, dim.p1z);
        const p2 = new THREE.Vector3(dim.p2x, dim.p2y, dim.p2z);
        const off = new THREE.Vector3(dim.ox, dim.oy, dim.oz);

        const q1 = p1.clone().add(off);
        const q2 = p2.clone().add(off);

        const group = new THREE.Group();
        group.userData.type = 'linearDim';

        // Dimension line (between offset endpoints)
        group.add(makeDimLine([q1.x, q1.y, q1.z, q2.x, q2.y, q2.z], dimColor));
        // Extension line 1
        group.add(makeDimLine([p1.x, p1.y, p1.z, q1.x, q1.y, q1.z], dimColor));
        // Extension line 2
        group.add(makeDimLine([p2.x, p2.y, p2.z, q2.x, q2.y, q2.z], dimColor));

        // Label at midpoint of dimension line
        const mid = q1.clone().lerp(q2, 0.5);
        const sprite = makeDimSprite(dim.label, dim.isDriven);
        sprite.position.copy(mid);
        // Offset label slightly further from the entity
        sprite.position.addScaledVector(off.clone().normalize(), 0.35);
        sprite.scale.set(1.0, 0.3, 1);
        group.add(sprite);

        return group;
      }

      // Radial dimension: leader line from center to edge + label beyond edge
      function buildRadialDimGroup(dim) {
        const isDark = currentTheme === 'Dark';
        const dimColor = isDark ? 0x7acfff : 0x005a9e;

        const c = new THREE.Vector3(dim.cx, dim.cy, dim.cz);
        const e = new THREE.Vector3(dim.ex, dim.ey, dim.ez);

        const group = new THREE.Group();
        group.userData.type = 'radialDim';

        // Leader line from center to edge point
        group.add(makeDimLine([c.x, c.y, c.z, e.x, e.y, e.z], dimColor));

        // Label slightly beyond the edge point
        const dir = e.clone().sub(c).normalize();
        const labelPos = e.clone().addScaledVector(dir, 0.5);
        const sprite = makeDimSprite(dim.label, dim.isDriven);
        sprite.position.copy(labelPos);
        sprite.scale.set(1.0, 0.3, 1);
        group.add(sprite);

        return group;
      }

      function applySelection() {
        // Clear hover to avoid conflicting emissive state
        hoveredBodyId = null;

        for (const plane of planeMap.values()) {
          const isSelected = plane.userData.entityId === selectedPlaneId;
          const isActiveSketchPlane =
            String(currentMode).toLowerCase() === 'sketch' &&
            String(plane.name || '').toLowerCase() === String(activePlaneName || '').toLowerCase();
          const surface = plane.userData.surfaceMesh;
          if (surface?.material) {
            surface.material.color.setHex(isSelected ? 0x4c7fb7 : plane.userData.baseColor);
            if (isSelected) {
              surface.material.opacity = 0.46;
            } else if (isActiveSketchPlane) {
              surface.material.opacity = 0.38;
            } else {
              surface.material.opacity = String(currentMode).toLowerCase() === 'sketch' ? 0.1 : 0.34;
            }
          }
        }

        for (const mesh of bodyMap.values()) {
          if (mesh.material?.emissive) {
            mesh.material.emissive.setHex(0x000000);
            mesh.material.emissiveIntensity = 0;
          }
        }

        // Reset all edge line colors
        for (const [id, lines] of edgeMap.entries()) {
          if (lines.material) {
            lines.material.color.setHex(bodyEdgeColor());
            lines.material.opacity = 0.45;
          }
        }

        if (String(currentMode).toLowerCase() !== 'sketch') {
          const allSelected = selectedBodyIds.size > 0 ? selectedBodyIds : (selectedBodyId ? new Set([selectedBodyId]) : new Set());
          for (const id of allSelected) {
            const m = bodyMap.get(id);
            if (m?.material?.emissive) {
              m.material.emissive.setHex(0x2b4f7a);
              m.material.emissiveIntensity = 0.18;
            }
            // Accent edge lines on selected body
            const edgeLines = edgeMap.get(id);
            if (edgeLines?.material) {
              edgeLines.material.color.setHex(bodyEdgeColorSelected());
              edgeLines.material.opacity = 0.72;
            }
          }
        }

        const selectedMesh = selectedBodyId ? bodyMap.get(selectedBodyId) : null;
        if (moveToolActive && String(currentMode).toLowerCase() !== 'sketch' && selectedMesh) {
          transformControls.attach(selectedMesh);
          transformControls.visible = true;
        } else {
          transformControls.detach();
          transformControls.visible = false;
        }

        for (const [sketchId, sketchGroup] of sketchMap.entries()) {
          const isSelected = sketchId === selectedSketchId;
          for (const child of sketchGroup.children) {
            if (!child.material?.color) {
              continue;
            }

            const isConstructionChild = child.userData?.isConstruction === true;
            child.material.color.setHex(isConstructionChild
              ? (currentTheme === 'Dark' ? 0x7ab8e0 : 0x3a7bbf)
              : (sketchGroup.userData.isFullyDefined === true || child.userData?.isConstrained === true
                  ? constrainedSketchColor(isSelected)
                  : unconstrainedSketchColor(isSelected)));
            child.material.opacity = sketchGroup.userData.isPreview
              ? 0.72
              : (isConstructionChild ? 0.65 : (sketchGroup.userData.isDraft ? 0.96 : (isSelected ? 1.0 : 0.82)));
          }
        }
      }

      function setScene(state) {
        const nextMode = state?.mode || 'DirectPrimitive';
        const nextPlaneName = state?.activePlaneName || 'Top';
        const modeChanged = String(previousMode) !== String(nextMode);
        const planeChanged = String(previousActivePlaneName) !== String(nextPlaneName);
        currentMode = nextMode;
        if (modeChanged && String(currentMode).toLowerCase() === 'sketch' && moveToolActive) {
          moveToolActive = false;
          postMessage({ type: 'move-tool-deactivated' });
        }
        if (modeChanged && String(currentMode).toLowerCase() !== 'sketch' && sketchTransformToolActive) {
          cancelSketchTransformDrag();
          sketchTransformToolActive = false;
          postMessage({ type: 'sketch-transform-deactivated' });
        }
        if (modeChanged && String(currentMode).toLowerCase() !== 'sketch' && sketchRotateToolActive) {
          cancelSketchRotateDrag();
          sketchRotateToolActive = false;
          postMessage({ type: 'sketch-rotate-deactivated' });
        }
        if (modeChanged) {
          clearFaceHighlight();
          hoveredFacePlane = null;
        }
        activePlaneName = nextPlaneName;
        // Disable orbit on left-drag in sketch mode so clicks go to sketch placement.
        controls.enableRotate = String(nextMode).toLowerCase() !== 'sketch';
        renderer.domElement.style.cursor = String(nextMode).toLowerCase() === 'sketch' ? 'crosshair' : 'default';
        selectedBodyId = state?.selectedBodyId || null;
        selectedPlaneId = state?.selectedPlaneId || null;
        selectedSketchId = state?.selectedSketchId || null;
        rebuildPlanes(state?.planes || []);
        rebuildSketches(state?.sketches || []);
        rebuildBodies(state?.bodies || []);
        rebuildSnapCandidates(state?.sketches || []);
        applySelection();
        const bodyCount = Array.isArray(state?.bodies) ? state.bodies.length : 0;
        const bodyBounds = getSceneBodyBounds();
        if (String(currentMode).toLowerCase() === 'sketch') {
          if (modeChanged || planeChanged) {
            const fp = state?.activeFacePlane;
            if (fp && (fp.nx !== 0 || fp.ny !== 0 || fp.nz !== 0)) {
              applyFacePlaneView(fp, bodyBounds);
            } else {
              applySketchView(activePlaneName, bodyBounds);
            }
          }
        } else if (bodyCount === 0) {
          if (lastBodyCount !== 0 || String(previousMode).toLowerCase() === 'sketch') {
            applyDefaultView();
          }
        } else if (shouldReframe(bodyBounds, bodyCount)) {
          frameBounds(bodyBounds);
        }
        lastBodyCount = bodyCount;
        previousMode = currentMode;
        previousActivePlaneName = activePlaneName;
        renderer.domElement.style.cursor =
          String(currentMode).toLowerCase() === 'sketch' ? 'crosshair' : 'default';
        if (modeChanged && String(currentMode).toLowerCase() !== 'sketch') {
          _sketchToolHint = '';
        }
        const modeName = currentMode;
        setHudText(
          String(currentMode).toLowerCase() === 'sketch'
            ? `Sketch | ${activePlaneName} plane | ${_sketchToolHint || 'Click to place geometry'}`
            : `Mode: ${modeName} | Active plane: ${activePlaneName}`);
        postMessage({
          type: 'scene-applied',
          planeCount: Array.isArray(state?.planes) ? state.planes.length : 0,
          bodyCount
        });
      }

      function setPointer(clientX, clientY) {
        const rect = renderer.domElement.getBoundingClientRect();
        pointer.x = ((clientX - rect.left) / rect.width) * 2 - 1;
        pointer.y = -((clientY - rect.top) / rect.height) * 2 + 1;
      }

      function pick(clientX, clientY) {
        setPointer(clientX, clientY);
        raycaster.setFromCamera(pointer, camera);
        const targets = [...bodyMap.values(), ...planeMap.values()];
        const hits = raycaster.intersectObjects(targets, true);
        for (const hit of hits) {
          const entity = resolveEntity(hit.object);
          if (entity) {
            return { ...hit, entity };
          }
        }

        return null;
      }

      function resolveEntity(object) {
        let current = object;
        while (current) {
          if (current.userData?.type === 'body' || current.userData?.type === 'plane') {
            return current;
          }

          current = current.parent;
        }

        return null;
      }

      let hoveredFacePlane = null;

      function pickFlatFace(clientX, clientY) {
        setPointer(clientX, clientY);
        raycaster.setFromCamera(pointer, camera);
        const bodyTargets = [...bodyMap.values()];
        const hits = raycaster.intersectObjects(bodyTargets, true);
        if (hits.length === 0) return null;

        const hit = hits[0];
        const mesh = hit.object;
        if (!mesh.geometry?.attributes?.position) return null;

        const bodyEntity = resolveEntity(mesh);
        if (!bodyEntity || bodyEntity.userData.type !== 'body') return null;

        const geo = mesh.geometry;
        const pos = geo.attributes.position;
        const idx = geo.index ? geo.index.array : null;
        const triCount = idx ? idx.length / 3 : pos.count / 3;

        // Get hit face normal in world space
        const hitFaceNormal = hit.face.normal.clone().transformDirection(mesh.matrixWorld).normalize();

        // Collect all triangles whose normals are within ~2° of the hit face normal
        const dotThreshold = Math.cos(2 * Math.PI / 180);
        const faceVerts = [];
        const tmpN = new THREE.Vector3();
        const vA = new THREE.Vector3();
        const vB = new THREE.Vector3();
        const vC = new THREE.Vector3();
        const edge1 = new THREE.Vector3();
        const edge2 = new THREE.Vector3();

        for (let i = 0; i < triCount; i++) {
          const ia = idx ? idx[i * 3] : i * 3;
          const ib = idx ? idx[i * 3 + 1] : i * 3 + 1;
          const ic = idx ? idx[i * 3 + 2] : i * 3 + 2;

          vA.fromBufferAttribute(pos, ia).applyMatrix4(mesh.matrixWorld);
          vB.fromBufferAttribute(pos, ib).applyMatrix4(mesh.matrixWorld);
          vC.fromBufferAttribute(pos, ic).applyMatrix4(mesh.matrixWorld);

          edge1.subVectors(vB, vA);
          edge2.subVectors(vC, vA);
          tmpN.crossVectors(edge1, edge2).normalize();

          if (tmpN.dot(hitFaceNormal) >= dotThreshold) {
            faceVerts.push(vA.clone(), vB.clone(), vC.clone());
          }
        }

        if (faceVerts.length < 3) return null;

        // Compute centroid (origin) of the flat face
        const origin = new THREE.Vector3();
        for (const v of faceVerts) origin.add(v);
        origin.divideScalar(faceVerts.length);

        // Compute U axis: project first edge onto face plane, normalise
        const uAxis = new THREE.Vector3().subVectors(faceVerts[1], faceVerts[0]);
        uAxis.addScaledVector(hitFaceNormal, -uAxis.dot(hitFaceNormal)).normalize();
        if (uAxis.length() < 0.001) {
          // Fallback: use world X projected onto face
          uAxis.set(1, 0, 0);
          uAxis.addScaledVector(hitFaceNormal, -uAxis.dot(hitFaceNormal)).normalize();
        }

        const vAxis = new THREE.Vector3().crossVectors(hitFaceNormal, uAxis).normalize();

        return {
          bodyId: bodyEntity.userData.entityId,
          nx: hitFaceNormal.x, ny: hitFaceNormal.y, nz: hitFaceNormal.z,
          ox: origin.x, oy: origin.y, oz: origin.z,
          ux: uAxis.x, uy: uAxis.y, uz: uAxis.z,
          vx: vAxis.x, vy: vAxis.y, vz: vAxis.z,
          faceVerts: faceVerts
        };
      }

      function highlightFace(facePlane, color) {
        if (!facePlane) return;
        // Build a small overlay mesh from the face triangles for highlighting
        if (window._faceHighlightMesh) {
          scene.remove(window._faceHighlightMesh);
          window._faceHighlightMesh.geometry.dispose();
          window._faceHighlightMesh = null;
        }
        const verts = facePlane.faceVerts;
        if (!verts || verts.length < 3) return;
        const geom = new THREE.BufferGeometry();
        const positions = new Float32Array(verts.length * 3);
        for (let i = 0; i < verts.length; i++) {
          positions[i * 3] = verts[i].x;
          positions[i * 3 + 1] = verts[i].y;
          positions[i * 3 + 2] = verts[i].z;
        }
        geom.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        const mat = new THREE.MeshBasicMaterial({ color: color, transparent: true, opacity: 0.3, side: THREE.DoubleSide, depthTest: false });
        const m = new THREE.Mesh(geom, mat);
        m.renderOrder = 5;
        scene.add(m);
        window._faceHighlightMesh = m;
      }

      function clearFaceHighlight() {
        if (window._faceHighlightMesh) {
          scene.remove(window._faceHighlightMesh);
          window._faceHighlightMesh.geometry.dispose();
          window._faceHighlightMesh = null;
        }
      }

      function resolveSketchPlane() {
        if (selectedPlaneId && planeMap.has(selectedPlaneId)) {
          return planeMap.get(selectedPlaneId);
        }

        for (const plane of planeMap.values()) {
          if (String(plane.name || '').toLowerCase() === String(activePlaneName || 'Top').toLowerCase()) {
            return plane;
          }
        }

        return null;
      }

      function tryGetSketchLocal(clientX, clientY) {
        const sketchPlane = resolveSketchPlane();
        if (!sketchPlane) {
          return null;
        }

        setPointer(clientX, clientY);
        raycaster.setFromCamera(pointer, camera);
        const hitPoint = new THREE.Vector3();
        const planeNormal = new THREE.Vector3();
        switch (String(sketchPlane.userData.kind || '').toLowerCase()) {
          case 'front':
            planeNormal.set(1, 0, 0);
            break;
          case 'right':
            planeNormal.set(0, 1, 0);
            break;
          default:
            planeNormal.set(0, 0, 1);
            break;
        }

        const infinitePlane = new THREE.Plane().setFromNormalAndCoplanarPoint(planeNormal, new THREE.Vector3(0, 0, 0));
        if (!raycaster.ray.intersectPlane(infinitePlane, hitPoint)) {
          return null;
        }

        const local = worldToPlanePoint(sketchPlane.userData.kind, hitPoint);
        return {
          planeId: sketchPlane.userData.entityId,
          planeKind: sketchPlane.userData.kind,
          local
        };
      }

      function worldToPlanePoint(kind, point) {
        switch (String(kind || '').toLowerCase()) {
          case 'front':
            return { u: round3(point.y), v: round3(point.z) };
          case 'right':
            return { u: round3(point.x), v: round3(point.z) };
          default:
            return { u: round3(point.x), v: round3(point.y) };
        }
      }

      function focusCameraAt(point) {
        const nextTarget = point.clone();
        const delta = nextTarget.clone().sub(controls.target);
        camera.position.add(delta);
        controls.target.copy(nextTarget);
        controls.update();
      }

      function focusSelection() {
        if (selectedBodyId && bodyMap.has(selectedBodyId)) {
          const mesh = bodyMap.get(selectedBodyId);
          const bounds = new THREE.Box3().setFromObject(mesh);
          if (!bounds.isEmpty()) {
            frameBounds(bounds, 1.45);
            return true;
          }
        }

        if (selectedPlaneId && planeMap.has(selectedPlaneId)) {
          applyDefaultView();
          return true;
        }

        applyDefaultView();
        return true;
      }

      function hideContextMenu() {
        menu.style.display = 'none';
      }

      window.my3dappSetScene = (state) => {
        setScene(state);
        return true;
      };

      window.my3dappSetTheme = (themeName) => {
        applyTheme(themeName);
        return true;
      };

      window.my3dappFocusSelection = () => {
        return focusSelection();
      };

      let _navButtonDown = false;
      window.my3dappSetMoveTool = (active) => {
        moveToolActive = Boolean(active);
        applySelection();
        return true;
      };

      window.my3dappSetSketchTransformTool = (active) => {
        sketchTransformToolActive = Boolean(active);
        if (!sketchTransformToolActive) {
          cancelSketchTransformDrag();
        }
        return true;
      };

      window.my3dappSetBoxSelection = (idsArray, append) => {
        if (!append) {
          selectedBodyIds.clear();
          selectedBodyId = null;
          selectedPlaneId = null;
        }
        for (const id of (idsArray || [])) {
          selectedBodyIds.add(id);
        }
        if (selectedBodyIds.size === 1) {
          selectedBodyId = [...selectedBodyIds][0];
          selectedBodyIds.clear();
        }
        applySelection();
        return true;
      };

      window.my3dappSetExtrudePreview = (body) => {
        clearExtrudePreview();
        if (!body || !body.positions || !body.indices || body.positions.length === 0) return true;
        const geo = new THREE.BufferGeometry();
        geo.setAttribute('position', new THREE.BufferAttribute(new Float32Array(body.positions), 3));
        geo.setIndex(new THREE.BufferAttribute(new Uint32Array(body.indices), 1));
        geo.computeVertexNormals();
        const mat = new THREE.MeshStandardMaterial({
          color: 0x3399ff,
          transparent: true,
          opacity: 0.38,
          depthWrite: false,
          side: THREE.DoubleSide
        });
        extrudePreviewMesh = new THREE.Mesh(geo, mat);
        extrudePreviewMesh.position.set(body.x || 0, body.y || 0, body.z || 0);
        scene.add(extrudePreviewMesh);
        const edgeMat = new THREE.LineBasicMaterial({ color: 0x1177cc, transparent: true, opacity: 0.55 });
        extrudePreviewEdges = new THREE.LineSegments(new THREE.EdgesGeometry(geo), edgeMat);
        extrudePreviewEdges.position.copy(extrudePreviewMesh.position);
        scene.add(extrudePreviewEdges);
        return true;
      };

      window.my3dappClearExtrudePreview = () => {
        clearExtrudePreview();
        return true;
      };

      function clearExtrudePreview() {
        if (extrudePreviewMesh) { scene.remove(extrudePreviewMesh); extrudePreviewMesh.geometry.dispose(); extrudePreviewMesh = null; }
        if (extrudePreviewEdges) { scene.remove(extrudePreviewEdges); extrudePreviewEdges.geometry.dispose(); extrudePreviewEdges = null; }
      }

      window.my3dappSetSketchRotateTool = (active) => {
        sketchRotateToolActive = Boolean(active);
        if (!sketchRotateToolActive) {
          cancelSketchRotateDrag();
        }
        return true;
      };

      function cancelSketchRotateDrag() {
        if (sketchRotateDragging && sketchRotateTargetId) {
          const group = sketchMap.get(sketchRotateTargetId);
          if (group) {
            group.rotation.z = 0;
          }
        }
        sketchRotateDragging = false;
        sketchRotateTargetId = null;
        sketchRotatePivotUV = null;
        sketchRotateStartAngle = null;
        sketchRotateCurrentAngle = null;
        sketchRotateAccumDeg = 0;
      }

      function commitSketchRotateDrag() {
        if (!sketchRotateDragging || !sketchRotateTargetId || sketchRotateAccumDeg === 0) {
          cancelSketchRotateDrag();
          return;
        }
        const sketchId = sketchRotateTargetId;
        const planeKind = sketchRotateTargetKind;
        const pivotU = sketchRotatePivotUV ? sketchRotatePivotUV.u : 0;
        const pivotV = sketchRotatePivotUV ? sketchRotatePivotUV.v : 0;
        const angleDeg = round3(sketchRotateAccumDeg);
        cancelSketchRotateDrag();
        if (Math.abs(angleDeg) > 0.01) {
          postMessage({
            type: 'sketch-entity-rotate-committed',
            sketchId: sketchId,
            planeKind: planeKind,
            pivotU: round3(pivotU),
            pivotV: round3(pivotV),
            angleDeg: angleDeg
          });
        }
      }

      window.my3dappSetGridVisible = (visible) => {
        gridVisible = Boolean(visible);
        return true;
      };

      function cancelSketchTransformDrag() {
        if (sketchTransformDragging && sketchTransformTargetId) {
          const group = sketchMap.get(sketchTransformTargetId);
          if (group) {
            group.position.set(0, 0, 0);
          }
        }
        sketchTransformDragging = false;
        sketchTransformTargetId = null;
        sketchTransformStartUV = null;
        sketchTransformCurrentUV = null;
      }

      function commitSketchTransformDrag() {
        if (!sketchTransformDragging || !sketchTransformTargetId || !sketchTransformStartUV || !sketchTransformCurrentUV) {
          cancelSketchTransformDrag();
          return;
        }
        const du = round3(sketchTransformCurrentUV.u - sketchTransformStartUV.u);
        const dv = round3(sketchTransformCurrentUV.v - sketchTransformStartUV.v);
        const sketchId = sketchTransformTargetId;
        const planeKind = sketchTransformTargetKind;
        cancelSketchTransformDrag();
        if (Math.abs(du) > 0.001 || Math.abs(dv) > 0.001) {
          postMessage({
            type: 'sketch-entity-transform-committed',
            sketchId: sketchId,
            planeKind: planeKind,
            du: du,
            dv: dv
          });
        }
      }

      function showBoxSelect(x1, y1, x2, y2) {
        const left = Math.min(x1, x2);
        const top = Math.min(y1, y2);
        const width = Math.abs(x2 - x1);
        const height = Math.abs(y2 - y1);
        boxSelectOverlay.style.left = left + 'px';
        boxSelectOverlay.style.top = top + 'px';
        boxSelectOverlay.style.width = width + 'px';
        boxSelectOverlay.style.height = height + 'px';
        boxSelectOverlay.style.display = 'block';
        if (x2 < x1) {
          boxSelectOverlay.classList.add('crossing');
        } else {
          boxSelectOverlay.classList.remove('crossing');
        }
      }

      function hideBoxSelect() {
        boxSelectOverlay.style.display = 'none';
        boxSelectActive = false;
      }

      function commitBoxSelect(shiftHeld) {
        const minX = Math.min(boxSelectStartX, boxSelectEndX);
        const maxX = Math.max(boxSelectStartX, boxSelectEndX);
        const minY = Math.min(boxSelectStartY, boxSelectEndY);
        const maxY = Math.max(boxSelectStartY, boxSelectEndY);
        const isCrossing = boxSelectEndX < boxSelectStartX;
        const rect = renderer.domElement.getBoundingClientRect();
        const ids = [];

        for (const [id, mesh] of bodyMap.entries()) {
          const box3 = new THREE.Box3().setFromObject(mesh);
          const corners = [
            new THREE.Vector3(box3.min.x, box3.min.y, box3.min.z),
            new THREE.Vector3(box3.max.x, box3.min.y, box3.min.z),
            new THREE.Vector3(box3.min.x, box3.max.y, box3.min.z),
            new THREE.Vector3(box3.max.x, box3.max.y, box3.min.z),
            new THREE.Vector3(box3.min.x, box3.min.y, box3.max.z),
            new THREE.Vector3(box3.max.x, box3.min.y, box3.max.z),
            new THREE.Vector3(box3.min.x, box3.max.y, box3.max.z),
            new THREE.Vector3(box3.max.x, box3.max.y, box3.max.z)
          ];
          let scrMinX = Infinity, scrMaxX = -Infinity, scrMinY = Infinity, scrMaxY = -Infinity;
          for (const c of corners) {
            const proj = c.clone().project(camera);
            const sx = (proj.x + 1) / 2 * rect.width + rect.left;
            const sy = (-proj.y + 1) / 2 * rect.height + rect.top;
            scrMinX = Math.min(scrMinX, sx); scrMaxX = Math.max(scrMaxX, sx);
            scrMinY = Math.min(scrMinY, sy); scrMaxY = Math.max(scrMaxY, sy);
          }
          if (isCrossing) {
            if (scrMinX <= maxX && scrMaxX >= minX && scrMinY <= maxY && scrMaxY >= minY) {
              ids.push(id);
            }
          } else {
            if (scrMinX >= minX && scrMaxX <= maxX && scrMinY >= minY && scrMaxY <= maxY) {
              ids.push(id);
            }
          }
        }

        hideBoxSelect();
        if (ids.length === 0) return;
        postMessage({ type: 'box-select-result', ids: ids, append: Boolean(shiftHeld) });
      }

      function tryGetSketchGroupAtPointer(clientX, clientY) {
        setPointer(clientX, clientY);
        raycaster.setFromCamera(pointer, camera);
        raycaster.params.Line = { threshold: 0.6 };
        const sketchTargets = [...sketchMap.values()].filter(g => !g.userData.isPreview && !g.userData.isDraft);
        const hits = raycaster.intersectObjects(sketchTargets, true);
        for (const hit of hits) {
          let cur = hit.object;
          while (cur) {
            if (cur.userData && cur.userData.type === 'sketch') {
              return cur;
            }
            cur = cur.parent;
          }
        }
        return null;
      }

      function worldToPlaneUV(planeName, worldPos) {
        switch (String(planeName || '').toLowerCase()) {
          case 'front': return { u: round3(worldPos.y), v: round3(worldPos.z) };
          case 'right': return { u: round3(worldPos.x), v: round3(worldPos.z) };
          default: return { u: round3(worldPos.x), v: round3(worldPos.y) };
        }
      }

      function planeUVToWorldOffset(planeName, du, dv) {
        switch (String(planeName || '').toLowerCase()) {
          case 'front': return new THREE.Vector3(0, du, dv);
          case 'right': return new THREE.Vector3(du, 0, dv);
          default: return new THREE.Vector3(du, dv, 0);
        }
      }

      function uvToWorld(planeName, u, v) {
        switch (String(planeName || '').toLowerCase()) {
          case 'front': return new THREE.Vector3(0, u, v);
          case 'right': return new THREE.Vector3(u, 0, v);
          default: return new THREE.Vector3(u, v, 0);
        }
      }

      function worldToScreen(worldPos) {
        if (!worldPos) return null;
        const projected = worldPos.clone().project(camera);
        if (projected.z < -1 || projected.z > 1) return null;
        return {
          x: (projected.x + 1) * 0.5 * window.innerWidth,
          y: (-projected.y + 1) * 0.5 * window.innerHeight
        };
      }

      function getCurrentGridSpacing() {
        const s0 = worldToScreen(uvToWorld(activePlaneName, 0, 0));
        const s1 = worldToScreen(uvToWorld(activePlaneName, 1, 0));
        const pxPerUnit = (s0 && s1)
          ? Math.sqrt((s1.x - s0.x) ** 2 + (s1.y - s0.y) ** 2)
          : 20;
        const spacings = [0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500];
        for (const s of spacings) {
          if (pxPerUnit * s >= 40) return s;
        }
        return 500;
      }

      function drawGrid() {
        if (!snapCtx || !snapOverlay) return;
        const planeName = activePlaneName;
        const isDark = currentTheme === 'Dark';
        // Determine region in UV space: sketch entity bounds + margin, or default 20×20 at origin.
        let regionMinU, regionMaxU, regionMinV, regionMaxV;
        if (sketchEntityBoundsUV) {
          const rangeU = Math.max(1, sketchEntityBoundsUV.maxU - sketchEntityBoundsUV.minU);
          const rangeV = Math.max(1, sketchEntityBoundsUV.maxV - sketchEntityBoundsUV.minV);
          const marginU = Math.max(5, rangeU * 0.5);
          const marginV = Math.max(5, rangeV * 0.5);
          regionMinU = sketchEntityBoundsUV.minU - marginU;
          regionMaxU = sketchEntityBoundsUV.maxU + marginU;
          regionMinV = sketchEntityBoundsUV.minV - marginV;
          regionMaxV = sketchEntityBoundsUV.maxV + marginV;
        } else {
          regionMinU = -10; regionMaxU = 10;
          regionMinV = -10; regionMaxV = 10;
        }
        // Pixels per UV unit
        const s0 = worldToScreen(uvToWorld(planeName, 0, 0));
        const s1 = worldToScreen(uvToWorld(planeName, 1, 0));
        if (!s0 || !s1) return;
        const pxPerUnit = Math.hypot(s1.x - s0.x, s1.y - s0.y);
        if (pxPerUnit < 0.1) return;
        // Choose grid spacing (40-80 px between lines)
        const spacings = [0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500];
        let spacing = spacings[spacings.length - 1];
        for (const s of spacings) { if (pxPerUnit * s >= 40) { spacing = s; break; } }
        const startU = Math.ceil(regionMinU / spacing) * spacing;
        const startV = Math.ceil(regionMinV / spacing) * spacing;
        const centerU = (regionMinU + regionMaxU) * 0.5;
        const centerV = (regionMinV + regionMaxV) * 0.5;
        const halfRangeU = Math.max(0.001, (regionMaxU - regionMinU) * 0.5);
        const halfRangeV = Math.max(0.001, (regionMaxV - regionMinV) * 0.5);
        snapCtx.save();
        snapCtx.lineWidth = 1;
        // V-axis lines (constant U)
        for (let u = startU; u <= regionMaxU + 0.001; u += spacing) {
          const distFrac = Math.abs(u - centerU) / halfRangeU;
          const fade = Math.max(0, 1 - Math.pow(Math.min(distFrac, 1), 1.4));
          if (fade < 0.02) continue;
          const isOriginLine = Math.abs(u) < spacing * 0.01;
          const alpha = (isOriginLine ? 0.22 : 0.10) * fade * (isDark ? 1.2 : 1.0);
          snapCtx.strokeStyle = isDark
            ? `rgba(200,215,240,${alpha.toFixed(3)})`
            : `rgba(50,70,100,${alpha.toFixed(3)})`;
          const pa = worldToScreen(uvToWorld(planeName, u, regionMinV));
          const pb = worldToScreen(uvToWorld(planeName, u, regionMaxV));
          if (!pa || !pb) continue;
          snapCtx.beginPath(); snapCtx.moveTo(pa.x, pa.y); snapCtx.lineTo(pb.x, pb.y); snapCtx.stroke();
        }
        // U-axis lines (constant V)
        for (let v = startV; v <= regionMaxV + 0.001; v += spacing) {
          const distFrac = Math.abs(v - centerV) / halfRangeV;
          const fade = Math.max(0, 1 - Math.pow(Math.min(distFrac, 1), 1.4));
          if (fade < 0.02) continue;
          const isOriginLine = Math.abs(v) < spacing * 0.01;
          const alpha = (isOriginLine ? 0.22 : 0.10) * fade * (isDark ? 1.2 : 1.0);
          snapCtx.strokeStyle = isDark
            ? `rgba(200,215,240,${alpha.toFixed(3)})`
            : `rgba(50,70,100,${alpha.toFixed(3)})`;
          const pa = worldToScreen(uvToWorld(planeName, regionMinU, v));
          const pb = worldToScreen(uvToWorld(planeName, regionMaxU, v));
          if (!pa || !pb) continue;
          snapCtx.beginPath(); snapCtx.moveTo(pa.x, pa.y); snapCtx.lineTo(pb.x, pb.y); snapCtx.stroke();
        }
        snapCtx.restore();
      }

      function rebuildSnapCandidates(sketches) {
        snapCandidates = [];
        let minU = Infinity, maxU = -Infinity, minV = Infinity, maxV = -Infinity;
        for (const sketch of sketches || []) {
          if (sketch.isPreview) continue;
          const planeName = String(sketch.planeName || activePlaneName || 'Top');
          for (const curve of sketch.curves || []) {
            const pts = curve.points;
            if (!pts || pts.length < 6) continue;
            const nPts = Math.floor(pts.length / 3);
            if (nPts < 2) continue;
            snapCandidates.push({ type: 'endpoint', world: new THREE.Vector3(pts[0], pts[1], pts[2]) });
            snapCandidates.push({ type: 'endpoint', world: new THREE.Vector3(pts[(nPts - 1) * 3], pts[(nPts - 1) * 3 + 1], pts[(nPts - 1) * 3 + 2]) });
            const mid = Math.floor(nPts / 2);
            snapCandidates.push({ type: 'midpoint', world: new THREE.Vector3(pts[mid * 3], pts[mid * 3 + 1], pts[mid * 3 + 2]) });
            if (curve.closed) {
              let cx = 0, cy = 0, cz = 0;
              for (let i = 0; i < nPts; i++) { cx += pts[i * 3]; cy += pts[i * 3 + 1]; cz += pts[i * 3 + 2]; }
              snapCandidates.push({ type: 'center', world: new THREE.Vector3(cx / nPts, cy / nPts, cz / nPts) });
            }
            // Track UV bounding box for region-based grid
            for (let i = 0; i < nPts; i++) {
              const wp = { x: pts[i * 3], y: pts[i * 3 + 1], z: pts[i * 3 + 2] };
              const uv = worldToPlaneUV(planeName, wp);
              if (uv.u < minU) minU = uv.u; if (uv.u > maxU) maxU = uv.u;
              if (uv.v < minV) minV = uv.v; if (uv.v > maxV) maxV = uv.v;
            }
          }
        }
        sketchEntityBoundsUV = (minU <= maxU && minV <= maxV)
          ? { minU, maxU, minV, maxV }
          : null;

        sketchEntitySegments = [];
        for (const sketch of sketches || []) {
          if (sketch.isPreview || sketch.isDraft) continue;
          for (const curve of sketch.curves || []) {
            const pts = curve.points;
            if (!pts || pts.length < 6 || !curve.entityId) continue;
            const nPts = Math.floor(pts.length / 3);
            const worldPts = [];
            for (let i = 0; i < nPts; i++) {
              worldPts.push(new THREE.Vector3(pts[i * 3], pts[i * 3 + 1], pts[i * 3 + 2]));
            }
            sketchEntitySegments.push({
              entityId: curve.entityId,
              entityType: curve.entityType || '',
              worldPts
            });
          }
        }
      }

      function findNearestSketchEntity(screenX, screenY) {
        const HIT_RADIUS = 8;
        let bestId = null;
        let bestType = '';
        let bestDist = HIT_RADIUS + 1;
        for (const seg of sketchEntitySegments) {
          const screenPts = seg.worldPts.map(w => worldToScreen(w)).filter(Boolean);
          if (screenPts.length < 2) continue;
          for (let i = 0; i < screenPts.length - 1; i++) {
            const ax = screenPts[i].x, ay = screenPts[i].y;
            const bx = screenPts[i + 1].x, by = screenPts[i + 1].y;
            const dx = bx - ax, dy = by - ay;
            const lenSq = dx * dx + dy * dy;
            let d;
            if (lenSq < 0.001) {
              d = Math.sqrt((screenX - ax) ** 2 + (screenY - ay) ** 2);
            } else {
              const t = Math.max(0, Math.min(1, ((screenX - ax) * dx + (screenY - ay) * dy) / lenSq));
              d = Math.sqrt((screenX - (ax + t * dx)) ** 2 + (screenY - (ay + t * dy)) ** 2);
            }
            if (d < bestDist) { bestDist = d; bestId = seg.entityId; bestType = seg.entityType; }
          }
        }
        return bestDist <= HIT_RADIUS ? { entityId: bestId, entityType: bestType } : null;
      }

      function computeSnap(screenX, screenY) {
        let best = null;
        let bestDist = SNAP_RADIUS + 1;
        for (const cand of snapCandidates) {
          const s = worldToScreen(cand.world);
          if (!s) continue;
          const d = Math.sqrt((s.x - screenX) ** 2 + (s.y - screenY) ** 2);
          if (d < bestDist) {
            bestDist = d;
            const uv = worldToPlaneUV(activePlaneName, cand.world);
            best = { type: cand.type, sx: s.x, sy: s.y, u: round3(uv.u), v: round3(uv.v), dist: d };
          }
        }
        if (gridVisible && bestDist > SNAP_RADIUS) {
          const spacing = getCurrentGridSpacing();
          const hit = tryGetSketchLocal(screenX, screenY);
          if (hit) {
            const snapU = round3(Math.round(hit.local.u / spacing) * spacing);
            const snapV = round3(Math.round(hit.local.v / spacing) * spacing);
            const snapWorld = uvToWorld(activePlaneName, snapU, snapV);
            const snapScreen = worldToScreen(snapWorld);
            if (snapScreen) {
              const d = Math.sqrt((snapScreen.x - screenX) ** 2 + (snapScreen.y - screenY) ** 2);
              if (d <= SNAP_RADIUS && (best === null || d < best.dist)) {
                best = { type: 'grid', sx: snapScreen.x, sy: snapScreen.y, u: snapU, v: snapV, dist: d };
              }
            }
          }
        }
        return bestDist <= SNAP_RADIUS ? best : null;
      }

      const _snapLabelMap = {
        endpoint: 'Endpoint', midpoint: 'Midpoint', center: 'Center',
        intersection: 'Intersection', oncurve: 'On Curve'
      };

      function drawSnapLabel(snapCtxL, sx, sy, label, isDark) {
        if (!label) return;
        const lx = sx + 14;
        const ly = sy - 12;
        const pad = 4;
        snapCtxL.save();
        snapCtxL.font = '11px Segoe UI, sans-serif';
        const tw = snapCtxL.measureText(label).width;
        snapCtxL.fillStyle   = isDark ? 'rgba(18,28,42,0.85)' : 'rgba(247,250,253,0.90)';
        snapCtxL.strokeStyle = isDark ? '#3a6090' : '#b8c8da';
        snapCtxL.lineWidth = 0.8;
        snapCtxL.setLineDash([]);
        snapCtxL.beginPath();
        snapCtxL.roundRect(lx - pad, ly - 9, tw + pad * 2, 18, 3);
        snapCtxL.fill();
        snapCtxL.stroke();
        snapCtxL.fillStyle = isDark ? '#9ec8ec' : '#1a3850';
        snapCtxL.textBaseline = 'middle';
        snapCtxL.fillText(label, lx, ly);
        snapCtxL.restore();
      }

      function drawSnapMarker(snap) {
        if (!snapCtx || !snap) return;
        const { type, sx, sy } = snap;
        const isDark = currentTheme === 'Dark';
        const color = isDark ? '#7fc8ff' : '#0078d4';
        snapCtx.save();
        snapCtx.strokeStyle = color;
        snapCtx.fillStyle = color;
        snapCtx.lineWidth = 1.5;
        snapCtx.setLineDash([]);
        switch (type) {
          case 'endpoint': {
            const hs = 5;
            snapCtx.strokeRect(sx - hs, sy - hs, hs * 2, hs * 2);
            break;
          }
          case 'midpoint': {
            snapCtx.beginPath();
            snapCtx.moveTo(sx, sy - 6);
            snapCtx.lineTo(sx + 5.5, sy + 4);
            snapCtx.lineTo(sx - 5.5, sy + 4);
            snapCtx.closePath();
            snapCtx.stroke();
            break;
          }
          case 'center': {
            snapCtx.beginPath();
            snapCtx.moveTo(sx - 6, sy); snapCtx.lineTo(sx + 6, sy);
            snapCtx.moveTo(sx, sy - 6); snapCtx.lineTo(sx, sy + 6);
            snapCtx.stroke();
            break;
          }
          case 'grid': {
            snapCtx.globalAlpha = 0.60;
            snapCtx.strokeStyle = isDark ? '#b0c8e8' : '#4a7aaa';
            snapCtx.lineWidth = 1;
            snapCtx.beginPath();
            snapCtx.moveTo(sx - 7, sy); snapCtx.lineTo(sx + 7, sy);
            snapCtx.moveTo(sx, sy - 7); snapCtx.lineTo(sx, sy + 7);
            snapCtx.stroke();
            break;
          }
          case 'intersection': {
            const r = 5.5;
            snapCtx.beginPath();
            snapCtx.moveTo(sx - r, sy - r); snapCtx.lineTo(sx + r, sy + r);
            snapCtx.moveTo(sx + r, sy - r); snapCtx.lineTo(sx - r, sy + r);
            snapCtx.stroke();
            break;
          }
          case 'oncurve': {
            snapCtx.beginPath();
            snapCtx.arc(sx, sy, 4.5, 0, Math.PI * 2);
            snapCtx.stroke();
            break;
          }
        }
        snapCtx.restore();

        // Inference label (skip grid — it's obvious)
        if (type !== 'grid') {
          drawSnapLabel(snapCtx, sx, sy, _snapLabelMap[type] ?? null, isDark);
        }
      }

      function drawAlignmentHints(cursorSx, cursorSy) {
        if (!snapCtx || cursorSx < -100 || cursorSy < -100) return;
        if (snapCandidates.length === 0) return;
        const isDark = currentTheme === 'Dark';
        const hColor  = isDark ? 'rgba(120,200,255,0.55)' : 'rgba(0,120,210,0.45)';
        const vColor  = isDark ? 'rgba(120,200,255,0.55)' : 'rgba(0,120,210,0.45)';

        let drewH = false, drewV = false;
        for (const cand of snapCandidates) {
          if (drewH && drewV) break;
          const s = worldToScreen(cand.world);
          if (!s) continue;
          if (!drewH && Math.abs(s.y - cursorSy) < ALIGN_TOL) {
            // Horizontal alignment line
            snapCtx.save();
            snapCtx.strokeStyle = hColor;
            snapCtx.lineWidth = 0.8;
            snapCtx.setLineDash([4, 4]);
            snapCtx.beginPath();
            snapCtx.moveTo(s.x, s.y);
            snapCtx.lineTo(cursorSx, s.y);
            snapCtx.stroke();
            snapCtx.restore();
            drawSnapLabel(snapCtx, Math.min(s.x, cursorSx) + Math.abs(cursorSx - s.x) * 0.5 - 22,
              s.y - 2, 'Horizontal', isDark);
            drewH = true;
          }
          if (!drewV && Math.abs(s.x - cursorSx) < ALIGN_TOL) {
            // Vertical alignment line
            snapCtx.save();
            snapCtx.strokeStyle = vColor;
            snapCtx.lineWidth = 0.8;
            snapCtx.setLineDash([4, 4]);
            snapCtx.beginPath();
            snapCtx.moveTo(s.x, s.y);
            snapCtx.lineTo(s.x, cursorSy);
            snapCtx.stroke();
            snapCtx.restore();
            drawSnapLabel(snapCtx, s.x + 6,
              Math.min(s.y, cursorSy) + Math.abs(cursorSy - s.y) * 0.5 - 9, 'Vertical', isDark);
            drewV = true;
          }
        }
      }

      function drawOverlay() {
        if (!snapCtx || !snapOverlay) return;
        snapCtx.clearRect(0, 0, snapOverlay.width, snapOverlay.height);
        if (String(currentMode).toLowerCase() !== 'sketch') return;
        if (gridVisible) drawGrid();
        if (!currentSnap) {
          drawAlignmentHints(_sketchCursorSx, _sketchCursorSy);
        }
        if (currentSnap) drawSnapMarker(currentSnap);
      }

      renderer.domElement.addEventListener('pointerdown', (event) => {
        if (event.button !== 0) {
          if (event.button === 1 || event.button === 2) {
            _navButtonDown = true;
            renderer.domElement.style.cursor = 'grabbing';
          }
          return;
        }

        if (menu.style.display === 'block') {
          hideContextMenu();
        }

        if (transformControls.axis) {
          return;
        }

        if (String(currentMode).toLowerCase() === 'sketch') {
          if (sketchTransformToolActive) {
            const hitGroup = tryGetSketchGroupAtPointer(event.clientX, event.clientY);
            if (hitGroup) {
              sketchTransformDragging = true;
              sketchTransformTargetId = hitGroup.userData.entityId;
              const planeGroup = resolveSketchPlane();
              sketchTransformTargetKind = planeGroup ? (planeGroup.userData.kind || 'Top') : 'Top';
              const sketchHit = tryGetSketchLocal(event.clientX, event.clientY);
              sketchTransformStartUV = sketchHit ? { u: sketchHit.local.u, v: sketchHit.local.v } : { u: 0, v: 0 };
              sketchTransformCurrentUV = { ...sketchTransformStartUV };
              hitGroup.position.set(0, 0, 0);
              renderer.domElement.style.cursor = 'grabbing';
              return;
            }
            return;
          }
          if (sketchRotateToolActive) {
            const hitGroup = tryGetSketchGroupAtPointer(event.clientX, event.clientY);
            if (hitGroup) {
              const planeGroup = resolveSketchPlane();
              sketchRotateDragging = true;
              sketchRotateTargetId = hitGroup.userData.entityId;
              sketchRotateTargetKind = planeGroup ? (planeGroup.userData.kind || 'Top') : 'Top';
              const sketchHit = tryGetSketchLocal(event.clientX, event.clientY);
              if (sketchHit) {
                sketchRotatePivotUV = { u: sketchHit.local.u, v: sketchHit.local.v };
                sketchRotateStartAngle = Math.atan2(0, 1);
              }
              sketchRotateCurrentAngle = sketchRotateStartAngle;
              sketchRotateAccumDeg = 0;
              hitGroup.rotation.z = 0;
              renderer.domElement.style.cursor = 'alias';
              return;
            }
            return;
          }
          const sketchHit = tryGetSketchLocal(event.clientX, event.clientY);
          if (sketchHit) {
            const placementSnap = computeSnap(event.clientX, event.clientY);
            const placeU = placementSnap ? placementSnap.u : sketchHit.local.u;
            const placeV = placementSnap ? placementSnap.v : sketchHit.local.v;
            postMessage({
              type: 'sketch-point',
              planeId: sketchHit.planeId,
              planeKind: sketchHit.planeKind,
              u: placeU,
              v: placeV
            });
            setHudText(`Sketch point: ${placeU}, ${placeV}`);
            return;
          }
        }

        const hit = pick(event.clientX, event.clientY);
        if (!hit) {
          // In measure mode a missed click clears the measure state
          if (isMeasureMode) {
            measurePtA = null;
            setHudText('Measure: click a body surface for first point');
            return;
          }
          boxSelectActive = true;
          boxSelectStartX = event.clientX;
          boxSelectStartY = event.clientY;
          boxSelectEndX = event.clientX;
          boxSelectEndY = event.clientY;
          return;
        }

        // Measurement mode intercepts body clicks
        if (isMeasureMode && hit.point) {
          if (!measurePtA) {
            measurePtA = hit.point.clone();
            setHudText(`Measure: A = (${r3(measurePtA.x)}, ${r3(measurePtA.y)}, ${r3(measurePtA.z)})  — click second point`);
          } else {
            const ptB = hit.point;
            const dist = measurePtA.distanceTo(ptB);
            setHudText(`Distance: ${dist.toFixed(3)} mm  |  A=(${r3(measurePtA.x)},${r3(measurePtA.y)},${r3(measurePtA.z)})  B=(${r3(ptB.x)},${r3(ptB.y)},${r3(ptB.z)})`);
            postMessage({ type: 'measure-result', distance: round3(dist),
              ax: round3(measurePtA.x), ay: round3(measurePtA.y), az: round3(measurePtA.z),
              bx: round3(ptB.x), by: round3(ptB.y), bz: round3(ptB.z) });
            measurePtA = null; // ready for next pair
          }
          return;
        }

        if (hit.entity.userData.type === 'body') {
          const clickedId = hit.entity.userData.entityId;
          selectedPlaneId = null;

          if (event.shiftKey) {
            // Shift+click: add to multi-select
            const current = new Set(selectedBodyIds);
            if (selectedBodyId) current.add(selectedBodyId);
            current.add(clickedId);
            selectedBodyId = null;
            selectedBodyIds = current;
            applySelection();
            postMessage({ type: 'box-select-result', ids: [...current], append: false });
            return;
          }
          if (event.ctrlKey) {
            // Ctrl+click: toggle in multi-select
            const current = new Set(selectedBodyIds);
            if (selectedBodyId) current.add(selectedBodyId);
            if (current.has(clickedId)) { current.delete(clickedId); } else { current.add(clickedId); }
            selectedBodyId = current.size === 1 ? [...current][0] : null;
            selectedBodyIds = current.size > 1 ? current : new Set();
            applySelection();
            postMessage({ type: 'box-select-result', ids: [...current], append: false });
            return;
          }

          selectedBodyIds.clear();
          selectedBodyId = clickedId;

          // Check if we clicked a flat face
          const flatFace = hoveredFacePlane || pickFlatFace(event.clientX, event.clientY);
          if (flatFace) {
            highlightFace(flatFace, 0x2b7fde);
            postMessage({
              type: 'face-click',
              bodyId: flatFace.bodyId,
              nx: round3(flatFace.nx), ny: round3(flatFace.ny), nz: round3(flatFace.nz),
              ox: round3(flatFace.ox), oy: round3(flatFace.oy), oz: round3(flatFace.oz),
              ux: round3(flatFace.ux), uy: round3(flatFace.uy), uz: round3(flatFace.uz),
              vx: round3(flatFace.vx), vy: round3(flatFace.vy), vz: round3(flatFace.vz)
            });
            setHudText(`Face selected — click Start Sketch to sketch on this face`);
          } else {
            clearFaceHighlight();
            applySelection();
            postMessage({
              type: 'selection-changed',
              entityKind: 'Body',
              entityId: selectedBodyId
            });
            setHudText(`Selected part: ${hit.entity.name}`);
          }
          return;
        }

        if (hit.entity.userData.type === 'plane') {
          selectedBodyIds.clear();
          selectedPlaneId = hit.entity.userData.entityId;
          selectedBodyId = null;
          applySelection();
          postMessage({
            type: 'selection-changed',
            entityKind: 'ReferencePlane',
            entityId: selectedPlaneId
          });
          setHudText(`Selected plane: ${hit.entity.name}`);
        }
      });

      renderer.domElement.addEventListener('contextmenu', (event) => {
        event.preventDefault();

        if (String(currentMode).toLowerCase() === 'sketch') {
          const entity = findNearestSketchEntity(event.clientX, event.clientY);
          if (entity) {
            postMessage({
              type: 'sketch-entity-context',
              entityId: entity.entityId,
              entityType: entity.entityType,
              screenX: event.clientX,
              screenY: event.clientY
            });
          }
          return;
        }

        const hit = pick(event.clientX, event.clientY);
        if (hit) {
          contextPoint = hit.point.clone();
        } else {
          contextPoint = controls.target.clone();
        }

        menu.style.left = `${event.clientX}px`;
        menu.style.top = `${event.clientY}px`;
        menu.style.display = 'block';
      });

      renderer.domElement.addEventListener('pointermove', (event) => {
        if (String(currentMode).toLowerCase() !== 'sketch') {
          if (boxSelectActive) {
            boxSelectEndX = event.clientX;
            boxSelectEndY = event.clientY;
            showBoxSelect(boxSelectStartX, boxSelectStartY, boxSelectEndX, boxSelectEndY);
            renderer.domElement.style.cursor = 'crosshair';
          } else if (!_navButtonDown) {
            const hovered = pick(event.clientX, event.clientY);
            if (hovered && hovered.entity.userData.type === 'body') {
              setHoverBody(hovered.entity.userData.entityId);
              const face = pickFlatFace(event.clientX, event.clientY);
              if (face) {
                hoveredFacePlane = face;
                highlightFace(face, 0x4c9ef0);
                renderer.domElement.style.cursor = 'cell';
              } else {
                clearFaceHighlight();
                hoveredFacePlane = null;
                renderer.domElement.style.cursor = 'pointer';
              }
            } else {
              setHoverBody(null);
              clearFaceHighlight();
              hoveredFacePlane = null;
              renderer.domElement.style.cursor = hovered ? 'pointer' : 'default';
            }
          }
          return;
        }

        if (sketchTransformToolActive) {
          if (sketchTransformDragging && sketchTransformTargetId) {
            const sketchHit = tryGetSketchLocal(event.clientX, event.clientY);
            if (sketchHit) {
              sketchTransformCurrentUV = { u: sketchHit.local.u, v: sketchHit.local.v };
              const du = sketchTransformCurrentUV.u - sketchTransformStartUV.u;
              const dv = sketchTransformCurrentUV.v - sketchTransformStartUV.v;
              const group = sketchMap.get(sketchTransformTargetId);
              if (group) {
                const offset = planeUVToWorldOffset(sketchTransformTargetKind, du, dv);
                group.position.copy(offset);
              }
            }
          } else {
            const hitGroup = tryGetSketchGroupAtPointer(event.clientX, event.clientY);
            renderer.domElement.style.cursor = hitGroup ? 'grab' : 'crosshair';
          }
          return;
        }

        if (sketchRotateToolActive) {
          if (sketchRotateDragging && sketchRotateTargetId && sketchRotatePivotUV) {
            const sketchHit = tryGetSketchLocal(event.clientX, event.clientY);
            if (sketchHit) {
              const du = sketchHit.local.u - sketchRotatePivotUV.u;
              const dv = sketchHit.local.v - sketchRotatePivotUV.v;
              const currentAngle = Math.atan2(dv, du);
              if (sketchRotateStartAngle === null) {
                sketchRotateStartAngle = currentAngle;
              }
              let delta = currentAngle - sketchRotateStartAngle;
              let deltaDegs = delta * 180 / Math.PI;
              if (snapCandidates.length > 0 || gridVisible) {
                deltaDegs = Math.round(deltaDegs / 5) * 5;
              }
              sketchRotateAccumDeg = deltaDegs;
              const group = sketchMap.get(sketchRotateTargetId);
              if (group) {
                group.rotation.z = deltaDegs * Math.PI / 180;
              }
            }
          } else {
            const hitGroup = tryGetSketchGroupAtPointer(event.clientX, event.clientY);
            renderer.domElement.style.cursor = hitGroup ? 'alias' : 'crosshair';
          }
          return;
        }

        renderer.domElement.style.cursor = 'crosshair';
        _sketchCursorSx = event.clientX;
        _sketchCursorSy = event.clientY;
        const sketchHit = tryGetSketchLocal(event.clientX, event.clientY);
        if (!sketchHit) {
          currentSnap = null;
          return;
        }
        currentSnap = computeSnap(event.clientX, event.clientY);
        const snapU = currentSnap ? currentSnap.u : sketchHit.local.u;
        const snapV = currentSnap ? currentSnap.v : sketchHit.local.v;
        postMessage({
          type: 'sketch-preview',
          planeId: sketchHit.planeId,
          planeKind: sketchHit.planeKind,
          u: snapU,
          v: snapV
        });
      });

      renderer.domElement.addEventListener('pointerup', (event) => {
        if (event.button === 0 && boxSelectActive) {
          const dx = Math.abs(boxSelectEndX - boxSelectStartX);
          const dy = Math.abs(boxSelectEndY - boxSelectStartY);
          if (dx > 4 || dy > 4) {
            commitBoxSelect(event.shiftKey);
          } else {
            hideBoxSelect();
            // Empty click in 3D mode — clear selection
            if (String(currentMode).toLowerCase() !== 'sketch') {
              selectedBodyId = null;
              selectedBodyIds.clear();
              selectedPlaneId = null;
              applySelection();
              postMessage({ type: 'selection-cleared' });
            }
          }
          renderer.domElement.style.cursor = 'default';
        }
        if (event.button === 0 && sketchTransformDragging) {
          commitSketchTransformDrag();
          renderer.domElement.style.cursor = 'grab';
        }
        if (event.button === 0 && sketchRotateDragging) {
          commitSketchRotateDrag();
          renderer.domElement.style.cursor = 'alias';
        }
        if (event.button === 1 || event.button === 2) {
          _navButtonDown = false;
          renderer.domElement.style.cursor = String(currentMode).toLowerCase() === 'sketch' ? 'crosshair' : 'default';
        }
      });

      renderer.domElement.addEventListener('pointerleave', () => {
        _navButtonDown = false;
        currentSnap = null;
        _sketchCursorSx = -9999;
        _sketchCursorSy = -9999;
        setHoverBody(null);
        clearFaceHighlight();
        if (String(currentMode).toLowerCase() === 'sketch') {
          postMessage({ type: 'sketch-clear-preview' });
        }
      });

      window.addEventListener('pointerdown', (event) => {
        if (menu.style.display !== 'block') {
          return;
        }

        if (event.button === 2) {
          return;
        }

        if (!menu.contains(event.target)) {
          hideContextMenu();
        }
      });

      window.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') {
          hideContextMenu();
          if (boxSelectActive) {
            hideBoxSelect();
            renderer.domElement.style.cursor = 'default';
            return;
          }
          if (String(currentMode).toLowerCase() === 'sketch') {
            if (sketchTransformToolActive) {
              cancelSketchTransformDrag();
              sketchTransformToolActive = false;
              renderer.domElement.style.cursor = 'crosshair';
              postMessage({ type: 'sketch-transform-deactivated' });
            } else if (sketchRotateToolActive) {
              cancelSketchRotateDrag();
              sketchRotateToolActive = false;
              renderer.domElement.style.cursor = 'crosshair';
              postMessage({ type: 'sketch-rotate-deactivated' });
            } else {
              postMessage({ type: 'sketch-cancel-step' });
            }
          } else if (moveToolActive) {
            if (isDragging && transformControls.object && dragStartPosition) {
              transformControls.object.position.copy(dragStartPosition);
              isDragging = false;
              dragStartPosition = null;
            }
            moveToolActive = false;
            applySelection();
            postMessage({ type: 'move-tool-deactivated' });
          } else if (isMeasureMode) {
            // Esc cancels active measurement
            isMeasureMode = false;
            measurePtA = null;
            renderer.domElement.style.cursor = 'default';
            setHudText('');
            postMessage({ type: 'measure-cancelled' });
          } else {
            // Esc in 3D mode — clear body selection
            selectedBodyId = null;
            selectedBodyIds.clear();
            selectedPlaneId = null;
            applySelection();
            postMessage({ type: 'selection-cleared' });
          }
          return;
        }
        if (event.key === 'Enter') {
          if (sketchTransformToolActive && String(currentMode).toLowerCase() === 'sketch') {
            commitSketchTransformDrag();
            sketchTransformToolActive = false;
            renderer.domElement.style.cursor = 'crosshair';
            postMessage({ type: 'sketch-transform-deactivated' });
            return;
          }
          if (sketchRotateToolActive && String(currentMode).toLowerCase() === 'sketch') {
            commitSketchRotateDrag();
            sketchRotateToolActive = false;
            renderer.domElement.style.cursor = 'crosshair';
            postMessage({ type: 'sketch-rotate-deactivated' });
            return;
          }
          if (moveToolActive && String(currentMode).toLowerCase() !== 'sketch') {
            moveToolActive = false;
            applySelection();
            postMessage({ type: 'move-tool-deactivated' });
            return;
          }
        }
        // F = fit all visible bodies; Home = reset to default camera position.
        if (event.key === 'f' || event.key === 'F') {
          const bounds = getSceneBodyBounds();
          if (bounds) { frameBounds(bounds); } else { applyDefaultView(); }
          return;
        }
        if (event.key === 'Home') {
          applyDefaultView();
        }
        if (event.key === 'g' || event.key === 'G') {
          if (String(currentMode).toLowerCase() === 'sketch') {
            gridVisible = !gridVisible;
          }
          return;
        }
        if (event.key === 'q' || event.key === 'Q') {
          if (String(currentMode).toLowerCase() === 'sketch') {
            postMessage({ type: 'toggle-construction-mode' });
          }
          return;
        }
      });

      window.addEventListener('error', (event) => {
        postMessage({
          type: 'viewport-error',
          message: event.message || 'Unhandled viewport error.',
          stack: event.error?.stack || ''
        });
      });

      window.addEventListener('unhandledrejection', (event) => {
        const reason = event.reason;
        postMessage({
          type: 'viewport-error',
          message: reason?.message || String(reason || 'Unhandled viewport rejection.'),
          stack: reason?.stack || ''
        });
      });

      menu.addEventListener('click', (event) => {
        const button = event.target.closest('button[data-action]');
        if (!button) {
          return;
        }

        const action = button.dataset.action;
        if (action === 'delete') {
          postMessage({ type: 'delete-requested' });
        } else if (action === 'focus') {
          focusCameraAt(contextPoint);
        }

        hideContextMenu();
      });

      menu.addEventListener('pointerover', (event) => {
        const button = event.target.closest('button[data-action]');
        if (!button) {
          return;
        }

        button.style.background = themePalettes[currentTheme].menuHover;
      });

      menu.addEventListener('pointerout', (event) => {
        const button = event.target.closest('button[data-action]');
        if (!button) {
          return;
        }

        button.style.background = 'transparent';
      });

      // ---- Smooth camera snap ----
      let _snap = null; // { fromPos, fromUp, toPos, toUp, t0, dur }
      const SNAP_DUR_MS = 280; // ms

      function startCameraSnap(toPos, toUp) {
        _snap = {
          fromPos: camera.position.clone(),
          fromUp:  camera.up.clone(),
          toPos:   toPos.clone(),
          toUp:    toUp.clone(),
          t0:      performance.now(),
          dur:     SNAP_DUR_MS
        };
      }

      // Ease-in-out cubic
      function easeInOut(t) {
        return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
      }

      function tickCameraSnap() {
        if (!_snap) return;
        const elapsed = performance.now() - _snap.t0;
        const raw = Math.min(elapsed / _snap.dur, 1);
        const ease = easeInOut(raw);

        camera.position.lerpVectors(_snap.fromPos, _snap.toPos, ease);
        camera.up.lerpVectors(_snap.fromUp, _snap.toUp, ease).normalize();
        controls.target.set(0, 0, 0);
        camera.lookAt(0, 0, 0);
        controls.update();

        if (raw >= 1) _snap = null;
      }

      function frame() {
        tickCameraSnap();
        controls.update();
        renderer.render(scene, camera);
        window._vcUpdate?.();
        drawOverlay();
        requestAnimationFrame(frame);
      }

      window.addEventListener('resize', () => {
        camera.aspect = window.innerWidth / window.innerHeight;
        camera.updateProjectionMatrix();
        renderer.setPixelRatio(window.devicePixelRatio);
        renderer.setSize(window.innerWidth, window.innerHeight);
        if (snapOverlay) {
          snapOverlay.width = window.innerWidth;
          snapOverlay.height = window.innerHeight;
        }
      });

      // Standard view navigation buttons
      const viewNav = document.getElementById('view-nav');
      if (viewNav) {
        viewNav.addEventListener('click', (e) => {
          const btn = e.target.closest('button');
          if (!btn) return;
          const view = btn.dataset.view;
          const dist = 60;
          const iso = new THREE.Vector3(1, 0.82, 1).normalize().multiplyScalar(dist);
          const views = {
            iso:    { pos: iso,                              up: new THREE.Vector3(0, 1, 0) },
            'iso-ne': { pos: new THREE.Vector3( 1, 0.82,  1).normalize().multiplyScalar(dist), up: new THREE.Vector3(0, 1, 0) },
            'iso-nw': { pos: new THREE.Vector3(-1, 0.82,  1).normalize().multiplyScalar(dist), up: new THREE.Vector3(0, 1, 0) },
            'iso-se': { pos: new THREE.Vector3( 1, 0.82, -1).normalize().multiplyScalar(dist), up: new THREE.Vector3(0, 1, 0) },
            'iso-sw': { pos: new THREE.Vector3(-1, 0.82, -1).normalize().multiplyScalar(dist), up: new THREE.Vector3(0, 1, 0) },
            top:    { pos: new THREE.Vector3(0, dist, 0.1),  up: new THREE.Vector3(0, 0, -1) },
            front:  { pos: new THREE.Vector3(0, 0, dist),    up: new THREE.Vector3(0, 1, 0) },
            right:  { pos: new THREE.Vector3(dist, 0, 0),    up: new THREE.Vector3(0, 1, 0) },
            bottom: { pos: new THREE.Vector3(0, -dist, 0.1), up: new THREE.Vector3(0, 0, 1) },
            back:   { pos: new THREE.Vector3(0, 0, -dist),   up: new THREE.Vector3(0, 1, 0) },
            left:   { pos: new THREE.Vector3(-dist, 0, 0),   up: new THREE.Vector3(0, 1, 0) }
          };
          const v = views[view];
          if (!v) return;
          startCameraSnap(v.pos, v.up);
          setCameraClipping(dist);
          e.stopPropagation();
        });
      }

      const wfToggle = document.getElementById('wf-toggle');
      if (wfToggle) {
        wfToggle.addEventListener('click', (e) => {
          isWireframeMode = !isWireframeMode;
          wfToggle.textContent = isWireframeMode ? 'Wfr' : 'Shd';
          wfToggle.style.borderColor = isWireframeMode ? '#7baee0' : '';
          bodyMap.forEach((mesh) => { mesh.material.wireframe = isWireframeMode; });
          edgeMap.forEach((lines) => { lines.visible = !isWireframeMode; });
          e.stopPropagation();
        });
      }

      // ---- View cube ----
      (function() {
        const vcCanvas = document.getElementById('vcube');
        if (!vcCanvas) return;

        const sz = 92;
        vcCanvas.width  = Math.round(sz * window.devicePixelRatio);
        vcCanvas.height = Math.round(sz * window.devicePixelRatio);
        vcCanvas.style.width  = sz + 'px';
        vcCanvas.style.height = sz + 'px';

        const vcRenderer = new THREE.WebGLRenderer({ canvas: vcCanvas, alpha: true, antialias: true });
        vcRenderer.setPixelRatio(window.devicePixelRatio);
        vcRenderer.setSize(sz, sz);
        vcRenderer.setClearColor(0x000000, 0);

        const vcScene  = new THREE.Scene();
        const vcCamera = new THREE.PerspectiveCamera(38, 1, 0.1, 100);
        vcCamera.position.set(0, 0, 5);

        // Build a canvas-texture face (colored rounded rect + white label)
        function makeFaceTex(bgHex, label) {
          const ts = 128;
          const cv = document.createElement('canvas');
          cv.width = ts; cv.height = ts;
          const cx = cv.getContext('2d');
          cx.clearRect(0, 0, ts, ts);
          cx.fillStyle = bgHex;
          cx.beginPath();
          cx.roundRect(5, 5, ts - 10, ts - 10, 14);
          cx.fill();
          cx.strokeStyle = 'rgba(0,0,0,0.28)';
          cx.lineWidth = 3;
          cx.stroke();
          cx.fillStyle = '#ffffff';
          cx.font = `bold ${Math.round(ts * 0.27)}px Segoe UI, Arial, sans-serif`;
          cx.textAlign = 'center';
          cx.textBaseline = 'middle';
          cx.fillText(label, ts / 2, ts / 2);
          return new THREE.CanvasTexture(cv);
        }

        // BoxGeometry face order: +X, -X, +Y, -Y, +Z, -Z
        const vcMaterials = [
          new THREE.MeshBasicMaterial({ map: makeFaceTex('#c85028', 'RIGHT') }),
          new THREE.MeshBasicMaterial({ map: makeFaceTex('#7a2a14', 'LEFT')  }),
          new THREE.MeshBasicMaterial({ map: makeFaceTex('#2878c8', 'TOP')   }),
          new THREE.MeshBasicMaterial({ map: makeFaceTex('#144e84', 'BOT')   }),
          new THREE.MeshBasicMaterial({ map: makeFaceTex('#28a050', 'FRONT') }),
          new THREE.MeshBasicMaterial({ map: makeFaceTex('#145e28', 'BACK')  }),
        ];
        const vcGeom = new THREE.BoxGeometry(2, 2, 2);
        const vcMesh = new THREE.Mesh(vcGeom, vcMaterials);
        vcScene.add(vcMesh);
        vcScene.add(new THREE.LineSegments(
          new THREE.EdgesGeometry(vcGeom),
          new THREE.LineBasicMaterial({ color: 0x000000 })
        ));

        // Snap view definitions (same dist/up as view-nav buttons)
        const vd = 60;
        const vcSnaps = [
          { pos: new THREE.Vector3( vd,  0,   0),    up: new THREE.Vector3(0, 1,  0) }, // +X Right
          { pos: new THREE.Vector3(-vd,  0,   0),    up: new THREE.Vector3(0, 1,  0) }, // -X Left
          { pos: new THREE.Vector3(  0,  vd, 0.1),   up: new THREE.Vector3(0, 0, -1) }, // +Y Top
          { pos: new THREE.Vector3(  0, -vd, 0.1),   up: new THREE.Vector3(0, 0,  1) }, // -Y Bottom
          { pos: new THREE.Vector3(  0,  0,  vd),    up: new THREE.Vector3(0, 1,  0) }, // +Z Front
          { pos: new THREE.Vector3(  0,  0, -vd),    up: new THREE.Vector3(0, 1,  0) }, // -Z Back
        ];
        const vcFaceNames = ['Right','Left','Top','Bottom','Front','Back'];

        const vcRay = new THREE.Raycaster();
        function vcNDC(e) {
          const r = vcCanvas.getBoundingClientRect();
          return new THREE.Vector2(
            ((e.clientX - r.left) / r.width)  *  2 - 1,
           -((e.clientY - r.top)  / r.height) *  2 + 1
          );
        }

        vcCanvas.addEventListener('mousemove', (e) => {
          vcRay.setFromCamera(vcNDC(e), vcCamera);
          const hits = vcRay.intersectObject(vcMesh);
          vcCanvas.title = hits.length > 0 ? vcFaceNames[hits[0].face.materialIndex] + ' view' : '';
        });

        vcCanvas.addEventListener('click', (e) => {
          vcRay.setFromCamera(vcNDC(e), vcCamera);
          const hits = vcRay.intersectObject(vcMesh);
          if (hits.length > 0) {
            const sv = vcSnaps[hits[0].face.materialIndex];
            if (sv) {
              startCameraSnap(sv.pos, sv.up);
              setCameraClipping(vd);
            }
          }
          e.stopPropagation();
        });

        // Called every frame from the main animate loop
        window._vcUpdate = function() {
          const dir = new THREE.Vector3();
          camera.getWorldDirection(dir);
          vcCamera.position.copy(dir).multiplyScalar(-5);
          vcCamera.up.copy(camera.up);
          vcCamera.lookAt(0, 0, 0);
          vcRenderer.render(vcScene, vcCamera);
        };
      })();

      // ---- Section plane (clipping) API ----
      let sectionPlane = null;

      function applySectionPlane() {
        if (sectionPlane) {
          renderer.clippingPlanes = [sectionPlane];
          renderer.localClippingEnabled = true;
        } else {
          renderer.clippingPlanes = [];
          renderer.localClippingEnabled = false;
        }
      }

      window.my3dappSetMeasureMode = function(enabled) {
        isMeasureMode = !!enabled;
        measurePtA = null;
        if (isMeasureMode) {
          setHudText('Measure: click a body surface for first point');
          renderer.domElement.style.cursor = 'crosshair';
        } else {
          setHudText('');
          renderer.domElement.style.cursor = 'default';
        }
      };

      window.my3dappSetSketchToolHint = function(hint) {
        _sketchToolHint = hint || '';
        if (String(currentMode).toLowerCase() === 'sketch') {
          setHudText(`Sketch | ${activePlaneName} plane | ${_sketchToolHint || 'Click to place geometry'}`);
        }
      };

      window.my3dappSetSectionPlane = function(axis, offset, enabled) {
        if (!enabled) {
          sectionPlane = null;
        } else {
          let normal;
          switch (String(axis).toLowerCase()) {
            case 'x': normal = new THREE.Vector3(-1, 0, 0); break;
            case 'y': normal = new THREE.Vector3(0, -1, 0); break;
            default:  normal = new THREE.Vector3(0, 0, -1); break;
          }
          sectionPlane = new THREE.Plane(normal, Number(offset) || 0);
        }
        applySectionPlane();
      };

      applyTheme('Light');
      applyDefaultView();
      frame();
      postMessage({
        type: 'viewport-ready',
        message: 'Three.js initialized.'
      });
    } catch (error) {
      const hud = document.getElementById('hud');
      const errorText = error?.message || String(error);
      const errorStack = error?.stack || '';
      if (hud) {
        hud.textContent = `3D engine failed to load: ${errorText}`;
        hud.style.background = 'rgba(255,238,238,0.96)';
        hud.style.borderColor = '#e2b7b7';
        hud.style.color = '#9a3a3a';
      }

      if (window.chrome?.webview?.postMessage) {
        window.chrome.webview.postMessage({
          type: 'viewport-error',
          message: errorText,
          stack: errorStack
        });
      }

      console.error('My3DApp viewport initialization failed:', error);
    }
  </script>
</body>
</html>
""";
}
