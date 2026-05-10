using System.Globalization;
using System.Text.Json.Serialization;
using FormaCore.Core;

namespace FormaCore.Engine;

public enum CadMode
{
    DirectPrimitive,
    Boolean,
    Sketch,
    Parametric,
    Simulation,
    Cam,
    Slicer,
    OrganicSculpt
}

public enum CadEntityKind
{
    None,
    Project,
    Scene,
    ReferencePlane,
    Body,
    Feature,
    Sketch,
    SketchEntity
}

public enum CadFeatureRole
{
    Create,
    Operation,
    Sketch,
    Derived,
    Placeholder
}

public enum CadReferencePlaneKind
{
    Top,
    Front,
    Right,
    Datum
}

public enum CadPrimitiveKind
{
    Box,
    Cylinder,
    Sphere,
    Cone,
    Torus,
    Pyramid,
    Wedge,
    Ellipsoid,
    Capsule,
    Hemisphere,
    Prism,
    Arrow,
    Icosphere,
    Tetrahedron,
    Octahedron,
    Icosahedron
}

public enum CadSketchToolKind
{
    Line,
    Rectangle,
    Circle,
    Arc,
    Point,
    Polygon,
    Slot,
    Spline,
    Mirror,
    Trim,
    Offset,
    Fillet2d,
    Transform,
    Rotate,
    AngleDimension,
    LinearDimension,
    RadiusDimension
}

public enum CadSketchConstraintKind
{
    Coincident,
    Horizontal,
    Vertical,
    EqualRadius,
    EqualLength,
    Fixed,
    Tangent,
    Parallel,
    Perpendicular,
    Concentric
}

public enum CadSketchDimensionKind
{
    Length,
    Radius,
    Diameter,
    Angle
}

public enum CadFeatureKind
{
    Box,
    Cylinder,
    Sphere,
    Cone,
    Torus,
    Pyramid,
    Wedge,
    Ellipsoid,
    Capsule,
    Hemisphere,
    Prism,
    Arrow,
    Icosphere,
    Tetrahedron,
    Octahedron,
    Icosahedron,
    Sketch,
    Extrude,
    Revolve,
    Sweep,
    Loft,
    Move,
    Fillet,
    Chamfer,
    Shell,
    Mirror,
    LinearPattern,
    CircularPattern,
    BooleanBody,
    Hole,
    Placeholder
}

public enum HoleDepthKind
{
    ThroughAll,
    Blind
}

public enum CadExtrudeOperation
{
    NewBody,
    Join,
    Cut,
    Symmetric
}

public enum CadAxis
{
    X,
    Y,
    Z
}

public enum CadCommandActionKind
{
    CreatePrimitive,
    SelectEntity,
    SelectPlane,
    DeleteSelection,
    MoveSelected,
    StartSketch,
    SetSketchTool,
    PlaceSketchEntity,
    UpdateSketchPreview,
    ClearSketchPreview,
    CancelSketchStep,
    CancelSketch,
    FinishSketch,
    ApplySketchConstraint,
    EditSketchEntityValue,
    DeleteSketchEntity,
    SetSketchEntityConstruction,
    ExtrudeSelectedSketch,
    RevolveSelectedSketch,
    SweepSelectedSketch,
    LoftFromProfiles,
    FilletSelectedBody,
    ChamferSelectedBody,
    ShellSelectedBody,
    MirrorSelectedBody,
    LinearPatternSelectedBody,
    CircularPatternSelectedBody,
    HoleSelectedBody,
    FocusSelection,
    ToggleConstructionMode,
    BooleanUnion,
    BooleanSubtract,
    BooleanIntersect,
    EditSketch,
    RenameBody,
    DuplicateSelectedBody,
    CreateDatumPlane,
    ToggleDatumPlaneVisibility,
    DeleteDatumPlane,
    DeleteSketchConstraint,
    SetBodyColor,
    ToggleBodyVisibility
}

public enum CadDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public readonly record struct CadSelection(CadEntityKind Kind, Guid EntityId, string Name)
{
    public static CadSelection None => new(CadEntityKind.None, Guid.Empty, string.Empty);

    public bool IsEmpty => Kind == CadEntityKind.None || EntityId == Guid.Empty;
}

public sealed record CadParameter(string Name, string DisplayValue, double? NumericValue = null, bool Editable = false)
{
    public static CadParameter Number(string name, double value, bool editable = true)
    {
        return new(
            name,
            value.ToString("0.###", CultureInfo.InvariantCulture),
            value,
            editable);
    }

    public static CadParameter Text(string name, string value)
    {
        return new(name, value, null, false);
    }
}

public sealed record CadCommandAction(
    CadCommandActionKind Kind,
    CadPrimitiveKind? PrimitiveKind = null,
    CadSketchToolKind? SketchTool = null,
    Guid EntityId = default,
    string? EntityName = null,
    CadReferencePlaneKind? PlaneKind = null,
    CadAxis Axis = CadAxis.X,
    double Amount = 0d,
    double U = 0d,
    double V = 0d,
    Guid EntityIdB = default,
    // region: task 31 start
    CadExtrudeOperation ExtrudeOp = CadExtrudeOperation.NewBody,
    Guid TargetBodyId = default);

public sealed record CadCompileDiagnostic(
    CadDiagnosticSeverity Severity,
    string Message,
    Guid? BodyId = null,
    Guid? FeatureId = null);

public sealed class CadProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "My3DApp Project";

    public string Units { get; set; } = "mm";

    public CadMode ActiveMode { get; set; } = CadMode.DirectPrimitive;

    public CadScene Scene { get; set; } = new();

    public CadSelection Selection { get; set; } = CadSelection.None;

    public CadSketchSession? ActiveSketchSession { get; set; }

    // Transient: face clicked in viewport, not persisted to JSON
    [JsonIgnore]
    public CadFacePlane? SelectedFacePlane { get; set; }
}

public sealed class CadScene
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Scene";

    public List<CadBody> Bodies { get; set; } = [];

    public List<CadReferencePlane> ReferencePlanes { get; set; } = CadReferencePlane.CreateDefaults();

    public void EnsureReferencePlanes()
    {
        ReferencePlanes ??= [];

        EnsurePlane(CadReferencePlaneKind.Top, "Top");
        EnsurePlane(CadReferencePlaneKind.Front, "Front");
        EnsurePlane(CadReferencePlaneKind.Right, "Right");
    }

    private void EnsurePlane(CadReferencePlaneKind kind, string name)
    {
        if (ReferencePlanes.Any(plane => plane.Kind == kind))
        {
            return;
        }

        ReferencePlanes.Add(new CadReferencePlane
        {
            Kind = kind,
            Name = name
        });
    }
}

public sealed class CadReferencePlane
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Plane";

    public CadReferencePlaneKind Kind { get; set; } = CadReferencePlaneKind.Top;

    public bool Visible { get; set; } = true;

    // Datum-only: which base plane to offset from and by how much.
    public CadReferencePlaneKind? DatumSourceKind { get; set; }
    public double DatumOffsetDistance { get; set; }

    public static List<CadReferencePlane> CreateDefaults()
    {
        return
        [
            new CadReferencePlane { Name = "Top", Kind = CadReferencePlaneKind.Top },
            new CadReferencePlane { Name = "Front", Kind = CadReferencePlaneKind.Front },
            new CadReferencePlane { Name = "Right", Kind = CadReferencePlaneKind.Right }
        ];
    }

    // World-space geometry (used by viewport and sketch entry).
    [System.Text.Json.Serialization.JsonIgnore]
    public (double X, double Y, double Z) WorldOrigin => Kind switch
    {
        CadReferencePlaneKind.Top   => (0, DatumOffsetForKind(CadReferencePlaneKind.Top),  0),
        CadReferencePlaneKind.Front => (0, 0, DatumOffsetForKind(CadReferencePlaneKind.Front)),
        CadReferencePlaneKind.Right => (DatumOffsetForKind(CadReferencePlaneKind.Right), 0, 0),
        CadReferencePlaneKind.Datum => OffsetOrigin(),
        _ => (0, 0, 0)
    };

    [System.Text.Json.Serialization.JsonIgnore]
    public (double X, double Y, double Z) WorldNormal => Kind switch
    {
        CadReferencePlaneKind.Top   => (0, 1, 0),
        CadReferencePlaneKind.Front => (0, 0, 1),
        CadReferencePlaneKind.Right => (1, 0, 0),
        CadReferencePlaneKind.Datum => DatumNormal(),
        _ => (0, 1, 0)
    };

    private double DatumOffsetForKind(CadReferencePlaneKind k) =>
        Kind == CadReferencePlaneKind.Datum && DatumSourceKind == k ? DatumOffsetDistance : 0;

    private (double, double, double) OffsetOrigin() => DatumSourceKind switch
    {
        CadReferencePlaneKind.Top   => (0, DatumOffsetDistance, 0),
        CadReferencePlaneKind.Front => (0, 0, DatumOffsetDistance),
        CadReferencePlaneKind.Right => (DatumOffsetDistance, 0, 0),
        _ => (0, DatumOffsetDistance, 0)
    };

    private (double, double, double) DatumNormal() => DatumSourceKind switch
    {
        CadReferencePlaneKind.Top   => (0, 1, 0),
        CadReferencePlaneKind.Front => (0, 0, 1),
        CadReferencePlaneKind.Right => (1, 0, 0),
        _ => (0, 1, 0)
    };
}

public sealed class CadBody
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Body";

    public bool Visible { get; set; } = true;

    /// <summary>
    /// Optional hex color string (e.g. "#4080c0") for viewport appearance.
    /// Null means use the default kind-based color palette.
    /// </summary>
    public string? Color { get; set; }

    public List<CadFeature> Features { get; set; } = [];

    public CadFeature? BaseFeature => Features.FirstOrDefault(feature => feature.Role == CadFeatureRole.Create);
}

[JsonDerivedType(typeof(BoxFeature), typeDiscriminator: "box")]
[JsonDerivedType(typeof(CylinderFeature), typeDiscriminator: "cylinder")]
[JsonDerivedType(typeof(SphereFeature), typeDiscriminator: "sphere")]
[JsonDerivedType(typeof(ConeFeature), typeDiscriminator: "cone")]
[JsonDerivedType(typeof(TorusFeature), typeDiscriminator: "torus")]
[JsonDerivedType(typeof(PyramidFeature), typeDiscriminator: "pyramid")]
[JsonDerivedType(typeof(WedgeFeature), typeDiscriminator: "wedge")]
[JsonDerivedType(typeof(EllipsoidFeature), typeDiscriminator: "ellipsoid")]
[JsonDerivedType(typeof(CapsuleFeature), typeDiscriminator: "capsule")]
[JsonDerivedType(typeof(HemisphereFeature), typeDiscriminator: "hemisphere")]
[JsonDerivedType(typeof(PrismFeature), typeDiscriminator: "prism")]
[JsonDerivedType(typeof(ArrowFeature), typeDiscriminator: "arrow")]
[JsonDerivedType(typeof(IcosphereFeature), typeDiscriminator: "icosphere")]
[JsonDerivedType(typeof(TetrahedronFeature), typeDiscriminator: "tetrahedron")]
[JsonDerivedType(typeof(OctahedronFeature), typeDiscriminator: "octahedron")]
[JsonDerivedType(typeof(IcosahedronFeature), typeDiscriminator: "icosahedron")]
[JsonDerivedType(typeof(SketchFeature), typeDiscriminator: "sketch")]
[JsonDerivedType(typeof(ExtrudeFeature), typeDiscriminator: "extrude")]
[JsonDerivedType(typeof(RevolveFeature), typeDiscriminator: "revolve")]
[JsonDerivedType(typeof(SweepFeature), typeDiscriminator: "sweep")]
[JsonDerivedType(typeof(LoftFeature), typeDiscriminator: "loft")]
[JsonDerivedType(typeof(MoveFeature), typeDiscriminator: "move")]
[JsonDerivedType(typeof(FilletFeature), typeDiscriminator: "fillet")]
[JsonDerivedType(typeof(ChamferFeature), typeDiscriminator: "chamfer")]
[JsonDerivedType(typeof(ShellFeature), typeDiscriminator: "shell")]
[JsonDerivedType(typeof(MirrorFeature), typeDiscriminator: "mirror")]
[JsonDerivedType(typeof(LinearPatternFeature), typeDiscriminator: "linearpattern")]
[JsonDerivedType(typeof(CircularPatternFeature), typeDiscriminator: "circularpattern")]
[JsonDerivedType(typeof(BooleanFeature), typeDiscriminator: "boolean")]
[JsonDerivedType(typeof(HoleFeature), typeDiscriminator: "hole")]
[JsonDerivedType(typeof(PlaceholderFeature), typeDiscriminator: "placeholder")]
public abstract class CadFeature
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Feature";

    public bool Suppressed { get; set; }

    public abstract CadFeatureKind Kind { get; }

    public abstract CadFeatureRole Role { get; }

    public abstract IReadOnlyList<CadParameter> GetParameters();

    public abstract bool TrySetParameter(string key, double value);
}

public abstract class PrimitiveFeature : CadFeature
{
    public override CadFeatureRole Role => CadFeatureRole.Create;

    public abstract Solid CreateSolid();
}

public sealed class BoxFeature : PrimitiveFeature
{
    public double Width { get; set; } = 24d;

    public double Depth { get; set; } = 24d;

    public double Height { get; set; } = 18d;

    public override CadFeatureKind Kind => CadFeatureKind.Box;

    public override Solid CreateSolid() => new BoxSolid(Width, Depth, Height) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Width", Width),
        CadParameter.Number("Depth", Depth),
        CadParameter.Number("Height", Height)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Width":
                Width = value;
                return true;
            case "Depth":
                Depth = value;
                return true;
            case "Height":
                Height = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class CylinderFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 12d;

    public double Height { get; set; } = 18d;

    public override CadFeatureKind Kind => CadFeatureKind.Cylinder;

    public override Solid CreateSolid() => new CylinderSolid(Radius, Height) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius),
        CadParameter.Number("Height", Height)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Radius":
                Radius = value;
                return true;
            case "Height":
                Height = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class SphereFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 12d;

    public override CadFeatureKind Kind => CadFeatureKind.Sphere;

    public override Solid CreateSolid() => new SphereSolid(Radius) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key != "Radius")
        {
            return false;
        }

        Radius = value;
        return true;
    }
}

public sealed class ConeFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 12d;

    public double Height { get; set; } = 20d;

    public override CadFeatureKind Kind => CadFeatureKind.Cone;

    public override Solid CreateSolid() => new ConeSolid(0d, Radius, Height) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius),
        CadParameter.Number("Height", Height)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Radius":
                Radius = value;
                return true;
            case "Height":
                Height = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class TorusFeature : PrimitiveFeature
{
    public double MajorRadius { get; set; } = 16d;

    public double MinorRadius { get; set; } = 4d;

    public override CadFeatureKind Kind => CadFeatureKind.Torus;

    public override Solid CreateSolid() => new TorusSolid(MajorRadius, MinorRadius) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("MajorRadius", MajorRadius),
        CadParameter.Number("MinorRadius", MinorRadius)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "MajorRadius":
                MajorRadius = value;
                return true;
            case "MinorRadius":
                MinorRadius = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class PyramidFeature : PrimitiveFeature
{
    public double BaseWidth { get; set; } = 20d;

    public double BaseDepth { get; set; } = 20d;

    public double Height { get; set; } = 18d;

    public override CadFeatureKind Kind => CadFeatureKind.Pyramid;

    public override Solid CreateSolid() => new PyramidSolid(BaseWidth, BaseDepth, Height) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("BaseWidth", BaseWidth),
        CadParameter.Number("BaseDepth", BaseDepth),
        CadParameter.Number("Height", Height)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "BaseWidth":
                BaseWidth = value;
                return true;
            case "BaseDepth":
                BaseDepth = value;
                return true;
            case "Height":
                Height = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class WedgeFeature : PrimitiveFeature
{
    public double Width { get; set; } = 20d;

    public double Depth { get; set; } = 20d;

    public double Height { get; set; } = 18d;

    public override CadFeatureKind Kind => CadFeatureKind.Wedge;

    public override Solid CreateSolid() => new WedgeSolid(Width, Depth, Height) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Width", Width),
        CadParameter.Number("Depth", Depth),
        CadParameter.Number("Height", Height)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Width":
                Width = value;
                return true;
            case "Depth":
                Depth = value;
                return true;
            case "Height":
                Height = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class EllipsoidFeature : PrimitiveFeature
{
    public double RadiusX { get; set; } = 14d;

    public double RadiusY { get; set; } = 10d;

    public double RadiusZ { get; set; } = 8d;

    public override CadFeatureKind Kind => CadFeatureKind.Ellipsoid;

    public override Solid CreateSolid() => new EllipsoidSolid(RadiusX, RadiusY, RadiusZ) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("RadiusX", RadiusX),
        CadParameter.Number("RadiusY", RadiusY),
        CadParameter.Number("RadiusZ", RadiusZ)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "RadiusX":
                RadiusX = value;
                return true;
            case "RadiusY":
                RadiusY = value;
                return true;
            case "RadiusZ":
                RadiusZ = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class CapsuleFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 8d;

    public double Height { get; set; } = 20d;

    public override CadFeatureKind Kind => CadFeatureKind.Capsule;

    public override Solid CreateSolid() => new CapsuleSolid(Radius, Height) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius),
        CadParameter.Number("Height", Height)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Radius":
                Radius = value;
                return true;
            case "Height":
                Height = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class HemisphereFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 10d;

    public override CadFeatureKind Kind => CadFeatureKind.Hemisphere;

    public override Solid CreateSolid() => new HemisphereSolid(Radius) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key != "Radius")
        {
            return false;
        }

        Radius = value;
        return true;
    }
}

public sealed class PrismFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 10d;

    public double Height { get; set; } = 18d;

    public int Sides { get; set; } = 6;

    public override CadFeatureKind Kind => CadFeatureKind.Prism;

    public override Solid CreateSolid() => new PrismSolid(Radius, Height, Sides) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius),
        CadParameter.Number("Height", Height),
        CadParameter.Number("Sides", Sides)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Radius":
                Radius = value;
                return true;
            case "Height":
                Height = value;
                return true;
            case "Sides":
                Sides = Math.Clamp((int)Math.Round(value), 3, 64);
                return true;
            default:
                return false;
        }
    }
}

public sealed class ArrowFeature : PrimitiveFeature
{
    public double ShaftRadius { get; set; } = 3d;

    public double ShaftHeight { get; set; } = 16d;

    public double HeadRadius { get; set; } = 7d;

    public double HeadHeight { get; set; } = 10d;

    public override CadFeatureKind Kind => CadFeatureKind.Arrow;

    public override Solid CreateSolid() => new ArrowSolid(ShaftRadius, ShaftHeight, HeadRadius, HeadHeight) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("ShaftRadius", ShaftRadius),
        CadParameter.Number("ShaftHeight", ShaftHeight),
        CadParameter.Number("HeadRadius", HeadRadius),
        CadParameter.Number("HeadHeight", HeadHeight)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "ShaftRadius":
                ShaftRadius = value;
                return true;
            case "ShaftHeight":
                ShaftHeight = value;
                return true;
            case "HeadRadius":
                HeadRadius = value;
                return true;
            case "HeadHeight":
                HeadHeight = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class IcosphereFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 10d;

    public int Subdivisions { get; set; } = 1;

    public override CadFeatureKind Kind => CadFeatureKind.Icosphere;

    public override Solid CreateSolid() => new IcosphereSolid(Radius, Subdivisions) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius),
        CadParameter.Number("Subdivisions", Subdivisions)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Radius":
                Radius = value;
                return true;
            case "Subdivisions":
                Subdivisions = Math.Clamp((int)Math.Round(value), 0, 4);
                return true;
            default:
                return false;
        }
    }
}

public sealed class TetrahedronFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 10d;

    public override CadFeatureKind Kind => CadFeatureKind.Tetrahedron;

    public override Solid CreateSolid() => new TetrahedronSolid(Radius) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key != "Radius")
        {
            return false;
        }

        Radius = value;
        return true;
    }
}

public sealed class OctahedronFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 10d;

    public override CadFeatureKind Kind => CadFeatureKind.Octahedron;

    public override Solid CreateSolid() => new OctahedronSolid(Radius) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key != "Radius")
        {
            return false;
        }

        Radius = value;
        return true;
    }
}

public sealed class IcosahedronFeature : PrimitiveFeature
{
    public double Radius { get; set; } = 10d;

    public override CadFeatureKind Kind => CadFeatureKind.Icosahedron;

    public override Solid CreateSolid() => new IcosahedronSolid(Radius) { Name = Name };

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key != "Radius")
        {
            return false;
        }

        Radius = value;
        return true;
    }
}

[JsonDerivedType(typeof(CadSketchLine), typeDiscriminator: "line")]
[JsonDerivedType(typeof(CadSketchRectangle), typeDiscriminator: "rectangle")]
[JsonDerivedType(typeof(CadSketchCircle), typeDiscriminator: "circle")]
[JsonDerivedType(typeof(CadSketchArc), typeDiscriminator: "arc")]
[JsonDerivedType(typeof(CadSketchPoint), typeDiscriminator: "point")]
[JsonDerivedType(typeof(CadSketchPolygon), typeDiscriminator: "polygon")]
[JsonDerivedType(typeof(CadSketchSlot), typeDiscriminator: "slot")]
[JsonDerivedType(typeof(CadSketchSpline), typeDiscriminator: "spline")]
public abstract class CadSketchEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool IsConstruction { get; set; }

    public bool IsFixed { get; set; }

    public abstract string EntityType { get; }

    public abstract IReadOnlyList<CadParameter> GetParameters();

    public abstract bool TrySetParameter(string key, double value);
}

public sealed class CadSketchLine : CadSketchEntity
{
    public double StartX { get; set; }

    public double StartY { get; set; }

    public double EndX { get; set; }

    public double EndY { get; set; }

    public override string EntityType => "Line";

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("StartX", StartX),
        CadParameter.Number("StartY", StartY),
        CadParameter.Number("EndX", EndX),
        CadParameter.Number("EndY", EndY)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "StartX":
                StartX = value;
                return true;
            case "StartY":
                StartY = value;
                return true;
            case "EndX":
                EndX = value;
                return true;
            case "EndY":
                EndY = value;
                return true;
            case "Length":
            {
                // Scale line from midpoint preserving direction
                var newLen = Math.Max(Math.Abs(value), 0.001d);
                var dx = EndX - StartX;
                var dy = EndY - StartY;
                var curLen = Math.Sqrt(dx * dx + dy * dy);
                if (curLen < 1e-12) return false;
                var mx = (StartX + EndX) / 2d;
                var my = (StartY + EndY) / 2d;
                var scale = newLen / curLen / 2d;
                StartX = mx - dx * scale;
                StartY = my - dy * scale;
                EndX = mx + dx * scale;
                EndY = my + dy * scale;
                return true;
            }
            default:
                return false;
        }
    }
}

public sealed class CadSketchRectangle : CadSketchEntity
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; } = 10d;

    public double Height { get; set; } = 10d;

    public override string EntityType => "Rectangle";

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("X", X),
        CadParameter.Number("Y", Y),
        CadParameter.Number("Width", Width),
        CadParameter.Number("Height", Height)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "X":
                X = value;
                return true;
            case "Y":
                Y = value;
                return true;
            case "Width":
                Width = Math.Max(Math.Abs(value), 0.2d);
                return true;
            case "Height":
                Height = Math.Max(Math.Abs(value), 0.2d);
                return true;
            default:
                return false;
        }
    }
}

public sealed class CadSketchCircle : CadSketchEntity
{
    public double CenterX { get; set; }

    public double CenterY { get; set; }

    public double Radius { get; set; } = 5d;

    public override string EntityType => "Circle";

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("CenterX", CenterX),
        CadParameter.Number("CenterY", CenterY),
        CadParameter.Number("Radius", Radius)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "CenterX":
                CenterX = value;
                return true;
            case "CenterY":
                CenterY = value;
                return true;
            case "Radius":
                Radius = Math.Max(Math.Abs(value), 0.2d);
                return true;
            default:
                return false;
        }
    }
}

public sealed class CadSketchArc : CadSketchEntity
{
    public double CenterX { get; set; }

    public double CenterY { get; set; }

    public double Radius { get; set; } = 5d;

    public double StartAngleDegrees { get; set; }

    public double EndAngleDegrees { get; set; } = 90d;

    public bool CounterClockwise { get; set; } = true;

    public override string EntityType => "Arc";

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("CenterX", CenterX),
        CadParameter.Number("CenterY", CenterY),
        CadParameter.Number("Radius", Radius),
        CadParameter.Number("StartAngle", StartAngleDegrees),
        CadParameter.Number("EndAngle", EndAngleDegrees),
        CadParameter.Number("CounterClockwise", CounterClockwise ? 1d : 0d)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "CenterX":
                CenterX = value;
                return true;
            case "CenterY":
                CenterY = value;
                return true;
            case "Radius":
                Radius = Math.Max(Math.Abs(value), 0.2d);
                return true;
            case "StartAngle":
                StartAngleDegrees = value;
                return true;
            case "EndAngle":
                EndAngleDegrees = value;
                return true;
            case "CounterClockwise":
                CounterClockwise = value >= 0.5d;
                return true;
            default:
                return false;
        }
    }
}

public sealed class CadSketchPoint : CadSketchEntity
{
    public double X { get; set; }

    public double Y { get; set; }

    public override string EntityType => "Point";

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("X", X),
        CadParameter.Number("Y", Y)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "X":
                X = value;
                return true;
            case "Y":
                Y = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class CadSketchPolygon : CadSketchEntity
{
    public double CenterX { get; set; }

    public double CenterY { get; set; }

    public double Radius { get; set; } = 10d;

    public int Sides { get; set; } = 6;

    public override string EntityType => "Polygon";

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("CenterX", CenterX),
        CadParameter.Number("CenterY", CenterY),
        CadParameter.Number("Radius", Radius),
        CadParameter.Number("Sides", Sides)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "CenterX": CenterX = value; return true;
            case "CenterY": CenterY = value; return true;
            case "Radius": Radius = Math.Max(Math.Abs(value), 0.2d); return true;
            case "Sides": Sides = Math.Clamp((int)Math.Round(value), 3, 50); return true;
            default: return false;
        }
    }
}

public sealed class CadSketchSlot : CadSketchEntity
{
    public double Center1X { get; set; }

    public double Center1Y { get; set; }

    public double Center2X { get; set; }

    public double Center2Y { get; set; }

    public double Radius { get; set; } = 5d;

    public override string EntityType => "Slot";

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Center1X", Center1X),
        CadParameter.Number("Center1Y", Center1Y),
        CadParameter.Number("Center2X", Center2X),
        CadParameter.Number("Center2Y", Center2Y),
        CadParameter.Number("Radius", Radius)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Center1X": Center1X = value; return true;
            case "Center1Y": Center1Y = value; return true;
            case "Center2X": Center2X = value; return true;
            case "Center2Y": Center2Y = value; return true;
            case "Radius": Radius = Math.Max(Math.Abs(value), 0.2d); return true;
            default: return false;
        }
    }
}

public sealed class CadSketchSpline : CadSketchEntity
{
    // Flat list: [x0,y0, x1,y1, ...] for control points.
    public List<double> ControlPointsXY { get; set; } = [];

    public override string EntityType => "Spline";

    public override IReadOnlyList<CadParameter> GetParameters()
    {
        var count = ControlPointsXY.Count / 2;
        return [CadParameter.Number("ControlPoints", count, editable: false)];
    }

    public override bool TrySetParameter(string key, double value) => false;
}

public sealed record CadSketchConstraint(
    CadSketchConstraintKind Kind,
    string Description,
    IReadOnlyList<Guid> EntityIds);

public sealed record CadSketchDimension(
    CadSketchDimensionKind Kind,
    string Label,
    double Value,
    IReadOnlyList<Guid> EntityIds,
    string? ParameterKey = null,
    bool IsDriven = false);

public sealed class SketchFeature : CadFeature
{
    public Guid PlaneId { get; set; }

    public string PlaneName { get; set; } = "Top";

    // Populated when the sketch was created on an existing body face
    public CadFacePlane? FacePlane { get; set; }

    public List<CadSketchEntity> Entities { get; set; } = [];

    public List<CadSketchConstraint> Constraints { get; set; } = [];

    public List<CadSketchDimension> Dimensions { get; set; } = [];

    public bool IsClosedProfile => ProfileBuilder.CanBuildClosedProfile(Entities);

    public override CadFeatureKind Kind => CadFeatureKind.Sketch;

    public override CadFeatureRole Role => CadFeatureRole.Sketch;

    public override IReadOnlyList<CadParameter> GetParameters()
    {
        var constraints = GetBasicConstraints();
        var parameters = new List<CadParameter>
        {
            CadParameter.Text("Plane", PlaneName),
            CadParameter.Number("Entities", Entities.Count, editable: false),
            CadParameter.Number("Constraints", constraints.Count, editable: false),
            CadParameter.Number("Dimensions", GetBasicDimensions().Count, editable: false),
            CadParameter.Text("Closed", IsClosedProfile ? "Yes" : "No")
        };

        if (Entities.Count != 1)
        {
            return parameters;
        }

        switch (Entities[0])
        {
            case CadSketchLine line:
                parameters.Add(CadParameter.Text("Profile", "Line"));
                parameters.Add(CadParameter.Number("StartX", line.StartX));
                parameters.Add(CadParameter.Number("StartY", line.StartY));
                parameters.Add(CadParameter.Number("EndX", line.EndX));
                parameters.Add(CadParameter.Number("EndY", line.EndY));
                break;

            case CadSketchRectangle rectangle:
                parameters.Add(CadParameter.Text("Profile", "Rectangle"));
                parameters.Add(CadParameter.Number("X", rectangle.X));
                parameters.Add(CadParameter.Number("Y", rectangle.Y));
                parameters.Add(CadParameter.Number("Width", rectangle.Width));
                parameters.Add(CadParameter.Number("Height", rectangle.Height));
                break;

            case CadSketchCircle circle:
                parameters.Add(CadParameter.Text("Profile", "Circle"));
                parameters.Add(CadParameter.Number("CenterX", circle.CenterX));
                parameters.Add(CadParameter.Number("CenterY", circle.CenterY));
                parameters.Add(CadParameter.Number("Radius", circle.Radius));
                break;

            case CadSketchArc arc:
                parameters.Add(CadParameter.Text("Profile", "Arc"));
                parameters.Add(CadParameter.Number("CenterX", arc.CenterX));
                parameters.Add(CadParameter.Number("CenterY", arc.CenterY));
                parameters.Add(CadParameter.Number("Radius", arc.Radius));
                parameters.Add(CadParameter.Number("StartAngle", arc.StartAngleDegrees));
                parameters.Add(CadParameter.Number("EndAngle", arc.EndAngleDegrees));
                break;

            case CadSketchPoint point:
                parameters.Add(CadParameter.Text("Profile", "Point"));
                parameters.Add(CadParameter.Number("X", point.X));
                parameters.Add(CadParameter.Number("Y", point.Y));
                break;

            case CadSketchPolygon polygon:
                parameters.Add(CadParameter.Text("Profile", "Polygon"));
                parameters.Add(CadParameter.Number("CenterX", polygon.CenterX));
                parameters.Add(CadParameter.Number("CenterY", polygon.CenterY));
                parameters.Add(CadParameter.Number("Radius", polygon.Radius));
                parameters.Add(CadParameter.Number("Sides", polygon.Sides));
                break;

            case CadSketchSlot slot:
                parameters.Add(CadParameter.Text("Profile", "Slot"));
                parameters.Add(CadParameter.Number("Center1X", slot.Center1X));
                parameters.Add(CadParameter.Number("Center1Y", slot.Center1Y));
                parameters.Add(CadParameter.Number("Center2X", slot.Center2X));
                parameters.Add(CadParameter.Number("Center2Y", slot.Center2Y));
                parameters.Add(CadParameter.Number("Radius", slot.Radius));
                break;

            case CadSketchSpline spline:
                parameters.Add(CadParameter.Text("Profile", "Spline"));
                parameters.Add(CadParameter.Number("ControlPoints", spline.ControlPointsXY.Count / 2, editable: false));
                break;
        }

        return parameters;
    }

    public override bool TrySetParameter(string key, double value)
    {
        if (Entities.Count != 1)
        {
            return false;
        }

        var changed = Entities[0] switch
        {
            CadSketchLine line => TrySetLineParameter(line, key, value),
            CadSketchRectangle rectangle => TrySetRectangleParameter(rectangle, key, value),
            CadSketchCircle circle => TrySetCircleParameter(circle, key, value),
            CadSketchArc arc => TrySetArcParameter(arc, key, value),
            CadSketchPoint point => TrySetPointParameter(point, key, value),
            CadSketchPolygon polygon => polygon.TrySetParameter(key, value),
            CadSketchSlot slot => slot.TrySetParameter(key, value),
            CadSketchSpline spline => spline.TrySetParameter(key, value),
            _ => false
        };

        if (changed)
        {
            Constraints = InferBasicConstraints(Entities);
            Dimensions = InferBasicDimensions(Entities);
        }

        return changed;
    }

    public IReadOnlyList<CadSketchConstraint> GetBasicConstraints()
    {
        return Constraints.Count > 0
            ? Constraints
            : InferBasicConstraints(Entities);
    }

    public IReadOnlyList<CadSketchDimension> GetBasicDimensions()
    {
        return Dimensions.Count > 0
            ? Dimensions
            : InferBasicDimensions(Entities);
    }

    private static List<CadSketchConstraint> InferBasicConstraints(IReadOnlyList<CadSketchEntity> entities)
    {
        var constraints = new List<CadSketchConstraint>();

        foreach (var entity in entities)
        {
            if (entity.IsFixed)
            {
                constraints.Add(new CadSketchConstraint(
                    CadSketchConstraintKind.Fixed,
                    $"{entity.EntityType} is fixed in place.",
                    [entity.Id]));
            }

            switch (entity)
            {
                case CadSketchRectangle rectangle:
                    constraints.Add(new CadSketchConstraint(
                        CadSketchConstraintKind.Horizontal,
                        "Rectangle top/bottom locked horizontal.",
                        [rectangle.Id]));
                    constraints.Add(new CadSketchConstraint(
                        CadSketchConstraintKind.Vertical,
                        "Rectangle left/right locked vertical.",
                        [rectangle.Id]));
                    break;

                case CadSketchCircle circle:
                    constraints.Add(new CadSketchConstraint(
                        CadSketchConstraintKind.EqualRadius,
                        "Circle maintains a constant radius.",
                        [circle.Id]));
                    break;

                case CadSketchArc arc:
                    constraints.Add(new CadSketchConstraint(
                        CadSketchConstraintKind.EqualRadius,
                        "Arc maintains a constant radius.",
                        [arc.Id]));
                    break;
            }
        }

        var geometry = entities.Where(entity => entity is not CadSketchPoint).ToList();
        for (var index = 0; index < geometry.Count; index++)
        {
            var currentEntity = geometry[index];

            if (currentEntity is CadSketchLine currentLine)
            {
                if (Math.Abs(currentLine.StartY - currentLine.EndY) <= 0.0001d)
                {
                    constraints.Add(new CadSketchConstraint(
                        CadSketchConstraintKind.Horizontal,
                        $"Line {index + 1} is horizontal.",
                        [currentLine.Id]));
                }

                if (Math.Abs(currentLine.StartX - currentLine.EndX) <= 0.0001d)
                {
                    constraints.Add(new CadSketchConstraint(
                        CadSketchConstraintKind.Vertical,
                        $"Line {index + 1} is vertical.",
                        [currentLine.Id]));
                }
            }

            var nextEntity = geometry[(index + 1) % geometry.Count];
            if (!TryGetEndpoints(currentEntity, out var currentEnd, out _) ||
                !TryGetEndpoints(nextEntity, out _, out var nextStart) ||
                !PointsEqual(currentEnd.X, currentEnd.Y, nextStart.X, nextStart.Y))
            {
                continue;
            }

            constraints.Add(new CadSketchConstraint(
                CadSketchConstraintKind.Coincident,
                $"Entities {index + 1} and {((index + 1) % geometry.Count) + 1} share a coincident endpoint.",
                [currentEntity.Id, nextEntity.Id]));
        }

        return constraints;
    }

    private static List<CadSketchDimension> InferBasicDimensions(IReadOnlyList<CadSketchEntity> entities)
    {
        var dimensions = new List<CadSketchDimension>();
        var lineIndex = 1;
        var circleIndex = 1;
        var arcIndex = 1;

        foreach (var entity in entities)
        {
            switch (entity)
            {
                case CadSketchRectangle rect:
                    dimensions.Add(new CadSketchDimension(
                        CadSketchDimensionKind.Length, "W", rect.Width, [rect.Id], "Width"));
                    dimensions.Add(new CadSketchDimension(
                        CadSketchDimensionKind.Length, "H", rect.Height, [rect.Id], "Height"));
                    break;

                case CadSketchLine line:
                    dimensions.Add(new CadSketchDimension(
                        CadSketchDimensionKind.Length,
                        $"L{lineIndex}",
                        Math.Sqrt(Math.Pow(line.EndX - line.StartX, 2) + Math.Pow(line.EndY - line.StartY, 2)),
                        [line.Id]));
                    lineIndex++;
                    break;

                case CadSketchCircle circle:
                    dimensions.Add(new CadSketchDimension(
                        CadSketchDimensionKind.Radius,
                        $"R{circleIndex}",
                        circle.Radius,
                        [circle.Id],
                        "Radius"));
                    circleIndex++;
                    break;

                case CadSketchArc arc:
                    dimensions.Add(new CadSketchDimension(
                        CadSketchDimensionKind.Radius,
                        $"R{arcIndex}",
                        arc.Radius,
                        [arc.Id],
                        "Radius"));
                    arcIndex++;
                    break;
            }
        }

        return dimensions;
    }

    private static bool PointsEqual(double ax, double ay, double bx, double by)
    {
        const double tolerance = 0.0001d;
        return Math.Abs(ax - bx) <= tolerance && Math.Abs(ay - by) <= tolerance;
    }

    private static bool TrySetRectangleParameter(CadSketchRectangle rectangle, string key, double value)
    {
        return rectangle.TrySetParameter(key, value);
    }

    private static bool TrySetLineParameter(CadSketchLine line, string key, double value)
    {
        return line.TrySetParameter(key, value);
    }

    private static bool TrySetCircleParameter(CadSketchCircle circle, string key, double value)
    {
        return circle.TrySetParameter(key, value);
    }

    private static bool TrySetArcParameter(CadSketchArc arc, string key, double value)
    {
        return arc.TrySetParameter(key, value);
    }

    private static bool TrySetPointParameter(CadSketchPoint point, string key, double value)
    {
        return point.TrySetParameter(key, value);
    }

    private static bool TryGetEndpoints(CadSketchEntity entity, out Vector2D end, out Vector2D start)
    {
        switch (entity)
        {
            case CadSketchLine line:
                start = new Vector2D(line.StartX, line.StartY);
                end = new Vector2D(line.EndX, line.EndY);
                return true;

            case CadSketchArc arc:
                start = ArcPoint(arc, arc.StartAngleDegrees);
                end = ArcPoint(arc, arc.EndAngleDegrees);
                return true;

            default:
                start = default;
                end = default;
                return false;
        }
    }

    private static Vector2D ArcPoint(CadSketchArc arc, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Vector2D(
            arc.CenterX + (arc.Radius * Math.Cos(radians)),
            arc.CenterY + (arc.Radius * Math.Sin(radians)));
    }
}

public sealed class ExtrudeFeature : CadFeature
{
    public Guid SketchFeatureId { get; set; }

    public string SketchName { get; set; } = "Sketch";

    public double Depth { get; set; } = 8d;

    public bool ReverseDirection { get; set; }

    public bool Symmetric { get; set; }

    public double TaperAngleDegrees { get; set; }

    public CadExtrudeOperation Operation { get; set; } = CadExtrudeOperation.NewBody;

    // region: task 31 start
    public Guid TargetBodyId { get; set; }

    public string TargetBodyName { get; set; } = string.Empty;
    // region: task 31 end

    public override CadFeatureKind Kind => CadFeatureKind.Extrude;

    public override CadFeatureRole Role => CadFeatureRole.Derived;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Text("Sketch", SketchName),
        CadParameter.Number("Depth", Depth),
        CadParameter.Number("Reverse", ReverseDirection ? 1d : 0d),
        CadParameter.Number("Symmetric", Symmetric ? 1d : 0d),
        CadParameter.Number("Taper", TaperAngleDegrees),
        CadParameter.Text("Operation", Operation.ToString())
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Depth": Depth = value; return true;
            case "Reverse": ReverseDirection = value >= 0.5d; return true;
            case "Symmetric": Symmetric = value >= 0.5d; return true;
            case "Taper": TaperAngleDegrees = Math.Clamp(value, 0d, 45d); return true;
            default: return false;
        }
    }
}

public sealed class RevolveFeature : CadFeature
{
    public Guid SketchFeatureId { get; set; }

    public string SketchName { get; set; } = "Sketch";

    public double AngleDegrees { get; set; } = 360d;

    public CadAxis Axis { get; set; } = CadAxis.Y;

    public override CadFeatureKind Kind => CadFeatureKind.Revolve;

    public override CadFeatureRole Role => CadFeatureRole.Derived;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Text("Sketch", SketchName),
        CadParameter.Number("Angle", AngleDegrees),
        CadParameter.Text("Axis", Axis.ToString())
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key != "Angle")
        {
            return false;
        }

        AngleDegrees = value;
        return true;
    }
}

public sealed class SweepFeature : CadFeature
{
    public Guid SketchFeatureId { get; set; }

    public string SketchName { get; set; } = "Sketch";

    public double Distance { get; set; } = 20d;

    public double TwistDegrees { get; set; } = 0d;

    public override CadFeatureKind Kind => CadFeatureKind.Sweep;

    public override CadFeatureRole Role => CadFeatureRole.Derived;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Text("Sketch", SketchName),
        CadParameter.Number("Distance", Distance),
        CadParameter.Number("Twist", TwistDegrees)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Distance": Distance = Math.Clamp(value, 0.1d, 10000d); return true;
            case "Twist": TwistDegrees = Math.Clamp(value, -360d, 360d); return true;
            default: return false;
        }
    }
}

public sealed class LoftFeature : CadFeature
{
    public Guid ProfileASketchId { get; set; }

    public string ProfileASketchName { get; set; } = "Profile A";

    public Guid ProfileBSketchId { get; set; }

    public string ProfileBSketchName { get; set; } = "Profile B";

    public double Distance { get; set; } = 20d;

    public override CadFeatureKind Kind => CadFeatureKind.Loft;

    public override CadFeatureRole Role => CadFeatureRole.Derived;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Text("ProfileA", ProfileASketchName),
        CadParameter.Text("ProfileB", ProfileBSketchName),
        CadParameter.Number("Distance", Distance)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key == "Distance") { Distance = Math.Clamp(value, 0.1d, 10000d); return true; }
        return false;
    }
}

public sealed class MoveFeature : CadFeature
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }

    public override CadFeatureKind Kind => CadFeatureKind.Move;

    public override CadFeatureRole Role => CadFeatureRole.Operation;

    public Solid Apply(Solid current)
    {
        return new TransformedSolid(current, Transform3D.CreateTranslation(X, Y, Z))
        {
            Name = Name
        };
    }

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("X", X),
        CadParameter.Number("Y", Y),
        CadParameter.Number("Z", Z)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "X":
                X = value;
                return true;
            case "Y":
                Y = value;
                return true;
            case "Z":
                Z = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class FilletFeature : CadFeature
{
    public double Radius { get; set; } = 2d;

    public override CadFeatureKind Kind => CadFeatureKind.Fillet;

    public override CadFeatureRole Role => CadFeatureRole.Operation;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Radius", Radius)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key != "Radius") return false;
        Radius = Math.Max(0.1d, value);
        return true;
    }
}

public sealed class ChamferFeature : CadFeature
{
    public double Distance { get; set; } = 2d;

    public override CadFeatureKind Kind => CadFeatureKind.Chamfer;

    public override CadFeatureRole Role => CadFeatureRole.Operation;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Distance", Distance)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key != "Distance") return false;
        Distance = Math.Max(0.1d, value);
        return true;
    }
}

public sealed class ShellFeature : CadFeature
{
    public double Thickness { get; set; } = 2d;

    public override CadFeatureKind Kind => CadFeatureKind.Shell;

    public override CadFeatureRole Role => CadFeatureRole.Operation;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Thickness", Thickness)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        if (key != "Thickness") return false;
        Thickness = Math.Max(0.1d, value);
        return true;
    }
}

public sealed class MirrorFeature : CadFeature
{
    public MirrorAxis Axis { get; set; } = MirrorAxis.X;

    public override CadFeatureKind Kind => CadFeatureKind.Mirror;

    public override CadFeatureRole Role => CadFeatureRole.Operation;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Text("Axis", Axis.ToString())
    ];

    public override bool TrySetParameter(string key, double value) => false;
}

public sealed class LinearPatternFeature : CadFeature
{
    public int Count { get; set; } = 3;

    public double Spacing { get; set; } = 20d;

    public CadAxis Axis { get; set; } = CadAxis.X;

    public override CadFeatureKind Kind => CadFeatureKind.LinearPattern;

    public override CadFeatureRole Role => CadFeatureRole.Operation;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Count", Count),
        CadParameter.Number("Spacing", Spacing),
        CadParameter.Text("Axis", Axis.ToString())
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Count":
                Count = Math.Max(2, (int)Math.Round(value));
                return true;
            case "Spacing":
                Spacing = Math.Max(0.1d, value);
                return true;
            default:
                return false;
        }
    }
}

public sealed class CircularPatternFeature : CadFeature
{
    public int Count { get; set; } = 4;

    public double TotalAngle { get; set; } = 360d;

    public CadAxis Axis { get; set; } = CadAxis.Y;

    public override CadFeatureKind Kind => CadFeatureKind.CircularPattern;

    public override CadFeatureRole Role => CadFeatureRole.Operation;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Count", Count),
        CadParameter.Number("TotalAngle", TotalAngle),
        CadParameter.Text("Axis", Axis.ToString())
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Count":
                Count = Math.Max(2, (int)Math.Round(value));
                return true;
            case "TotalAngle":
                TotalAngle = Math.Max(1d, value);
                return true;
            default:
                return false;
        }
    }
}

public sealed class BooleanFeature : CadFeature
{
    public Guid BodyAId { get; set; }

    public Guid BodyBId { get; set; }

    public BooleanOperation Operation { get; set; } = BooleanOperation.Union;

    public string BodyAName { get; set; } = "A";

    public string BodyBName { get; set; } = "B";

    public override CadFeatureKind Kind => CadFeatureKind.BooleanBody;

    public override CadFeatureRole Role => CadFeatureRole.Create;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Text("Operation", Operation.ToString()),
        CadParameter.Text("Body A", BodyAName),
        CadParameter.Text("Body B", BodyBName)
    ];

    public override bool TrySetParameter(string key, double value) => false;
}

public sealed class HoleFeature : CadFeature
{
    public double Diameter { get; set; } = 10d;

    public HoleDepthKind DepthKind { get; set; } = HoleDepthKind.ThroughAll;

    public double DepthValue { get; set; } = 20d;

    public double CenterOffsetX { get; set; }

    public double CenterOffsetY { get; set; }

    public override CadFeatureKind Kind => CadFeatureKind.Hole;

    public override CadFeatureRole Role => CadFeatureRole.Operation;

    public string DepthLabel => DepthKind == HoleDepthKind.ThroughAll ? "Through" : $"{DepthValue:0.###}mm";

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("Diameter", Diameter),
        CadParameter.Text("Depth", DepthKind.ToString()),
        CadParameter.Number("DepthValue", DepthValue),
        CadParameter.Number("CenterOffsetX", CenterOffsetX),
        CadParameter.Number("CenterOffsetY", CenterOffsetY)
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Diameter":
                Diameter = Math.Max(0.01d, value);
                return true;
            case "DepthValue":
                DepthValue = Math.Max(0.01d, value);
                return true;
            case "CenterOffsetX":
                CenterOffsetX = value;
                return true;
            case "CenterOffsetY":
                CenterOffsetY = value;
                return true;
            default:
                return false;
        }
    }
}

public sealed class PlaceholderFeature : CadFeature
{
    public string PlaceholderKind { get; set; } = "Future";

    public override CadFeatureKind Kind => CadFeatureKind.Placeholder;

    public override CadFeatureRole Role => CadFeatureRole.Placeholder;

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Text("PlaceholderKind", PlaceholderKind)
    ];

    public override bool TrySetParameter(string key, double value) => false;
}

// Face plane data: a flat face on an existing body used as sketch plane
public sealed class CadFacePlane
{
    public Guid BodyId { get; set; }

    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double OriginZ { get; set; }

    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; }

    // U axis (first tangent direction in the face plane)
    public double UAxisX { get; set; }
    public double UAxisY { get; set; }
    public double UAxisZ { get; set; }

    // V axis = Normal × U (second tangent direction; stored for robustness)
    public double VAxisX { get; set; }
    public double VAxisY { get; set; }
    public double VAxisZ { get; set; }

    public FacePlaneData ToFacePlaneData() => new(
        OriginX, OriginY, OriginZ,
        UAxisX, UAxisY, UAxisZ,
        VAxisX, VAxisY, VAxisZ,
        NormalX, NormalY, NormalZ);
}

public sealed class CadSketchSession
{
    public Guid PlaneId { get; set; }

    public string PlaneName { get; set; } = "Top";

    // Non-null when the sketch is on a body face rather than a reference plane
    [JsonIgnore]
    public CadFacePlane? FacePlane { get; set; }

    public CadSketchToolKind ActiveTool { get; set; } = CadSketchToolKind.Rectangle;

    public bool IsConstructionModeActive { get; set; }

    public List<CadSketchEntity> DraftEntities { get; set; } = [];

    public List<CadSketchEntity> PreviewEntities { get; set; } = [];

    public Vector2D? PendingLineStart { get; set; }

    public Vector2D? PendingShapeAnchor { get; set; }

    public List<Vector2D> PendingSketchPoints { get; set; } = [];

    public int PendingPolygonSides { get; set; } = 6;

    public double PendingOffsetDistance { get; set; } = 1.0d;

    public double PendingFilletRadius { get; set; } = 1.0d;

    public Guid? PendingAngleDimFirstId { get; set; }

    public Guid? PendingLinearDimFirstId { get; set; }

    public List<CadSketchConstraint> ManualConstraints { get; set; } = [];

    public List<CadSketchDimension> ManualDimensions { get; set; } = [];

    public Guid EditingSketchId { get; set; }
}

public sealed class CadCompiledPlane
{
    public Guid PlaneId { get; init; }

    public string Name { get; init; } = string.Empty;

    public CadReferencePlaneKind Kind { get; init; }

    public bool Visible { get; init; }
}

public sealed class CadCompiledSketch
{
    public Guid BodyId { get; init; }

    public Guid SketchId { get; init; }

    public string BodyName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string PlaneName { get; init; } = string.Empty;

    public int EntityCount { get; init; }

    public bool IsClosedProfile { get; init; }
}

public sealed class CadCompiledBody
{
    public Guid BodyId { get; init; }

    public string Name { get; init; } = string.Empty;

    public CadFeatureKind SourceKind { get; init; }

    public Solid Solid { get; init; } = new BoxSolid(1d, 1d, 1d);

    public IReadOnlyList<Guid> FeatureIds { get; init; } = [];

    public IReadOnlyList<CadParameter> Parameters { get; init; } = [];
}

public sealed class CadCompileResult
{
    public Guid ProjectId { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public string Units { get; init; } = "mm";

    public CadMode Mode { get; init; } = CadMode.DirectPrimitive;

    public string ActivePlaneName { get; init; } = "Top";

    public CadSelection Selection { get; init; } = CadSelection.None;

    public IReadOnlyList<CadCompiledPlane> Planes { get; init; } = [];

    public IReadOnlyList<CadCompiledBody> Bodies { get; init; } = [];

    public IReadOnlyList<CadCompiledSketch> Sketches { get; init; } = [];

    public IReadOnlyList<CadCompileDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class CadActionResult
{
    public bool IsSuccess { get; init; }

    public bool Mutated { get; init; }

    public string Message { get; init; } = string.Empty;

    public CadCompileResult Snapshot { get; init; } = new();

    public static CadActionResult Success(string message, bool mutated, CadCompileResult snapshot)
    {
        return new()
        {
            IsSuccess = true,
            Mutated = mutated,
            Message = message,
            Snapshot = snapshot
        };
    }

    public static CadActionResult Failure(string message, CadCompileResult snapshot)
    {
        return new()
        {
            IsSuccess = false,
            Mutated = false,
            Message = message,
            Snapshot = snapshot
        };
    }
}
