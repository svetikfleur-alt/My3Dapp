using FormaCore.Core;

namespace FormaCore.Engine;

public sealed class SolidCompiler
{
    public Solid Compile(ShapeNode node)
    {
        Solid solid = node switch
        {
            BoxNode b => new BoxSolid(b.Width, b.Depth, b.Height),
            CylinderNode c => new CylinderSolid(c.Radius, c.Height),
            SphereNode s => new SphereSolid(s.Radius),
            ConeNode c => new ConeSolid(c.RadiusTop, c.RadiusBottom, c.Height),
            PipeNode p => new PipeSolid(p.OuterRadius, p.InnerRadius, p.Height),
            TorusNode t => new TorusSolid(t.MajorRadius, t.MinorRadius),
            PyramidNode p => new PyramidSolid(p.BaseWidth, p.BaseDepth, p.Height),
            WedgeNode w => new WedgeSolid(w.Width, w.Depth, w.Height),
            EllipsoidNode e => new EllipsoidSolid(e.RadiusX, e.RadiusY, e.RadiusZ),
            CapsuleNode c => new CapsuleSolid(c.Radius, c.Height),
            HemisphereNode h => new HemisphereSolid(h.Radius),
            PrismNode p => new PrismSolid(p.Radius, p.Height, p.Sides),
            DiskNode d => new DiskSolid(d.OuterRadius, d.InnerRadius),
            ArrowNode a => new ArrowSolid(a.ShaftRadius, a.ShaftHeight, a.HeadRadius, a.HeadHeight),
            IcosphereNode i => new IcosphereSolid(i.Radius, i.Subdivisions),
            TetrahedronNode t => new TetrahedronSolid(t.Radius),
            OctahedronNode o => new OctahedronSolid(o.Radius),
            IcosahedronNode i => new IcosahedronSolid(i.Radius),
            SpringNode s => new SpringSolid(s.CoilRadius, s.TubeRadius, s.Coils, s.Height),
            TranslateNode t => new TransformedSolid(
                Compile(t.Child),
                Transform3D.CreateTranslation(t.X, t.Y, t.Z)),
            UnionNode u => new BooleanSolid(
                BooleanOperation.Union,
                Compile(u.A),
                Compile(u.B)),
            SubtractNode s => new BooleanSolid(
                BooleanOperation.Subtract,
                Compile(s.A),
                Compile(s.B)),
            IntersectNode i => new BooleanSolid(
                BooleanOperation.Intersect,
                Compile(i.A),
                Compile(i.B)),
            _ => throw new NotSupportedException($"Unsupported node: {node.GetType().Name}")
        };

        return solid with { Name = node.Name };
    }
}
