using System.Text.Json;
using System.Text.Json.Serialization;
using FormaCore.Core;

namespace My3DApp.Studio;

public enum CadStudioMode
{
    DirectPrimitive,
    Boolean,
    SketchExtrude,
    Parametric,
    Simulation,
    Cam,
    Slicer,
    OrganicSculpt
}

public enum CadFeatureRole
{
    Create,
    Operation,
    Placeholder
}

public enum CadReferencePlaneKind
{
    XY,
    YZ,
    ZX
}

public sealed class CadDocument
{
    public string Name { get; set; } = "My3DApp Project";

    public string Units { get; set; } = "mm";

    public CadStudioMode ActiveMode { get; set; } = CadStudioMode.DirectPrimitive;

    public CadScene Scene { get; set; } = new();
}

public sealed class CadScene
{
    public string Name { get; set; } = "Scene";

    public List<CadBody> Bodies { get; set; } = new();

    public List<CadReferencePlane> ReferencePlanes { get; set; } = CadReferencePlane.CreateDefaults();

    public void EnsureReferencePlanes()
    {
        ReferencePlanes ??= new List<CadReferencePlane>();

        bool HasPlane(CadReferencePlaneKind kind) =>
            ReferencePlanes.Any(plane => plane.Kind == kind);

        if (!HasPlane(CadReferencePlaneKind.XY))
        {
            ReferencePlanes.Add(new CadReferencePlane { Name = "XY Plane", Kind = CadReferencePlaneKind.XY });
        }

        if (!HasPlane(CadReferencePlaneKind.YZ))
        {
            ReferencePlanes.Add(new CadReferencePlane { Name = "YZ Plane", Kind = CadReferencePlaneKind.YZ });
        }

        if (!HasPlane(CadReferencePlaneKind.ZX))
        {
            ReferencePlanes.Add(new CadReferencePlane { Name = "ZX Plane", Kind = CadReferencePlaneKind.ZX });
        }
    }
}

public sealed class CadReferencePlane
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Plane";

    public CadReferencePlaneKind Kind { get; set; } = CadReferencePlaneKind.XY;

    public bool Visible { get; set; } = true;

    public static List<CadReferencePlane> CreateDefaults() =>
        new()
        {
            new CadReferencePlane { Name = "XY Plane", Kind = CadReferencePlaneKind.XY },
            new CadReferencePlane { Name = "YZ Plane", Kind = CadReferencePlaneKind.YZ },
            new CadReferencePlane { Name = "ZX Plane", Kind = CadReferencePlaneKind.ZX }
        };
}

public sealed class CadBody
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Body";

    public List<CadFeature> Features { get; set; } = new();

    public CadFeature? BaseFeature => Features.FirstOrDefault(feature => feature.Role == CadFeatureRole.Create);

    public IEnumerable<CadFeature> OperationFeatures => Features.Where(feature => feature.Role == CadFeatureRole.Operation);
}

[JsonDerivedType(typeof(BoxFeature), typeDiscriminator: "box")]
[JsonDerivedType(typeof(CylinderFeature), typeDiscriminator: "cylinder")]
[JsonDerivedType(typeof(SphereFeature), typeDiscriminator: "sphere")]
[JsonDerivedType(typeof(ConeFeature), typeDiscriminator: "cone")]
[JsonDerivedType(typeof(TorusFeature), typeDiscriminator: "torus")]
[JsonDerivedType(typeof(PyramidFeature), typeDiscriminator: "pyramid")]
[JsonDerivedType(typeof(MoveFeature), typeDiscriminator: "move")]
[JsonDerivedType(typeof(FutureFeaturePlaceholder), typeDiscriminator: "future")]
public abstract class CadFeature
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Feature";

    public abstract string FeatureType { get; }

    public abstract CadFeatureRole Role { get; }

    public abstract Solid Apply(Solid? current);

    public abstract IEnumerable<CadParameter> GetParameters();

    public abstract bool TrySetParameter(string key, double value);
}

public sealed class BoxFeature : CadFeature
{
    public double Width { get; set; } = 24;

    public double Depth { get; set; } = 24;

    public double Height { get; set; } = 18;

    public override string FeatureType => "Box";

    public override CadFeatureRole Role => CadFeatureRole.Create;

    public override Solid Apply(Solid? current)
    {
        return new BoxSolid(Width, Depth, Height) { Name = Name };
    }

    public override IEnumerable<CadParameter> GetParameters()
    {
        yield return new CadParameter("Width", Width);
        yield return new CadParameter("Depth", Depth);
        yield return new CadParameter("Height", Height);
    }

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

public sealed class CylinderFeature : CadFeature
{
    public double Radius { get; set; } = 12;

    public double Height { get; set; } = 16;

    public override string FeatureType => "Cylinder";

    public override CadFeatureRole Role => CadFeatureRole.Create;

    public override Solid Apply(Solid? current)
    {
        return new CylinderSolid(Radius, Height) { Name = Name };
    }

    public override IEnumerable<CadParameter> GetParameters()
    {
        yield return new CadParameter("Radius", Radius);
        yield return new CadParameter("Height", Height);
    }

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

public sealed class SphereFeature : CadFeature
{
    public double Radius { get; set; } = 12;

    public override string FeatureType => "Sphere";

    public override CadFeatureRole Role => CadFeatureRole.Create;

    public override Solid Apply(Solid? current)
    {
        return new SphereSolid(Radius) { Name = Name };
    }

    public override IEnumerable<CadParameter> GetParameters()
    {
        yield return new CadParameter("Radius", Radius);
    }

    public override bool TrySetParameter(string key, double value)
    {
        if (key == "Radius")
        {
            Radius = value;
            return true;
        }

        return false;
    }
}

public sealed class ConeFeature : CadFeature
{
    public double Radius { get; set; } = 12;
    public double Height { get; set; } = 20;

    public override string FeatureType => "Cone";
    public override CadFeatureRole Role => CadFeatureRole.Create;

    public override Solid Apply(Solid? current) =>
        new ConeSolid(0, Radius, Height) { Name = Name };

    public override IEnumerable<CadParameter> GetParameters()
    {
        yield return new CadParameter("Radius", Radius);
        yield return new CadParameter("Height", Height);
    }

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "Radius": Radius = value; return true;
            case "Height": Height = value; return true;
            default: return false;
        }
    }
}

public sealed class TorusFeature : CadFeature
{
    public double MajorRadius { get; set; } = 14;
    public double MinorRadius { get; set; } = 4;

    public override string FeatureType => "Torus";
    public override CadFeatureRole Role => CadFeatureRole.Create;

    public override Solid Apply(Solid? current) =>
        new TorusSolid(MajorRadius, MinorRadius) { Name = Name };

    public override IEnumerable<CadParameter> GetParameters()
    {
        yield return new CadParameter("MajorRadius", MajorRadius);
        yield return new CadParameter("MinorRadius", MinorRadius);
    }

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "MajorRadius": MajorRadius = value; return true;
            case "MinorRadius": MinorRadius = value; return true;
            default: return false;
        }
    }
}

public sealed class PyramidFeature : CadFeature
{
    public double BaseWidth { get; set; } = 20;
    public double BaseDepth { get; set; } = 20;
    public double Height { get; set; } = 18;

    public override string FeatureType => "Pyramid";
    public override CadFeatureRole Role => CadFeatureRole.Create;

    public override Solid Apply(Solid? current) =>
        new PyramidSolid(BaseWidth, BaseDepth, Height) { Name = Name };

    public override IEnumerable<CadParameter> GetParameters()
    {
        yield return new CadParameter("BaseWidth", BaseWidth);
        yield return new CadParameter("BaseDepth", BaseDepth);
        yield return new CadParameter("Height", Height);
    }

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "BaseWidth": BaseWidth = value; return true;
            case "BaseDepth": BaseDepth = value; return true;
            case "Height":    Height = value;    return true;
            default: return false;
        }
    }
}

public sealed class MoveFeature : CadFeature
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }

    public override string FeatureType => "Move";

    public override CadFeatureRole Role => CadFeatureRole.Operation;

    public override Solid Apply(Solid? current)
    {
        if (current is null)
        {
            throw new InvalidOperationException("Move feature requires an existing solid.");
        }

        return new TransformedSolid(current, Transform3D.CreateTranslation(X, Y, Z))
        {
            Name = Name
        };
    }

    public override IEnumerable<CadParameter> GetParameters()
    {
        yield return new CadParameter("X", X);
        yield return new CadParameter("Y", Y);
        yield return new CadParameter("Z", Z);
    }

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

public sealed class FutureFeaturePlaceholder : CadFeature
{
    public string PlaceholderKind { get; set; } = "Future";

    public override string FeatureType => PlaceholderKind;

    public override CadFeatureRole Role => CadFeatureRole.Placeholder;

    public override Solid Apply(Solid? current)
    {
        return current ?? throw new InvalidOperationException(
            $"{PlaceholderKind} placeholder cannot be applied without an existing solid.");
    }

    public override IEnumerable<CadParameter> GetParameters()
    {
        yield break;
    }

    public override bool TrySetParameter(string key, double value) => false;
}

public readonly record struct CadParameter(string Name, double Value);

public sealed class CadDisplayBody
{
    public required CadBody Body { get; init; }

    public required Solid Solid { get; init; }
}

public sealed class CadDesignCompiler
{
    public IReadOnlyList<CadDisplayBody> BuildDisplayBodies(CadDocument document)
    {
        return document.Scene.Bodies
            .Where(static body => body.BaseFeature is not null)
            .Select(BuildDisplayBody)
            .ToList();
    }

    public CadDisplayBody BuildDisplayBody(CadBody body)
    {
        Solid? current = null;

        foreach (var feature in body.Features.Where(static feature => feature.Role != CadFeatureRole.Placeholder))
        {
            current = feature.Apply(current);
        }

        if (current is null)
        {
            throw new InvalidOperationException($"Body {body.Name} has no buildable features.");
        }

        return new CadDisplayBody
        {
            Body = body,
            Solid = current with { Name = body.Name }
        };
    }
}

public sealed class CadProjectSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void Save(string path, CadDocument document)
    {
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(path, json);
    }

    public CadDocument Load(string path)
    {
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<CadDocument>(json, JsonOptions);
        return document ?? throw new InvalidOperationException("Project file could not be loaded.");
    }
}

public static class CadDesignTextExporter
{
    public static string Export(CadDocument document)
    {
        var lines = new List<string>
        {
            $"project {Sanitize(document.Name)}",
            $"units {document.Units}",
            $"mode {document.ActiveMode}",
            string.Empty,
            $"scene {Sanitize(document.Scene.Name)} {{"
        };

        foreach (var body in document.Scene.Bodies)
        {
            lines.Add($"  body {Sanitize(body.Name)} {{");

            foreach (var feature in body.Features)
            {
                switch (feature)
                {
                    case BoxFeature box:
                        lines.Add(
                            $"    create {Sanitize(feature.Name)}: box(width={box.Width:0.##}, depth={box.Depth:0.##}, height={box.Height:0.##})");
                        break;
                    case CylinderFeature cylinder:
                        lines.Add(
                            $"    create {Sanitize(feature.Name)}: cylinder(radius={cylinder.Radius:0.##}, height={cylinder.Height:0.##})");
                        break;
                    case SphereFeature sphere:
                        lines.Add(
                            $"    create {Sanitize(feature.Name)}: sphere(radius={sphere.Radius:0.##})");
                        break;
                    case ConeFeature cone:
                        lines.Add(
                            $"    create {Sanitize(feature.Name)}: cone(radius={cone.Radius:0.##}, height={cone.Height:0.##})");
                        break;
                    case TorusFeature torus:
                        lines.Add(
                            $"    create {Sanitize(feature.Name)}: torus(major={torus.MajorRadius:0.##}, minor={torus.MinorRadius:0.##})");
                        break;
                    case PyramidFeature pyramid:
                        lines.Add(
                            $"    create {Sanitize(feature.Name)}: pyramid(width={pyramid.BaseWidth:0.##}, depth={pyramid.BaseDepth:0.##}, height={pyramid.Height:0.##})");
                        break;
                    case MoveFeature move:
                        lines.Add(
                            $"    op {Sanitize(feature.Name)}: move(x={move.X:0.##}, y={move.Y:0.##}, z={move.Z:0.##})");
                        break;
                    case FutureFeaturePlaceholder future:
                        lines.Add($"    future {Sanitize(feature.Name)}: {future.PlaceholderKind}");
                        break;
                    default:
                        lines.Add($"    feature {Sanitize(feature.Name)}: {feature.FeatureType}");
                        break;
                }
            }

            lines.Add("  }");
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Sanitize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unnamed" : value.Trim().Replace(' ', '_');
    }
}
