namespace FormaCore.Engine.Exact;

public enum CadStandardView
{
    Top,
    Front,
    Right,
    Isometric,
}

public enum CadDisplayMode
{
    Shaded,
    ShadedWithEdges,
    Wireframe,
}

/// <summary>
/// Presentation boundary for the native CAD-aware viewport. Implemented over
/// OCCT AIS/V3d; the UI talks to this interface only and never to native types.
/// </summary>
public interface ICadViewportHost
{
    void DisplayBody(IExactBodyHandle body);
    void UpdateBody(IExactBodyHandle body);
    void HideBody(Guid bodyId);

    void SetView(CadStandardView view);
    void FitAll();
    void SetDisplayMode(CadDisplayMode mode);

    /// <summary>Enable or disable picking for a topology kind (Body/Face/Edge/Vertex).</summary>
    void SetSelectionMode(TopologyKind kind, bool enabled);

    event EventHandler<TopologySelection>? SelectionChanged;
    event EventHandler<TopologySelection?>? HoverChanged;
}
