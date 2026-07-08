using FormaCore.Engine.Acl;
using FormaCore.Engine.Exact.Graph;

namespace FormaCore.Engine.Exact;

public sealed class AclBuildCoordinator
{
    private readonly AclExactCompiler _compiler;
    private readonly ExactModelSession _session;

    public AclBuildCoordinator()
    {
        _compiler = new AclExactCompiler();
        _session = new ExactModelSession();
    }
    
    public ExactModelSession Session => _session;

    public (bool Success, IReadOnlyList<AclDiagnostic> Diagnostics, ExactFeatureGraph? Graph) Build(string source)
    {
        var (graph, diagnostics) = _compiler.Compile(source);
        
        if (graph == null)
        {
            return (false, diagnostics, _session.CurrentGraph);
        }

        var success = _session.TryUpdateModel(graph, null);
        
        if (!success)
        {
            var buildError = new AclDiagnostic(AclDiagnosticSeverity.Error, "Build", "Exact kernel failed to build bodies.", AclSourceSpan.None);
            var updatedDiagnostics = new List<AclDiagnostic>(diagnostics) { buildError };
            return (false, updatedDiagnostics, _session.CurrentGraph);
        }

        return (true, diagnostics, graph);
    }

    public ExactFeatureGraph? ComputePreview(string source)
    {
        var (graph, _) = _compiler.Compile(source);
        return graph;
    }
}
