using FormaCore.Core;

namespace FormaCore.Engine;

/// <summary>Derived mesh properties computed via the divergence theorem.</summary>
public sealed record MeshProperties(
    double Volume,          // mm³
    double SurfaceArea,     // mm²
    double BBoxWidth,       // mm (X span)
    double BBoxDepth,       // mm (Y span)
    double BBoxHeight,      // mm (Z span)
    int TriangleCount,
    int VertexCount)
{
    public static readonly MeshProperties Empty =
        new(0, 0, 0, 0, 0, 0, 0);

    public double VolumeCm3 => Volume / 1000.0;
    public double SurfaceAreaCm2 => SurfaceArea / 100.0;
}

public static class MeshAnalyzer
{
    public static MeshProperties Analyze(Mesh mesh)
    {
        if (mesh.Triangles.Count == 0 || mesh.Vertices.Count == 0)
            return MeshProperties.Empty;

        double signedVolume = 0;
        double surfaceArea = 0;
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

        foreach (var v in mesh.Vertices)
        {
            if (v.X < minX) minX = v.X;
            if (v.Y < minY) minY = v.Y;
            if (v.Z < minZ) minZ = v.Z;
            if (v.X > maxX) maxX = v.X;
            if (v.Y > maxY) maxY = v.Y;
            if (v.Z > maxZ) maxZ = v.Z;
        }

        foreach (var tri in mesh.Triangles)
        {
            var a = mesh.Vertices[tri.A];
            var b = mesh.Vertices[tri.B];
            var c = mesh.Vertices[tri.C];

            // Signed volume of tetrahedron from origin (divergence theorem)
            signedVolume += a.X * (b.Y * c.Z - c.Y * b.Z)
                          + b.X * (c.Y * a.Z - a.Y * c.Z)
                          + c.X * (a.Y * b.Z - b.Y * a.Z);

            // Face area via cross product
            double ex1 = b.X - a.X, ey1 = b.Y - a.Y, ez1 = b.Z - a.Z;
            double ex2 = c.X - a.X, ey2 = c.Y - a.Y, ez2 = c.Z - a.Z;
            double nx = ey1 * ez2 - ez1 * ey2;
            double ny = ez1 * ex2 - ex1 * ez2;
            double nz = ex1 * ey2 - ey1 * ex2;
            surfaceArea += Math.Sqrt(nx * nx + ny * ny + nz * nz);
        }

        return new MeshProperties(
            Math.Abs(signedVolume) / 6.0,
            surfaceArea * 0.5,
            Math.Max(0, maxX - minX),
            Math.Max(0, maxY - minY),
            Math.Max(0, maxZ - minZ),
            mesh.Triangles.Count,
            mesh.Vertices.Count);
    }
}
