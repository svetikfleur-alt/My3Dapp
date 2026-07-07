#if STUDIO_UI_AVALONIA
using System.Numerics;
using FormaCore.Core;
using PkLibrary = PicoGK.Library;
using PkMesh = PicoGK.Mesh;
using PkVoxels = PicoGK.Voxels;

namespace FormaCore.Engine;

public sealed class PicoGkExperimentalGeometryKernelAdapter : IGeometryKernelAdapter
{
    private readonly float _voxelSizeMm;

    private PicoGkExperimentalGeometryKernelAdapter(float voxelSizeMm)
    {
        if (voxelSizeMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(voxelSizeMm), "Voxel size must be positive.");
        }

        _voxelSizeMm = voxelSizeMm;
    }

    public static void RunInSession(
        float voxelSizeMm,
        Action<PicoGkExperimentalGeometryKernelAdapter> action,
        string? logFolder = null,
        string? logFileName = null)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        Exception? failure = null;
        PkLibrary.Go(
            voxelSizeMm,
            () =>
            {
                try
                {
                    var adapter = new PicoGkExperimentalGeometryKernelAdapter(voxelSizeMm);
                    action(adapter);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    PkLibrary.EndTask();
                }
            },
            Path.Combine(logFolder ?? Path.GetTempPath(), logFileName ?? "PicoGKBringup.log"),
            true,
            "PicoGK Bring-up",
            string.Empty);

        if (failure is not null)
        {
            throw new InvalidOperationException("PicoGK session failed during the experimental bring-up run.", failure);
        }
    }

    public string KernelId => "picogk-experimental";
    public string DisplayName => "PicoGK experimental adapter";
    public bool IsExperimental => true;

    public GeometryKernelCapabilities Capabilities { get; } = new(
        SupportsBox: true,
        SupportsCylinder: true,
        SupportsBooleanUnion: true,
        SupportsBooleanSubtract: true,
        SupportsBooleanIntersect: true,
        SupportsMeshExport: true,
        Notes: "Experimental voxel-backed adapter using the PicoGK NuGet package already referenced by the Avalonia host.");

    public GeometryKernelBody CreateBox(double width, double depth, double height)
    {
        var formaMesh = MeshBuilder.CreateBox(width, depth, height);
        var mesh = ConvertMesh(formaMesh);
        return Wrap(new PkVoxels(mesh), $"PicoGK Box({width}, {depth}, {height})");
    }

    public GeometryKernelBody CreateCylinder(double radius, double height)
    {
        var formaMesh = MeshBuilder.CreateCylinder(radius, height);
        var mesh = ConvertMesh(formaMesh);
        return Wrap(new PkVoxels(mesh), $"PicoGK Cylinder(r={radius}, h={height})");
    }

    public GeometryKernelBody CreateFromMesh(Mesh mesh)
    {
        var pkMesh = ConvertMesh(mesh);
        return Wrap(new PkVoxels(pkMesh), "PicoGK Voxelized Mesh");
    }

    public GeometryKernelBody Translate(GeometryKernelBody body, double x, double y, double z)
    {
        var source = Unwrap(body);
        var mesh = source.mshAsMesh().mshCreateTransformed(Matrix4x4.CreateTranslation((float)x, (float)y, (float)z));
        return Wrap(new PkVoxels(mesh), $"{body.DebugName} translated by ({x}, {y}, {z})");
    }

    public GeometryKernelBody Union(GeometryKernelBody a, GeometryKernelBody b)
        => Wrap(Unwrap(a).voxBoolAdd(Unwrap(b)), $"PicoGK Union({a.DebugName}, {b.DebugName})");

    public GeometryKernelBody Subtract(GeometryKernelBody a, GeometryKernelBody b)
        => Wrap(Unwrap(a).voxBoolSubtract(Unwrap(b)), $"PicoGK Subtract({a.DebugName}, {b.DebugName})");

    public GeometryKernelBody Intersect(GeometryKernelBody a, GeometryKernelBody b)
        => Wrap(Unwrap(a).voxBoolIntersect(Unwrap(b)), $"PicoGK Intersect({a.DebugName}, {b.DebugName})");

    public Mesh Tessellate(GeometryKernelBody body)
        => ConvertMesh(Unwrap(body).mshAsMesh());

    public void ExportStl(GeometryKernelBody body, string path)
        => Unwrap(body).mshAsMesh().SaveToStlFile(path);

    private static Mesh ConvertMesh(PkMesh pkMesh)
    {
        var mesh = new Mesh();
        for (var i = 0; i < pkMesh.nTriangleCount(); i++)
        {
            pkMesh.GetTriangle(i, out var a, out var b, out var c);
            var baseIndex = mesh.Vertices.Count;
            mesh.Vertices.Add(new Vertex(a.X, a.Y, a.Z));
            mesh.Vertices.Add(new Vertex(b.X, b.Y, b.Z));
            mesh.Vertices.Add(new Vertex(c.X, c.Y, c.Z));
            mesh.Triangles.Add(new Triangle(baseIndex, baseIndex + 1, baseIndex + 2));
        }

        return mesh;
    }

    private static PkMesh ConvertMesh(Mesh formaMesh)
    {
        var mesh = new PkMesh();
        foreach (var triangle in formaMesh.Triangles)
        {
            var a = formaMesh.Vertices[triangle.A];
            var b = formaMesh.Vertices[triangle.B];
            var c = formaMesh.Vertices[triangle.C];
            mesh.nAddTriangle(
                new Vector3((float)a.X, (float)a.Y, (float)a.Z),
                new Vector3((float)b.X, (float)b.Y, (float)b.Z),
                new Vector3((float)c.X, (float)c.Y, (float)c.Z));
        }

        return mesh;
    }
    private GeometryKernelBody Wrap(PkVoxels voxels, string debugName)
        => new(KernelId, voxels, debugName);

    private static PkVoxels Unwrap(GeometryKernelBody body)
    {
        if (body.KernelId != "picogk-experimental" || body.NativeHandle is not PkVoxels voxels)
        {
            throw new InvalidOperationException($"Body '{body.DebugName}' does not belong to the PicoGK experimental adapter.");
        }

        return voxels;
    }
}
#endif
