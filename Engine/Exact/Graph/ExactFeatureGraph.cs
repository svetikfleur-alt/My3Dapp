using FormaCore.Engine.Acl;

namespace FormaCore.Engine.Exact.Graph;

public abstract class ExactFeatureNode : IDisposable
{
    protected ExactFeatureNode(string id, AclSourceSpan span)
    {
        Id = id;
        Span = span;
    }

    public string Id { get; }
    public AclSourceSpan Span { get; }
    public IExactBodyHandle? Body { get; protected set; }
    
    public abstract void Build(IExactCadKernel kernel, AclSemanticValidator? validator);

    public virtual void Dispose()
    {
        Body?.Dispose();
        Body = null;
    }
}

public sealed class ExactBoxNode : ExactFeatureNode
{
    private readonly double _width;
    private readonly double _depth;
    private readonly double _height;

    public ExactBoxNode(string id, AclSourceSpan span, double width, double depth, double height) 
        : base(id, span)
    {
        _width = width;
        _depth = depth;
        _height = height;
    }

    public override void Build(IExactCadKernel kernel, AclSemanticValidator? validator)
    {
        Body = kernel.CreateBox(_width, _depth, _height);
    }
}

public sealed class ExactCylinderNode : ExactFeatureNode
{
    private readonly double _radius;
    private readonly double _height;

    public ExactCylinderNode(string id, AclSourceSpan span, double radius, double height) 
        : base(id, span)
    {
        _radius = radius;
        _height = height;
    }

    public override void Build(IExactCadKernel kernel, AclSemanticValidator? validator)
    {
        Body = kernel.CreateCylinder(_radius, _height);
    }
}

public sealed class ExactFeatureGraph : IDisposable
{
    private readonly List<ExactFeatureNode> _nodes = [];
    private bool _isDisposed;

    public IReadOnlyList<ExactFeatureNode> Nodes => _nodes;

    public void AddNode(ExactFeatureNode node)
    {
        _nodes.Add(node);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        foreach (var node in _nodes)
        {
            node.Dispose();
        }
        
        _nodes.Clear();
        _isDisposed = true;
    }
}
