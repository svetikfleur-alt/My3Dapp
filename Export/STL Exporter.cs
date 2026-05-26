using FormaCore.Core;
using System.Globalization;
using System.Text;

namespace FormaCore.Export;

public static class STLExporter
{
    /// <summary>
    /// Export a mesh as binary STL (default) — 10-50x smaller than ASCII, preferred by all slicers.
    /// </summary>
    public static void Export(Mesh mesh, string path, bool binary = true)
    {
        var clean = RemoveDegenerateTriangles(mesh);
        if (binary)
            ExportBinary(clean, path);
        else
            ExportAscii(clean, path);
    }

    /// <summary>
    /// Export a mesh as binary STL to a stream. Useful for in-memory operations.
    /// </summary>
    public static void ExportToStream(Mesh mesh, Stream stream, bool binary = true)
    {
        var clean = RemoveDegenerateTriangles(mesh);
        if (binary)
            WriteBinary(clean, stream);
        else
            WriteAscii(clean, stream);
    }

    // ── Binary STL ──────────────────────────────────────────────────────
    // Format:
    //   80 bytes: header (arbitrary)
    //   4  bytes: uint32 triangle count
    //   per triangle:
    //     12 bytes: normal (3× float32)
    //     36 bytes: vertices (3× 3× float32)
    //      2 bytes: attribute byte count (0)
    //   Total per triangle: 50 bytes

    private static void ExportBinary(Mesh mesh, string path)
    {
        using var stream = File.Create(path);
        WriteBinary(mesh, stream);
    }

    private static void WriteBinary(Mesh mesh, Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        // 80-byte header
        var header = new byte[80];
        var headerText = Encoding.ASCII.GetBytes("My3DApp STL Export");
        Array.Copy(headerText, header, Math.Min(headerText.Length, 80));
        writer.Write(header);

        // Triangle count
        writer.Write((uint)mesh.Triangles.Count);

        // Triangles
        foreach (var tri in mesh.Triangles)
        {
            var v1 = mesh.Vertices[tri.A];
            var v2 = mesh.Vertices[tri.B];
            var v3 = mesh.Vertices[tri.C];
            var normal = CalculateNormal(v1, v2, v3);

            writer.Write((float)normal.X);
            writer.Write((float)normal.Y);
            writer.Write((float)normal.Z);

            writer.Write((float)v1.X);
            writer.Write((float)v1.Y);
            writer.Write((float)v1.Z);

            writer.Write((float)v2.X);
            writer.Write((float)v2.Y);
            writer.Write((float)v2.Z);

            writer.Write((float)v3.X);
            writer.Write((float)v3.Y);
            writer.Write((float)v3.Z);

            writer.Write((ushort)0); // attribute byte count
        }
    }

    // ── ASCII STL ───────────────────────────────────────────────────────

    private static void ExportAscii(Mesh mesh, string path)
    {
        using var stream = File.Create(path);
        WriteAscii(mesh, stream);
    }

    private static void WriteAscii(Mesh mesh, Stream stream)
    {
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.WriteLine("solid model");

        foreach (var tri in mesh.Triangles)
        {
            var v1 = mesh.Vertices[tri.A];
            var v2 = mesh.Vertices[tri.B];
            var v3 = mesh.Vertices[tri.C];
            var normal = CalculateNormal(v1, v2, v3);

            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"facet normal {normal.X} {normal.Y} {normal.Z}"));
            writer.WriteLine(" outer loop");
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  vertex {v1.X} {v1.Y} {v1.Z}"));
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  vertex {v2.X} {v2.Y} {v2.Z}"));
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  vertex {v3.X} {v3.Y} {v3.Z}"));
            writer.WriteLine(" endloop");
            writer.WriteLine("endfacet");
        }

        writer.WriteLine("endsolid model");
    }

    // ── Shared helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Remove degenerate triangles (zero-area, out-of-range indices, NaN vertices).
    /// Returns a new mesh that is safe for export.
    /// </summary>
    private static Mesh RemoveDegenerateTriangles(Mesh mesh)
    {
        if (mesh.Triangles.Count == 0)
            return mesh;

        var clean = new Mesh();
        clean.Vertices.AddRange(mesh.Vertices);

        foreach (var tri in mesh.Triangles)
        {
            if (tri.A < 0 || tri.A >= mesh.Vertices.Count ||
                tri.B < 0 || tri.B >= mesh.Vertices.Count ||
                tri.C < 0 || tri.C >= mesh.Vertices.Count)
                continue;

            if (tri.A == tri.B || tri.B == tri.C || tri.A == tri.C)
                continue;

            var v1 = mesh.Vertices[tri.A];
            var v2 = mesh.Vertices[tri.B];
            var v3 = mesh.Vertices[tri.C];

            // Skip NaN/Infinity vertices
            if (!IsFiniteVertex(v1) || !IsFiniteVertex(v2) || !IsFiniteVertex(v3))
                continue;

            // Skip zero-area triangles
            var normal = CalculateNormal(v1, v2, v3);
            if (normal.X == 0 && normal.Y == 0 && normal.Z == 0)
                continue;

            clean.Triangles.Add(tri);
        }

        return clean;
    }

    private static bool IsFiniteVertex(Vertex v) =>
        double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);

    private static Vertex CalculateNormal(Vertex v1, Vertex v2, Vertex v3)
    {
        var ux = v2.X - v1.X;
        var uy = v2.Y - v1.Y;
        var uz = v2.Z - v1.Z;

        var vx = v3.X - v1.X;
        var vy = v3.Y - v1.Y;
        var vz = v3.Z - v1.Z;

        var nx = uy * vz - uz * vy;
        var ny = uz * vx - ux * vz;
        var nz = ux * vy - uy * vx;

        var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (length == 0)
        {
            return new Vertex(0, 0, 0);
        }

        return new Vertex(nx / length, ny / length, nz / length);
    }
}
