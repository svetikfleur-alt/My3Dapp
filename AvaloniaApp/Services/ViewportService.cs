using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace My3DApp.AvaloniaApp.Services;

/// <summary>
/// Hosts the WebView2 3D viewport inside an Avalonia Border.
/// The web content (Three.js) lives in Assets/Web/viewer.html.
/// </summary>
public class ViewportService : IDisposable
{
    private readonly Border _host;
    private bool _initialized;
    private bool _disposed;

    // WebView2 COM interop handle — platform only, no-op on non-Windows
    private object? _webView;

    public ViewportService(Border host)
    {
        _host = host;
    }

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

    private async Task InitializeWebView2Async()
    {
        // Dynamically load WebView2 to avoid compile-time dependency issues on Linux CI.
        var wv2Type = Type.GetType(
            "Microsoft.Web.WebView2.WinForms.WebView2, Microsoft.Web.WebView2.WinForms");

        if (wv2Type is null)
        {
            RuntimeLog.Warn("Viewport", "WebView2 type not available.");
            return;
        }

        _webView = Activator.CreateInstance(wv2Type);
        if (_webView is null) return;

        // EnsureCoreWebView2Async
        var ensureMethod = wv2Type.GetMethod("EnsureCoreWebView2Async",
            [typeof(object)]) ?? wv2Type.GetMethod("EnsureCoreWebView2Async", []);
        if (ensureMethod != null)
            await (Task)(ensureMethod.Invoke(_webView, ensureMethod.GetParameters().Length == 0 ? null : [null])!);

        var viewerPath = GetViewerPath();
        NavigateTo($"file:///{viewerPath.Replace('\\', '/')}");
    }

    private static string GetViewerPath()
    {
        var exeDir = AppContext.BaseDirectory;
        return Path.Combine(exeDir, "Assets", "Web", "viewer.html");
    }

    private void NavigateTo(string url)
    {
        if (_webView is null) return;
        var method = _webView.GetType().GetMethod("Navigate", [typeof(string)])
            ?? _webView.GetType().GetMethod("Source"); // fallback property setter
        method?.Invoke(_webView, [url]);
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

    public Task FitViewAsync()  => ExecuteScriptAsync("viewer.fitView();");
    public Task ResetViewAsync() => ExecuteScriptAsync("viewer.resetView();");
    public Task SetWireframeAsync(bool on) => ExecuteScriptAsync($"viewer.setWireframe({(on ? "true" : "false")});");
    public Task SetViewAsync(string view)  => ExecuteScriptAsync($"viewer.setView('{view}');");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        (_webView as IDisposable)?.Dispose();
    }
}
