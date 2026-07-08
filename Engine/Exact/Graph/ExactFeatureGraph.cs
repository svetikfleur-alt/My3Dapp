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
        if (_width <= 0 || _depth <= 0 || _height <= 0) throw new ArgumentOutOfRangeException("Dimensions", "Box dimensions must be greater than zero.");
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
        if (_radius <= 0 || _height <= 0) throw new ArgumentOutOfRangeException("Dimensions", "Cylinder dimensions must be greater than zero.");
        Body = kernel.CreateCylinder(_radius, _height);
    }
}

public sealed class ExactRectangleNode : ExactFeatureNode
{
    private readonly double _width;
    private readonly double _height;
    public ExactRectangleNode(string id, AclSourceSpan span, double width, double height) : base(id, span)
    {
        _width = width;
        _height = height;
    }
    public override void Build(IExactCadKernel kernel, AclSemanticValidator? validator)
    {
        if (_width <= 0 || _height <= 0) throw new ArgumentOutOfRangeException("Dimensions", "Rectangle dimensions must be greater than zero.");
        var pts = new double[] { -_width/2, -_height/2, _width/2, -_height/2, _width/2, _height/2, -_width/2, _height/2 };
        using var wire = kernel.CreateWire(pts, true);
        Body = kernel.CreateFace(wire);
    }
}

public sealed class ExactCircleNode : ExactFeatureNode
{
    private readonly double _radius;
    public ExactCircleNode(string id, AclSourceSpan span, double radius) : base(id, span) { _radius = radius; }
    public override void Build(IExactCadKernel kernel, AclSemanticValidator? validator)
    {
        if (_radius <= 0) throw new ArgumentOutOfRangeException("Dimensions", "Circle radius must be greater than zero.");
        using var wire = kernel.CreateCircleWire(_radius);
        Body = kernel.CreateFace(wire);
    }
}

public sealed class ExactExtrudeNode : ExactFeatureNode
{
    private readonly ExactFeatureNode _profile;
    private readonly double _depth;

    public ExactExtrudeNode(string id, AclSourceSpan span, ExactFeatureNode profile, double depth) : base(id, span)
    {
        _profile = profile;
        _depth = depth;
    }

    public override void Build(IExactCadKernel kernel, AclSemanticValidator? validator)
    {
        if (_profile.Body == null) throw new InvalidOperationException($"Extrude profile '{_profile.Id}' has no body.");
        if (_depth <= 0) throw new ArgumentOutOfRangeException(nameof(_depth), "Extrude depth must be greater than zero.");
        Body = kernel.CreatePrism(_profile.Body, 0, 0, _depth);
    }
}

public sealed class ExactHoleNode : ExactFeatureNode
{
    private readonly ExactFeatureNode _target;
    private readonly double _diameter;
    private readonly double _x;
    private readonly double _y;
    private readonly double _depth; // 0 means through_all (we use 1000 for now)

    public ExactHoleNode(string id, AclSourceSpan span, ExactFeatureNode target, double diameter, double x, double y, double depth) : base(id, span)
    {
        _target = target;
        _diameter = diameter;
        _x = x;
        _y = y;
        _depth = depth <= 0 ? 1000.0 : depth;
    }

    public override void Build(IExactCadKernel kernel, AclSemanticValidator? validator)
    {
        if (_target.Body == null) throw new InvalidOperationException($"Hole target '{_target.Id}' has no body.");
        if (_diameter <= 0) throw new ArgumentOutOfRangeException(nameof(_diameter), "Hole diameter must be greater than zero.");
        
        using var cyl = kernel.CreateCylinder(_diameter / 2, _depth);
        // Translate hole to position
        using var translatedCyl = kernel.Translated(cyl, _x, _y, 0); // simplistic position
        Body = kernel.BooleanSubtract(_target.Body, translatedCyl);
    }
}

public sealed class ExactLinearPatternNode : ExactFeatureNode
{
    private readonly ExactFeatureNode _source;
    private readonly double _dx;
    private readonly double _dy;
    private readonly double _dz;
    private readonly int _count;
    private readonly double _spacing;

    public ExactLinearPatternNode(string id, AclSourceSpan span, ExactFeatureNode source, double dx, double dy, double dz, int count, double spacing) : base(id, span)
    {
        _source = source;
        _dx = dx; _dy = dy; _dz = dz;
        _count = count;
        _spacing = spacing;
    }

    public override void Build(IExactCadKernel kernel, AclSemanticValidator? validator)
    {
        if (_source.Body == null) throw new InvalidOperationException($"Linear pattern source '{_source.Id}' has no body.");
        if (_count <= 0) throw new ArgumentOutOfRangeException(nameof(_count), "Linear pattern count must be greater than zero.");

        var copies = new List<IExactBodyHandle>();
        for (int i = 0; i < _count; i++)
        {
            copies.Add(kernel.Translated(_source.Body, _dx * _spacing * i, _dy * _spacing * i, _dz * _spacing * i));
        }
        
        // The prompt says "combine with target only when selected pattern operation requires it; do not always duplicate and fuse".
        // Let's just return the compound as the body of this node.
        Body = kernel.CreateCompound(copies);
        foreach (var c in copies) c.Dispose();
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
