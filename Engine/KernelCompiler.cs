using FormaCore.Core;

namespace FormaCore.Engine;

public sealed class KernelCompiler
{
    private readonly IGeometryKernelAdapter _adapter;
    private readonly SolidMesher _mesher;

    public KernelCompiler(IGeometryKernelAdapter adapter, SolidMesher mesher)
    {
        _adapter = adapter;
        _mesher = mesher;
    }

    public GeometryKernelBody Compile(Solid solid)
    {
        return solid switch
        {
            BoxSolid b => _adapter.CreateBox(b.Width, b.Depth, b.Height),
            CylinderSolid c => _adapter.CreateCylinder(c.Radius, c.Height),
            TransformedSolid t => CompileTransformed(t),
            BooleanSolid b => CompileBoolean(b),
            _ => _adapter.CreateFromMesh(_mesher.Tessellate(solid))
        };
    }

    private GeometryKernelBody CompileTransformed(TransformedSolid t)
    {
        var child = Compile(t.Child);
        // We only support translation for now since the adapter only has Translate
        return _adapter.Translate(child, t.Transform.Translation.X, t.Transform.Translation.Y, t.Transform.Translation.Z);
    }

    private GeometryKernelBody CompileBoolean(BooleanSolid b)
    {
        var a = Compile(b.A);
        var childB = Compile(b.B);
        return b.Operation switch
        {
            BooleanOperation.Union => _adapter.Union(a, childB),
            BooleanOperation.Subtract => _adapter.Subtract(a, childB),
            BooleanOperation.Intersect => _adapter.Intersect(a, childB),
            _ => throw new NotSupportedException($"Unsupported boolean operation: {b.Operation}")
        };
    }
}
