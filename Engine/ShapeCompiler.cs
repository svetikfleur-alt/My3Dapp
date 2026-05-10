using FormaCore.Core;

namespace FormaCore.Engine;

public sealed class ShapeCompiler
{
    private readonly IGeometryBackend _backend;

    public ShapeCompiler(IGeometryBackend backend)
    {
        _backend = backend;
    }

    public object Compile(ShapeNode node)
    {
        return node switch
        {
            BoxNode b => _backend.CreateBox(b.Width, b.Depth, b.Height),
            CylinderNode c => _backend.CreateCylinder(c.Radius, c.Height),
            SphereNode s => _backend.CreateSphere(s.Radius),

            ConeNode c => _backend.CreateCone(c.RadiusTop, c.RadiusBottom, c.Height),
            PipeNode p => _backend.CreatePipe(p.OuterRadius, p.InnerRadius, p.Height),
            TorusNode t => _backend.CreateTorus(t.MajorRadius, t.MinorRadius),

            TranslateNode t => _backend.Translate(
                Compile(t.Child), t.X, t.Y, t.Z
            ),

            UnionNode u => _backend.Union(
                Compile(u.A), Compile(u.B)
            ),

            SubtractNode s => _backend.Subtract(
                Compile(s.A), Compile(s.B)
            ),

            _ => throw new NotSupportedException($"Unsupported node: {node.GetType().Name}")
        };
    }
}