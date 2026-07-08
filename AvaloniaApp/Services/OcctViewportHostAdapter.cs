using FormaCore.Engine.Exact;
using My3DApp.AvaloniaApp.Controls;
using My3DApp.Occt;

namespace My3DApp.AvaloniaApp.Services;

/// <summary>
/// ICadViewportHost over OcctViewportControl. Translates bridge enums/events into
/// the engine boundary types; UI code never touches My3DApp.Occt types directly.
/// </summary>
public sealed class OcctViewportHostAdapter : ICadViewportHost
{
    private readonly OcctViewportControl _control;

    public OcctViewportHostAdapter(OcctViewportControl control)
    {
        _control = control;
        if (_control.Viewer is not null)
        {
            Attach();
        }
        else
        {
            _control.ViewerReady += (_, _) => Attach();
        }
    }

    public event EventHandler<TopologySelection>? SelectionChanged;
    public event EventHandler<TopologySelection?>? HoverChanged;

    public void DisplayBody(IExactBodyHandle body) => Viewer.DisplayBody(OcctExactKernel.Unwrap(body));

    public void UpdateBody(IExactBodyHandle body) => Viewer.UpdateBody(OcctExactKernel.Unwrap(body));

    public void HideBody(Guid bodyId) => Viewer.HideBody(bodyId);

    public void SetView(CadStandardView view) => Viewer.SetView((OcctStandardView)view);

    public void FitAll() => Viewer.FitAll();

    public void SetDisplayMode(CadDisplayMode mode) => Viewer.SetDisplayMode((OcctDisplayMode)mode);

    public void SetSelectionMode(TopologyKind kind, bool enabled) => Viewer.SetSelectionMode((OcctTopoKind)kind, enabled);

    public void ShowFeaturePreview(IExactBodyHandle body)
        => throw new NotSupportedException("Exact feature preview arrives with the feature-dialog phase; no preview was shown.");

    public void ClearFeaturePreview()
    {
        // No preview can exist yet (ShowFeaturePreview throws), so clearing is a no-op by definition.
    }

    public void SetReferenceGeometryVisible(bool visible)
        => throw new NotSupportedException("Reference-geometry display arrives with the sketch phase; nothing was toggled.");

    private OcctViewer Viewer => _control.Viewer
        ?? throw new InvalidOperationException("OCCT viewport is not initialized yet");

    private void Attach()
    {
        var viewer = _control.Viewer!;
        viewer.SelectionChanged += (_, e) =>
        {
            SelectionChanged?.Invoke(this, ToSelection(e, "viewport-click"));
        };
        viewer.HoverChanged += (_, e) =>
        {
            HoverChanged?.Invoke(this, e.Cleared ? null : ToSelection(e, "viewport-hover"));
        };
    }

    private static TopologySelection ToSelection(OcctPickEventArgs e, string provenance)
        => new(e.BodyId, (TopologyKind)e.Kind, e.SubIndex, provenance);
}
