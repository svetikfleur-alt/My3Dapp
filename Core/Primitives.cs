namespace FormaCore.Core;

// --- Box ---
public sealed class BoxNode : ShapeNode
{
    public double Width { get; }
    public double Depth { get; }
    public double Height { get; }

    public BoxNode(double width, double depth, double height)
    {
        Width = width;
        Depth = depth;
        Height = height;
    }
}

// --- Cylinder ---
public sealed class CylinderNode : ShapeNode
{
    public double Radius { get; }
    public double Height { get; }

    public CylinderNode(double radius, double height)
    {
        Radius = radius;
        Height = height;
    }
}

// --- Sphere ---
public sealed class SphereNode : ShapeNode
{
    public double Radius { get; }

    public SphereNode(double radius)
    {
        Radius = radius;
    }
}

// --- Cone ---
public sealed class ConeNode : ShapeNode
{
    public double RadiusTop { get; }
    public double RadiusBottom { get; }
    public double Height { get; }

    public ConeNode(double rTop, double rBottom, double height)
    {
        RadiusTop = rTop;
        RadiusBottom = rBottom;
        Height = height;
    }
}

// --- Pipe (полый цилиндр) ---
public sealed class PipeNode : ShapeNode
{
    public double OuterRadius { get; }
    public double InnerRadius { get; }
    public double Height { get; }

    public PipeNode(double outer, double inner, double height)
    {
        OuterRadius = outer;
        InnerRadius = inner;
        Height = height;
    }
}

// --- Torus (кольцо) ---
public sealed class TorusNode : ShapeNode
{
    public double MajorRadius { get; }
    public double MinorRadius { get; }

    public TorusNode(double major, double minor)
    {
        MajorRadius = major;
        MinorRadius = minor;
    }
}

// --- Pyramid ---
public sealed class PyramidNode : ShapeNode
{
    public double BaseWidth { get; }
    public double BaseDepth { get; }
    public double Height { get; }

    public PyramidNode(double baseWidth, double baseDepth, double height)
    {
        BaseWidth = baseWidth;
        BaseDepth = baseDepth;
        Height = height;
    }
}

// --- Wedge ---
public sealed class WedgeNode : ShapeNode
{
    public double Width { get; }
    public double Depth { get; }
    public double Height { get; }

    public WedgeNode(double width, double depth, double height)
    {
        Width = width;
        Depth = depth;
        Height = height;
    }
}

// --- Ellipsoid ---
public sealed class EllipsoidNode : ShapeNode
{
    public double RadiusX { get; }
    public double RadiusY { get; }
    public double RadiusZ { get; }

    public EllipsoidNode(double radiusX, double radiusY, double radiusZ)
    {
        RadiusX = radiusX;
        RadiusY = radiusY;
        RadiusZ = radiusZ;
    }
}

// --- Capsule ---
public sealed class CapsuleNode : ShapeNode
{
    public double Radius { get; }
    public double Height { get; }

    public CapsuleNode(double radius, double height)
    {
        Radius = radius;
        Height = height;
    }
}

// --- Hemisphere ---
public sealed class HemisphereNode : ShapeNode
{
    public double Radius { get; }

    public HemisphereNode(double radius)
    {
        Radius = radius;
    }
}

// --- Prism (n-sided) ---
public sealed class PrismNode : ShapeNode
{
    public double Radius { get; }
    public double Height { get; }
    public int Sides { get; }

    public PrismNode(double radius, double height, int sides)
    {
        Radius = radius;
        Height = height;
        Sides = sides;
    }
}

// --- Disk / Ring ---
public sealed class DiskNode : ShapeNode
{
    public double OuterRadius { get; }
    public double InnerRadius { get; }

    public DiskNode(double outerRadius, double innerRadius = 0)
    {
        OuterRadius = outerRadius;
        InnerRadius = innerRadius;
    }
}

// --- Arrow ---
public sealed class ArrowNode : ShapeNode
{
    public double ShaftRadius { get; }
    public double ShaftHeight { get; }
    public double HeadRadius { get; }
    public double HeadHeight { get; }

    public ArrowNode(double shaftRadius, double shaftHeight, double headRadius, double headHeight)
    {
        ShaftRadius = shaftRadius;
        ShaftHeight = shaftHeight;
        HeadRadius = headRadius;
        HeadHeight = headHeight;
    }
}

// --- Icosphere ---
public sealed class IcosphereNode : ShapeNode
{
    public double Radius { get; }
    public int Subdivisions { get; }

    public IcosphereNode(double radius, int subdivisions = 2)
    {
        Radius = radius;
        Subdivisions = subdivisions;
    }
}

// --- Tetrahedron ---
public sealed class TetrahedronNode : ShapeNode
{
    public double Radius { get; }

    public TetrahedronNode(double radius)
    {
        Radius = radius;
    }
}

// --- Octahedron ---
public sealed class OctahedronNode : ShapeNode
{
    public double Radius { get; }

    public OctahedronNode(double radius)
    {
        Radius = radius;
    }
}

// --- Icosahedron ---
public sealed class IcosahedronNode : ShapeNode
{
    public double Radius { get; }

    public IcosahedronNode(double radius)
    {
        Radius = radius;
    }
}

// --- Spring / Helix ---
public sealed class SpringNode : ShapeNode
{
    public double CoilRadius { get; }
    public double TubeRadius { get; }
    public int Coils { get; }
    public double Height { get; }

    public SpringNode(double coilRadius, double tubeRadius, int coils, double height)
    {
        CoilRadius = coilRadius;
        TubeRadius = tubeRadius;
        Coils = coils;
        Height = height;
    }
}