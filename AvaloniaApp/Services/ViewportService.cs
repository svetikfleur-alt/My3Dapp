using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Controls;

namespace My3DApp.AvaloniaApp.Services;

/// <summary>
/// Hosts the WebView2 3D viewport inside an Avalonia Border.
/// The web content (Three.js) lives in Assets/Web/viewer.html.
///
/// On Windows: initialises a WinForms WebView2 control, then reparents its Win32
/// HWND under the Avalonia window and positions it to cover the host Border.
/// On Linux/macOS: no-op (WebView2 is Windows-only).
/// </summary>
public class ViewportService : IDisposable
{
    private readonly Border _host;
    private bool _initialized;
    private bool _disposed;

    private object? _webView;          // WinForms WebView2 instance (via reflection)
    private IntPtr   _webViewHwnd;     // Win32 HWND of that control

    public event Action<float, float, float>? CursorPositionChanged;
    public event Action<string?>? ObjectSelected;

    public ViewportService(Border host) => _host = host;

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            RuntimeLog.Warn("Viewport", "WebView2 requires Windows. Viewport disabled.");
            return;
        }

        try
        {
            await InitializeWebView2Async();
            _initialized = true;
            RuntimeLog.Info("Viewport", "WebView2 viewport ready.");
        }
        catch (Exception ex)
        {
            RuntimeLog.Error("Viewport", "WebView2 init failed.", ex);
        }
    }

    public async Task ExecuteScriptAsync(string script)
    {
        if (!_initialized || _webView is null) return;
        try
        {
            var method = _webView.GetType().GetMethod("ExecuteScriptAsync", [typeof(string)]);
            if (method != null)
                await (Task<string>)method.Invoke(_webView, [script])!;
        }
        catch (Exception ex)
        {
            RuntimeLog.Error("Viewport", "Script exec failed.", ex);
        }
    }

    public Task FitViewAsync()              => ExecuteScriptAsync("viewer.fitView();");
    public Task ResetViewAsync()            => ExecuteScriptAsync("viewer.resetView();");
    public Task SetWireframeAsync(bool on)  => ExecuteScriptAsync($"viewer.setWireframe({(on ? "true" : "false")});");
    public Task SetViewAsync(string view)   => ExecuteScriptAsync($"viewer.setView('{view}');");

    // ── Initialisation ────────────────────────────────────────────────────────

    private async Task InitializeWebView2Async()
    {
        var wv2Type = Type.GetType(
            "Microsoft.Web.WebView2.WinForms.WebView2, Microsoft.Web.WebView2.WinForms");

        if (wv2Type is null)
        {
            RuntimeLog.Warn("Viewport", "WebView2 WinForms type not available.");
            return;
        }

        _webView = Activator.CreateInstance(wv2Type);
        if (_webView is null) return;

        // Initialise the WebView2 runtime (creates the underlying CoreWebView2).
        var ensureMethod = wv2Type.GetMethod("EnsureCoreWebView2Async", [typeof(object)])
                        ?? wv2Type.GetMethod("EnsureCoreWebView2Async", []);
        if (ensureMethod != null)
            await (Task)(ensureMethod.Invoke(_webView,
                ensureMethod.GetParameters().Length == 0 ? null : [null])!);

        // Navigate to the Three.js viewer page.
        var viewerPath = GetViewerPath();
        NavigateTo($"file:///{viewerPath.Replace('\\', '/')}");

        // Subscribe to messages posted from JavaScript (cursor position, selection).
        SubscribeWebMessages();

        // Embed the WinForms HWND into the Avalonia window so it is actually visible.
        EmbedIntoAvaloniaWindow();
    }

    private void EmbedIntoAvaloniaWindow()
    {
        if (_webView is null) return;

        // Obtain the Avalonia top-level window and its Win32 HWND.
        var topLevel = TopLevel.GetTopLevel(_host);
        var parentHwnd = topLevel?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (parentHwnd == IntPtr.Zero)
        {
            RuntimeLog.Warn("Viewport", "Could not obtain parent HWND — viewport hidden.");
            return;
        }

        // Accessing Control.Handle forces creation of the Win32 window.
        var wfControlType = Type.GetType("System.Windows.Forms.Control, System.Windows.Forms");
        var handleProp = wfControlType?.GetProperty("Handle")
                      ?? _webView.GetType().GetProperty("Handle");
        if (handleProp is null) return;

        _webViewHwnd = (IntPtr)handleProp.GetValue(_webView)!;
        if (_webViewHwnd == IntPtr.Zero) return;

        // Re-style the window as a proper child (strip top-level / popup bits).
        const int   GWL_STYLE     = -16;
        const uint  WS_CHILD      = 0x40000000;
        const uint  WS_CLIPSIBLINGS = 0x04000000;
        const uint  WS_CLIPCHILDREN = 0x02000000;
        const uint  WS_VISIBLE    = 0x10000000;
        Win32.SetWindowLong(_webViewHwnd, GWL_STYLE,
            (int)(WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN | WS_VISIBLE));

        // Reparent under the Avalonia window and make visible.
        Win32.SetParent(_webViewHwnd, parentHwnd);
        Win32.ShowWindow(_webViewHwnd, 1 /* SW_SHOWNORMAL */);

        // Position to exactly cover the host Border.
        PositionWebView();

        // Keep position in sync when the layout or window moves.
        _host.LayoutUpdated += (_, _) => PositionWebView();
        if (topLevel is Window w)
            w.PositionChanged += (_, _) => PositionWebView();
    }

    private void PositionWebView()
    {
        if (_webViewHwnd == IntPtr.Zero) return;
        var topLevel = TopLevel.GetTopLevel(_host);
        if (topLevel is null) return;

        var pt    = _host.TranslatePoint(new Avalonia.Point(0, 0), topLevel) ?? default;
        var scale = topLevel.RenderScaling;

        Win32.MoveWindow(
            _webViewHwnd,
            (int)(pt.X * scale),
            (int)(pt.Y * scale),
            (int)(_host.Bounds.Width  * scale),
            (int)(_host.Bounds.Height * scale),
            repaint: true);
    }

    // ── WebView2 helpers ──────────────────────────────────────────────────────

    private void NavigateTo(string url)
    {
        if (_webView is null) return;
        var coreWv2 = _webView.GetType().GetProperty("CoreWebView2")?.GetValue(_webView);
        if (coreWv2 != null)
        {
            coreWv2.GetType().GetMethod("Navigate", [typeof(string)])?.Invoke(coreWv2, [url]);
            return;
        }
        var sourceProp = _webView.GetType().GetProperty("Source");
        sourceProp?.SetValue(_webView, new Uri(url));
    }

    private void SubscribeWebMessages()
    {
        try
        {
            var coreWv2 = _webView?.GetType().GetProperty("CoreWebView2")?.GetValue(_webView);
            if (coreWv2 is null) return;

            var eventInfo = coreWv2.GetType().GetEvent("WebMessageReceived");
            if (eventInfo is null) return;

            var handler = Delegate.CreateDelegate(
                eventInfo.EventHandlerType!,
                this,
                typeof(ViewportService).GetMethod(nameof(OnWebMessageReceived),
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)!);

            eventInfo.AddEventHandler(coreWv2, handler);
        }
        catch (Exception ex)
        {
            RuntimeLog.Warn("Viewport", $"WebMessage subscribe failed: {ex.Message}");
        }
    }

    private void OnWebMessageReceived(object sender, object e)
    {
        try
        {
            if (e.GetType().GetMethod("TryGetWebMessageAsString")
                    ?.Invoke(e, null) is not string json) return;

            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;

            switch (typeEl.GetString())
            {
                case "cursorPos" when
                    root.TryGetProperty("x", out var x) &&
                    root.TryGetProperty("y", out var y) &&
                    root.TryGetProperty("z", out var z):
                    CursorPositionChanged?.Invoke(x.GetSingle(), y.GetSingle(), z.GetSingle());
                    break;

                case "select":
                    var name = root.TryGetProperty("name", out var n) &&
                               n.ValueKind != JsonValueKind.Null
                               ? n.GetString() : null;
                    ObjectSelected?.Invoke(name);
                    break;
            }
        }
        catch { /* never throw from an event handler */ }
    }

    private static string GetViewerPath()
        => Path.Combine(AppContext.BaseDirectory, "Assets", "Web", "viewer.html");

    // ── Win32 interop ─────────────────────────────────────────────────────────

    private static class Win32
    {
        [DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr child, IntPtr parent);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmd);

        [DllImport("user32.dll")]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y,
                                              int cx, int cy, bool repaint);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNew);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        (_webView as IDisposable)?.Dispose();
    }
}
