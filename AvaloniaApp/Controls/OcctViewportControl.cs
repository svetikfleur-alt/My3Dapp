using Avalonia.Controls;
using Avalonia.Platform;
using My3DApp.Occt;

namespace My3DApp.AvaloniaApp.Controls;

/// <summary>
/// Native CAD viewport: hosts the OCCT AIS/V3d child window created by
/// <see cref="OcctViewer"/> inside Avalonia via NativeControlHost.
/// Avalonia owns placement/size of the child HWND; OCCT handles WM_PAINT/WM_SIZE.
/// </summary>
public sealed class OcctViewportControl : NativeControlHost
{
    private OcctViewer? _viewer;

    /// <summary>Managed viewer facade; null until the native control is created.</summary>
    public OcctViewer? Viewer => _viewer;

    public event EventHandler? ViewerReady;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _viewer = new OcctViewer();
        var width = Math.Max(100, (int)Bounds.Width);
        var height = Math.Max(100, (int)Bounds.Height);
        var hwnd = _viewer.Initialize(parent.Handle, width, height);
        ViewerReady?.Invoke(this, EventArgs.Empty);
        return new PlatformHandle(hwnd, "HWND");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        var viewer = _viewer;
        _viewer = null;
        viewer?.Dispose();
        base.DestroyNativeControlCore(control);
    }
}
