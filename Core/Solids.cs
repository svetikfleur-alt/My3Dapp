namespace FormaCore.Core;

public readonly record struct Vector2D(double X, double Y);

public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D Zero => new(0, 0, 0);

    public static Vector3D operator +(Vector3D a, Vector3D b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
}

public readonly record struct Transform3D(Vector3D Translation)
{
    public static Transform3D Identity => new(Vector3D.Zero);

    public static Transform3D CreateTranslation(double x, double y, double z) =>
        new(new Vector3D(x, y, z));

    public Transform3D Combine(Transform3D other) =>
        new(Translation + other.Translation);
}

public abstract record Solid
{
    public string Name { get; init; } = "Unnamed";
}

public abstract record PrimitiveSolid : Solid;

public sealed record BoxSolid(double Width, double Depth, double Height) : PrimitiveSolid;

public sealed record CylinderSolid(double Radius, double Height) : PrimitiveSolid;

public sealed record SphereSolid(double Radius) : PrimitiveSolid;

public sealed record ConeSolid(double RadiusTop, double RadiusBottom, double Height) : PrimitiveSolid;

public sealed record PipeSolid(double OuterRadius, double InnerRadius, double Height) : PrimitiveSolid;

public sealed record TorusSolid(double MajorRadius, double MinorRadius) : PrimitiveSolid;

public sealed record PyramidSolid(double BaseWidth, double BaseDepth, double Height) : PrimitiveSolid;

public sealed record WedgeSolid(double Width, double Depth, double Height) : PrimitiveSolid;

public sealed record EllipsoidSolid(double RadiusX, double RadiusY, double RadiusZ) : PrimitiveSolid;

public sealed record CapsuleSolid(double Radius, double Height) : PrimitiveSolid;

public sealed record HemisphereSolid(double Radius) : PrimitiveSolid;

public sealed record PrismSolid(double Radius, double Height, int Sides) : PrimitiveSolid;

public sealed record PolygonPrismSolid(IReadOnlyList<Vector2D> Profile, double Height) : PrimitiveSolid;

public sealed record DiskSolid(double OuterRadius, double InnerRadius) : PrimitiveSolid;

public sealed record ArrowSolid(double ShaftRadius, double ShaftHeight, double HeadRadius, double HeadHeight) : PrimitiveSolid;

public sealed record IcosphereSolid(double Radius, int Subdivisions) : PrimitiveSolid;

public sealed record TetrahedronSolid(double Radius) : PrimitiveSolid;

public sealed record OctahedronSolid(double Radius) : PrimitiveSolid;

public sealed record IcosahedronSolid(double Radius) : PrimitiveSolid;

public sealed record SpringSolid(double CoilRadius, double TubeRadius, int Coils, double Height) : PrimitiveSolid;

public enum PlaneOrientation { Top, Front, Right }

public enum MirrorAxis { X, Y, Z }

public enum ProfileAxis { X, Y }

// Profile: a closed ordered 2D contour produced by a sketch
public sealed record Profile(IReadOnlyList<Vector2D> Points);

// World-space UV frame for face-plane mapping: origin + orthonormal U/V/N axes
public sealed record FacePlaneData(
    double OriginX, double OriginY, double OriginZ,
    double UX, double UY, double UZ,
    double VX, double VY, double VZ,
    double NX, double NY, double NZ);

// ExtrudeSolid: parametric result of extruding a profile — the source of truth for sketch-based solids
public sealed record ExtrudeSolid(Profile Profile, double Height) : PrimitiveSolid
{
    public PlaneOrientation Plane { get; init; } = PlaneOrientation.Top;
    public bool Symmetric { get; init; }
    public double TaperAngleDegrees { get; init; }
    public double FilletRadius { get; init; }
    public double ChamferDistance { get; init; }
    public double ShellThickness { get; init; }
    // Set when the sketch was on a body face rather than a reference plane
    public FacePlaneData? FacePlane { get; init; }
}

// LinearPatternSolid: N copies of a solid stepped along a direction vector
public sealed record LinearPatternSolid(Solid Child, int Count, double Dx, double Dy, double Dz) : Solid;

// CircularPatternSolid: N copies of a solid rotated around an axis covering TotalAngleDegrees
public sealed record CircularPatternSolid(Solid Child, int Count, double TotalAngleDegrees, MirrorAxis Axis) : Solid;

// RevolveSolid: solid of revolution derived from a 2D sketch profile.
public sealed record RevolveSolid(Profile Profile, double AngleDegrees) : PrimitiveSolid
{
    public PlaneOrientation Plane { get; init; } = PlaneOrientation.Top;
    public ProfileAxis Axis { get; init; } = ProfileAxis.Y;
}

// SweepSolid: solid derived from sweeping a 2D sketch profile along the sketch plane normal.
public sealed record SweepSolid(Profile Profile, double Distance) : PrimitiveSolid
{
    public PlaneOrientation Plane { get; init; } = PlaneOrientation.Top;
    public double TwistDegrees { get; init; } = 0d;
}

// LoftSolid: solid blended between two 2D sketch profiles along the sketch plane normal.
public sealed record LoftSolid(Profile ProfileA, Profile ProfileB, double Distance) : PrimitiveSolid
{
    public PlaneOrientation Plane { get; init; } = PlaneOrientation.Top;
}

// MirrorSolid: child solid reflected across a plane perpendicular to the given axis
public sealed record MirrorSolid(Solid Child, MirrorAxis Axis) : Solid;

public sealed record TransformedSolid(Solid Child, Transform3D Transform) : Solid;

public enum BooleanOperation
{
    Union,
    Subtract,
    Intersect
}

public sealed record BooleanSolid(BooleanOperation Operation, Solid A, Solid B) : Solid;
