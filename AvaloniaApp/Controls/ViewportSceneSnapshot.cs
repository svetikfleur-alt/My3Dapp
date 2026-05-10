using System.Text.Json.Serialization;
using FormaCore.Engine;

namespace My3DApp.AvaloniaApp.Controls;

public sealed class ViewportActiveFacePlane
{
    [JsonPropertyName("nx")]
    public double Nx { get; init; }
    [JsonPropertyName("ny")]
    public double Ny { get; init; }
    [JsonPropertyName("nz")]
    public double Nz { get; init; }
    [JsonPropertyName("ox")]
    public double Ox { get; init; }
    [JsonPropertyName("oy")]
    public double Oy { get; init; }
    [JsonPropertyName("oz")]
    public double Oz { get; init; }
    [JsonPropertyName("ux")]
    public double Ux { get; init; }
    [JsonPropertyName("uy")]
    public double Uy { get; init; }
    [JsonPropertyName("uz")]
    public double Uz { get; init; }
    [JsonPropertyName("vx")]
    public double Vx { get; init; }
    [JsonPropertyName("vy")]
    public double Vy { get; init; }
    [JsonPropertyName("vz")]
    public double Vz { get; init; }
}

public sealed class ViewportRenderState
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "DirectPrimitive";

    [JsonPropertyName("activePlaneName")]
    public string ActivePlaneName { get; init; } = "Top";

    [JsonPropertyName("activeFacePlane")]
    public ViewportActiveFacePlane? ActiveFacePlane { get; init; }

    [JsonPropertyName("selectedBodyId")]
    public Guid? SelectedBodyId { get; init; }

    [JsonPropertyName("selectedPlaneId")]
    public Guid? SelectedPlaneId { get; init; }

    [JsonPropertyName("selectedSketchId")]
    public Guid? SelectedSketchId { get; init; }

    [JsonPropertyName("planes")]
    public IReadOnlyList<ViewportRenderPlane> Planes { get; init; } = [];

    [JsonPropertyName("sketches")]
    public IReadOnlyList<ViewportRenderSketch> Sketches { get; init; } = [];

    [JsonPropertyName("bodies")]
    public IReadOnlyList<ViewportRenderBody> Bodies { get; init; } = [];

    [JsonPropertyName("previewBodies")]
    public IReadOnlyList<ViewportRenderBody> PreviewBodies { get; init; } = [];
}

public sealed class ViewportRenderPlane
{
    [JsonPropertyName("planeId")]
    public Guid PlaneId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("visible")]
    public bool Visible { get; init; } = true;
}

public sealed class ViewportRenderBody
{
    [JsonPropertyName("bodyId")]
    public Guid BodyId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }

    [JsonPropertyName("z")]
    public double Z { get; init; }

    [JsonPropertyName("positions")]
    public double[] Positions { get; init; } = [];

    [JsonPropertyName("indices")]
    public int[] Indices { get; init; } = [];

    [JsonPropertyName("featureIds")]
    public Guid[] FeatureIds { get; init; } = [];

    /// <summary>Optional hex color (e.g. "#4080c0") to override the default kind-based palette.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }
}

public sealed class ViewportRenderSketch
{
    [JsonPropertyName("sketchId")]
    public Guid SketchId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("planeName")]
    public string PlaneName { get; init; } = string.Empty;

    [JsonPropertyName("isDraft")]
    public bool IsDraft { get; init; }

    [JsonPropertyName("isPreview")]
    public bool IsPreview { get; init; }

    [JsonPropertyName("isClosed")]
    public bool IsClosed { get; init; }

    [JsonPropertyName("isFullyDefined")]
    public bool IsFullyDefined { get; init; }

    [JsonPropertyName("curves")]
    public IReadOnlyList<ViewportRenderSketchCurve> Curves { get; init; } = [];

    [JsonPropertyName("angleDimensions")]
    public IReadOnlyList<ViewportRenderAngleDimension> AngleDimensions { get; init; } = [];

    [JsonPropertyName("linearDimensions")]
    public IReadOnlyList<ViewportRenderLinearDimension> LinearDimensions { get; init; } = [];

    [JsonPropertyName("radialDimensions")]
    public IReadOnlyList<ViewportRenderRadialDimension> RadialDimensions { get; init; } = [];
}

public sealed class ViewportRenderSketchCurve
{
    [JsonPropertyName("points")]
    public double[] Points { get; init; } = [];

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "polyline";

    [JsonPropertyName("closed")]
    public bool Closed { get; init; }

    [JsonPropertyName("isConstruction")]
    public bool IsConstruction { get; init; }

    [JsonPropertyName("isConstrained")]
    public bool IsConstrained { get; init; }

    [JsonPropertyName("entityId")]
    public Guid EntityId { get; init; }

    [JsonPropertyName("entityType")]
    public string EntityType { get; init; } = string.Empty;
}

public sealed class ViewportRenderAngleDimension
{
    [JsonPropertyName("cx")]
    public double Cx { get; init; }

    [JsonPropertyName("cy")]
    public double Cy { get; init; }

    [JsonPropertyName("cz")]
    public double Cz { get; init; }

    [JsonPropertyName("startDeg")]
    public double StartDeg { get; init; }

    [JsonPropertyName("sweepDeg")]
    public double SweepDeg { get; init; }

    [JsonPropertyName("radius")]
    public double Radius { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;
}

/// <summary>
/// A dimension showing the distance along a line: two 3D world endpoints + perpendicular offset + label.
/// </summary>
public sealed class ViewportRenderLinearDimension
{
    // Line start/end world coordinates (already mapped from sketch plane)
    [JsonPropertyName("p1x")] public double P1X { get; init; }
    [JsonPropertyName("p1y")] public double P1Y { get; init; }
    [JsonPropertyName("p1z")] public double P1Z { get; init; }
    [JsonPropertyName("p2x")] public double P2X { get; init; }
    [JsonPropertyName("p2y")] public double P2Y { get; init; }
    [JsonPropertyName("p2z")] public double P2Z { get; init; }
    // Perpendicular offset direction in world space (unit vector × offset distance)
    [JsonPropertyName("ox")] public double Ox { get; init; }
    [JsonPropertyName("oy")] public double Oy { get; init; }
    [JsonPropertyName("oz")] public double Oz { get; init; }
    [JsonPropertyName("label")] public string Label { get; init; } = string.Empty;
    [JsonPropertyName("isDriven")] public bool IsDriven { get; init; }
}

/// <summary>
/// A radius or diameter dimension for a circle/arc: center in world space + leader angle + value.
/// </summary>
public sealed class ViewportRenderRadialDimension
{
    [JsonPropertyName("cx")] public double Cx { get; init; }
    [JsonPropertyName("cy")] public double Cy { get; init; }
    [JsonPropertyName("cz")] public double Cz { get; init; }
    // Point on the circle edge that the leader line points to (world space)
    [JsonPropertyName("ex")] public double Ex { get; init; }
    [JsonPropertyName("ey")] public double Ey { get; init; }
    [JsonPropertyName("ez")] public double Ez { get; init; }
    [JsonPropertyName("label")] public string Label { get; init; } = string.Empty;
    [JsonPropertyName("isDriven")] public bool IsDriven { get; init; }
    [JsonPropertyName("isDiameter")] public bool IsDiameter { get; init; }
}

public sealed class ViewportSelectionChangedEventArgs : EventArgs
{
    public ViewportSelectionChangedEventArgs(CadEntityKind entityKind, Guid entityId)
    {
        EntityKind = entityKind;
        EntityId = entityId;
    }

    public CadEntityKind EntityKind { get; }

    public Guid EntityId { get; }
}

public sealed class ViewportFaceClickedEventArgs : EventArgs
{
    public ViewportFaceClickedEventArgs(Guid bodyId,
        double nx, double ny, double nz,
        double ox, double oy, double oz,
        double ux, double uy, double uz,
        double vx, double vy, double vz)
    {
        BodyId = bodyId;
        Nx = nx; Ny = ny; Nz = nz;
        Ox = ox; Oy = oy; Oz = oz;
        Ux = ux; Uy = uy; Uz = uz;
        Vx = vx; Vy = vy; Vz = vz;
    }

    public Guid BodyId { get; }
    public double Nx { get; }
    public double Ny { get; }
    public double Nz { get; }
    public double Ox { get; }
    public double Oy { get; }
    public double Oz { get; }
    public double Ux { get; }
    public double Uy { get; }
    public double Uz { get; }
    public double Vx { get; }
    public double Vy { get; }
    public double Vz { get; }
}

public sealed class ViewportBoxSelectEventArgs : EventArgs
{
    public ViewportBoxSelectEventArgs(IReadOnlyList<Guid> ids, bool append)
    {
        Ids = ids;
        Append = append;
    }

    public IReadOnlyList<Guid> Ids { get; }
    public bool Append { get; }
}

public sealed class ViewportBodyTransformEventArgs : EventArgs
{
    public ViewportBodyTransformEventArgs(Guid bodyId, double x, double y, double z)
    {
        BodyId = bodyId;
        X = x;
        Y = y;
        Z = z;
    }

    public Guid BodyId { get; }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }
}

public sealed class ViewportSketchPlacementEventArgs : EventArgs
{
    public ViewportSketchPlacementEventArgs(Guid planeId, string planeKind, double u, double v)
    {
        PlaneId = planeId;
        PlaneKind = planeKind;
        U = u;
        V = v;
    }

    public Guid PlaneId { get; }

    public string PlaneKind { get; }

    public double U { get; }

    public double V { get; }
}

public sealed class ViewportSketchPreviewEventArgs : EventArgs
{
    public ViewportSketchPreviewEventArgs(Guid planeId, string planeKind, double u, double v)
    {
        PlaneId = planeId;
        PlaneKind = planeKind;
        U = u;
        V = v;
    }

    public Guid PlaneId { get; }

    public string PlaneKind { get; }

    public double U { get; }

    public double V { get; }
}

public sealed class ViewportSketchEntityTransformEventArgs : EventArgs
{
    public ViewportSketchEntityTransformEventArgs(Guid sketchId, string planeKind, double du, double dv)
    {
        SketchId = sketchId;
        PlaneKind = planeKind;
        Du = du;
        Dv = dv;
    }

    public Guid SketchId { get; }

    public string PlaneKind { get; }

    public double Du { get; }

    public double Dv { get; }
}

public sealed class ViewportSketchEntityRotationEventArgs : EventArgs
{
    public ViewportSketchEntityRotationEventArgs(Guid sketchId, string planeKind, double pivotU, double pivotV, double angleDeg)
    {
        SketchId = sketchId;
        PlaneKind = planeKind;
        PivotU = pivotU;
        PivotV = pivotV;
        AngleDeg = angleDeg;
    }

    public Guid SketchId { get; }
    public string PlaneKind { get; }
    public double PivotU { get; }
    public double PivotV { get; }
    public double AngleDeg { get; }
}

public sealed class ViewportSketchEntityContextMenuEventArgs : EventArgs
{
    public ViewportSketchEntityContextMenuEventArgs(Guid entityId, string entityType, double screenX, double screenY)
    {
        EntityId = entityId;
        EntityType = entityType;
        ScreenX = screenX;
        ScreenY = screenY;
    }

    public Guid EntityId { get; }

    public string EntityType { get; }

    public double ScreenX { get; }

    public double ScreenY { get; }
}

public sealed class ViewportMeasureResultEventArgs : EventArgs
{
    public ViewportMeasureResultEventArgs(double distance)
    {
        Distance = distance;
    }

    public double Distance { get; }
}
