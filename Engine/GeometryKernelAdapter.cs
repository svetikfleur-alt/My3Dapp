using FormaCore.Core;

namespace FormaCore.Engine;

public sealed record GeometryKernelCapabilities(
    bool SupportsBox,
    bool SupportsCylinder,
    bool SupportsBooleanUnion,
    bool SupportsBooleanSubtract,
    bool SupportsBooleanIntersect,
    bool SupportsMeshExport,
    string Notes);

public sealed record GeometryKernelBody(
    string KernelId,
    object NativeHandle,
    string DebugName);

public interface IGeometryKernelAdapter
{
    string KernelId { get; }
    string DisplayName { get; }
    bool IsExperimental { get; }
    GeometryKernelCapabilities Capabilities { get; }

    GeometryKernelBody CreateBox(double width, double depth, double height);
    GeometryKernelBody CreateCylinder(double radius, double height);
    GeometryKernelBody CreateFromMesh(Mesh mesh);
    GeometryKernelBody Translate(GeometryKernelBody body, double x, double y, double z);
    GeometryKernelBody Union(GeometryKernelBody a, GeometryKernelBody b);
    GeometryKernelBody Subtract(GeometryKernelBody a, GeometryKernelBody b);
    GeometryKernelBody Intersect(GeometryKernelBody a, GeometryKernelBody b);
    Mesh Tessellate(GeometryKernelBody body);
    void ExportStl(GeometryKernelBody body, string path);
}

public static class GeometryKernelAdapterFactory
{
    public static IGeometryKernelAdapter CreateCurrent()
        => new CurrentGeometryKernelAdapter();

#if STUDIO_UI_AVALONIA
    public static IGeometryKernelAdapter CreatePicoGkExperimental(float voxelSizeMm = 0.5f)
        => throw new NotSupportedException(
            "The PicoGK experimental adapter must run inside an explicit PicoGK session. " +
            $"Use {nameof(PicoGkExperimentalGeometryKernelAdapter)}.{nameof(PicoGkExperimentalGeometryKernelAdapter.RunInSession)}(...) instead.");
#endif
}
