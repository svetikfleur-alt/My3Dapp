using FormaCore.Engine.Exact;
using FormaCore.Engine.Exact.Graph;

namespace My3DApp.AvaloniaApp.Services;

/// <summary>
/// Production owner of the exact kernel + native viewport pairing inside the
/// single My3DApp window. Also hosts the developer-only kernel validation body:
/// the validation body is NOT a user document — it never enters ACL, history,
/// or persistence, and can be cleared completely.
/// </summary>
public sealed class ExactSceneController : IDisposable
{
    private readonly OcctExactKernel _kernel = new();
    private readonly ICadViewportHost _host;
    private IExactBodyHandle? _validationBody;

    public ExactSceneController(ICadViewportHost host) => _host = host;

    public IExactCadKernel Kernel => _kernel;

    public IExactCadExporter Exporter => _kernel;

    public bool HasValidationBody => _validationBody is { IsValid: true };

    /// <summary>
    /// Developer validation geometry: deliberately ASYMMETRIC so Top/Front/Right/Iso
    /// orientations are distinguishable — plate 120×80×18 mm, offset Ø22 through-hole,
    /// Ø30×25 boss near one corner.
    /// </summary>
    public (double XMax, double YMax, double ZMax) ShowValidationBody()
    {
        ClearValidationBody();

        using var plate = _kernel.CreateBox(120, 80, 18);
        using var holeCyl = _kernel.CreateCylinder(11, 18);
        using var hole = _kernel.Translated(holeCyl, 85, 25, 0);
        using var plateWithHole = _kernel.BooleanSubtract(plate, hole);
        using var bossCyl = _kernel.CreateCylinder(15, 25);
        using var boss = _kernel.Translated(bossCyl, 30, 55, 18);
        _validationBody = _kernel.BooleanUnion(plateWithHole, boss);

        _host.DisplayBody(_validationBody);
        _host.SetView(CadStandardView.Isometric);
        _host.FitAll();

        var b = ((OcctBodyHandle)_validationBody).GetBounds();
        return (b.XMax, b.YMax, b.ZMax);
    }

    public void ClearValidationBody()
    {
        if (_validationBody is not null)
        {
            _host.HideBody(_validationBody.BodyId);
            _validationBody.Dispose();
            _validationBody = null;
        }
    }

    public void ExportValidationStep(string path)
    {
        if (_validationBody is null)
        {
            throw new InvalidOperationException("No validation body is displayed.");
        }

        _kernel.ExportStep(_validationBody, path);
    }

    public void Dispose()
    {
        ClearValidationBody();
        ClearFeatureGraph();
        _kernel.Dispose();
    }

    private ExactFeatureGraph? _currentGraph;

    public void DisplayFeatureGraph(ExactFeatureGraph graph)
    {
        ClearValidationBody();
        ClearFeatureGraph();
        
        _currentGraph = graph;
        
        foreach (var node in graph.Nodes)
        {
            if (node.Body != null)
            {
                _host.DisplayBody(node.Body);
            }
        }
        
        _host.FitAll();
    }

    public void ClearFeatureGraph()
    {
        if (_currentGraph != null)
        {
            foreach (var node in _currentGraph.Nodes)
            {
                if (node.Body != null)
                {
                    _host.HideBody(node.Body.BodyId);
                }
            }
            _currentGraph = null;
        }
    }
}
