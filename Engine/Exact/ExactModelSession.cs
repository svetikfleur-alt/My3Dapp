using FormaCore.Engine.Acl;
using FormaCore.Engine.Exact.Graph;

namespace FormaCore.Engine.Exact;

public sealed class ExactModelSession : IDisposable
{
    private IExactCadKernel? _kernel;
    private ExactFeatureGraph? _currentGraph;
    
    public ExactModelSession()
    {
    }

    public void SetKernel(IExactCadKernel kernel)
    {
        _kernel = kernel;
    }

    public ExactFeatureGraph? CurrentGraph => _currentGraph;

    public bool TryUpdateModel(ExactFeatureGraph newGraph, AclSemanticValidator? validator)
    {
        if (_kernel == null) return false;
        try
        {
            foreach (var node in newGraph.Nodes)
            {
                node.Build(_kernel, validator);
            }

            var oldGraph = _currentGraph;
            _currentGraph = newGraph;
            oldGraph?.Dispose();
            
            return true;
        }
        catch
        {
            // If build fails, we keep the old graph but return false to indicate it's stale
            newGraph.Dispose();
            return false;
        }
    }

    public void Dispose()
    {
        _currentGraph?.Dispose();
        _currentGraph = null;
    }
}
