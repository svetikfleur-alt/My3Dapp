using FormaCore.Core;
using FormaCore.Export;

namespace FormaCore.Engine;

public sealed class CurrentGeometryKernelAdapter : IGeometryKernelAdapter
{
    private readonly SolidMesher _mesher = new();

    public string KernelId => "current-solid-mesher";
    public string DisplayName => "Current SolidMesher adapter";
    public bool IsExperimental => false;

    public GeometryKernelCapabilities Capabilities { get; } = new(
        SupportsBox: true,
        SupportsCylinder: true,
        SupportsBooleanUnion: true,
        SupportsBooleanSubtract: true,
        SupportsBooleanIntersect: true,
        SupportsMeshExport: true,
        Notes: "Wraps the existing Solid -> Mesh path used by the current app.");

    public GeometryKernelBody CreateBox(double width, double depth, double height)
        => new(KernelId, new BoxSolid(width, depth, height), $"Box({width}, {depth}, {height})");

    public GeometryKernelBody CreateCylinder(double radius, double height)
        => new(KernelId, new CylinderSolid(radius, height), $"Cylinder(r={radius}, h={height})");

    public GeometryKernelBody Translate(GeometryKernelBody body, double x, double y, double z)
        => new(
            KernelId,
            new TransformedSolid(Unwrap(body), new Transform3D(new Vector3D(x, y, z))),
            $"{body.DebugName} translated by ({x}, {y}, {z})");

    public GeometryKernelBody Union(GeometryKernelBody a, GeometryKernelBody b)
        => new(KernelId, new BooleanSolid(BooleanOperation.Union, Unwrap(a), Unwrap(b)), $"Union({a.DebugName}, {b.DebugName})");

    public GeometryKernelBody Subtract(GeometryKernelBody a, GeometryKernelBody b)
        => new(KernelId, new BooleanSolid(BooleanOperation.Subtract, Unwrap(a), Unwrap(b)), $"Subtract({a.DebugName}, {b.DebugName})");

    public GeometryKernelBody Intersect(GeometryKernelBody a, GeometryKernelBody b)
        => new(KernelId, new BooleanSolid(BooleanOperation.Intersect, Unwrap(a), Unwrap(b)), $"Intersect({a.DebugName}, {b.DebugName})");

    public Mesh Tessellate(GeometryKernelBody body)
        => _mesher.Tessellate(Unwrap(body));

    public void ExportStl(GeometryKernelBody body, string path)
        => STLExporter.Export(Tessellate(body), path);

    private static Solid Unwrap(GeometryKernelBody body)
    {
        if (body.KernelId != "current-solid-mesher" || body.NativeHandle is not Solid solid)
        {
            throw new InvalidOperationException($"Body '{body.DebugName}' does not belong to the current geometry kernel adapter.");
        }

        return solid;
    }
}
