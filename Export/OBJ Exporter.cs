using FormaCore.Core;
using System.Globalization;
using System.Text;

namespace FormaCore.Export;

public static class OBJExporter
{
    /// <summary>
    /// Export a mesh as Wavefront OBJ with per-face vertex normals.
    /// Produces a valid file that imports cleanly into Blender, Fusion 360, etc.
    /// </summary>
    public static void Export(Mesh mesh, string path)
    {
        var clean = RemoveDegenerateTriangles(mesh);
        var sb = new StringBuilder();

        sb.AppendLine("# My3DApp OBJ Export");
        sb.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"# Vertices: {clean.Vertices.Count}  Triangles: {clean.Triangles.Count}"));
        sb.AppendLine();

        // Vertices
        foreach (var v in clean.Vertices)
        {
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"v {v.X:G9} {v.Y:G9} {v.Z:G9}"));
        }

        sb.AppendLine();

        // Compute and write per-vertex normals (averaged from adjacent faces)
        var normals = ComputeVertexNormals(clean);
        foreach (var n in normals)
        {
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"vn {n.X:G9} {n.Y:G9} {n.Z:G9}"));
        }

        sb.AppendLine();
        sb.AppendLine("g model");

        // Faces with vertex normals (f v//vn v//vn v//vn)
        foreach (var t in clean.Triangles)
        {
            var a = t.A + 1;
            var b = t.B + 1;
            var c = t.C + 1;
            sb.AppendLine($"f {a}//{a} {b}//{b} {c}//{c}");
        }

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>
    /// Export a mesh as OBJ with an accompanying MTL file for body color.
    /// </summary>
    public static void ExportWithMaterial(Mesh mesh, string path, string? hexColor = null)
    {
        var mtlPath = Path.ChangeExtension(path, ".mtl");
        var mtlName = Path.GetFileNameWithoutExtension(path);

        WriteMtl(mtlPath, mtlName, hexColor ?? "#808080");
        ExportWithMtlReference(mesh, path, Path.GetFileName(mtlPath), mtlName);
    }

    private static void ExportWithMtlReference(Mesh mesh, string path, string mtlFileName, string materialName)
    {
        var clean = RemoveDegenerateTriangles(mesh);
        var sb = new StringBuilder();

        sb.AppendLine("# My3DApp OBJ Export");
        sb.AppendLine($"mtllib {mtlFileName}");
        sb.AppendLine();

        foreach (var v in clean.Vertices)
        {
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"v {v.X:G9} {v.Y:G9} {v.Z:G9}"));
        }

        sb.AppendLine();

        var normals = ComputeVertexNormals(clean);
        foreach (var n in normals)
        {
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"vn {n.X:G9} {n.Y:G9} {n.Z:G9}"));
        }

        sb.AppendLine();
        sb.AppendLine($"usemtl {materialName}");

        foreach (var t in clean.Triangles)
        {
            var a = t.A + 1;
            var b = t.B + 1;
            var c = t.C + 1;
            sb.AppendLine($"f {a}//{a} {b}//{b} {c}//{c}");
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteMtl(string path, string materialName, string hexColor)
    {
        var (r, g, b) = ParseHexColor(hexColor);
        var sb = new StringBuilder();
        sb.AppendLine("# My3DApp MTL");
        sb.AppendLine($"newmtl {materialName}");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Kd {r:F4} {g:F4} {b:F4}"));
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Ka {r * 0.2:F4} {g * 0.2:F4} {b * 0.2:F4}"));
        sb.AppendLine("Ks 0.3000 0.3000 0.3000");
        sb.AppendLine("Ns 100.0000");
        sb.AppendLine("d 1.0000");
        sb.AppendLine("illum 2");
        File.WriteAllText(path, sb.ToString());
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Vertex[] ComputeVertexNormals(Mesh mesh)
    {
        var normals = new double[mesh.Vertices.Count * 3];

        foreach (var tri in mesh.Triangles)
        {
            var v0 = mesh.Vertices[tri.A];
            var v1 = mesh.Vertices[tri.B];
            var v2 = mesh.Vertices[tri.C];

            double e1x = v1.X - v0.X, e1y = v1.Y - v0.Y, e1z = v1.Z - v0.Z;
            double e2x = v2.X - v0.X, e2y = v2.Y - v0.Y, e2z = v2.Z - v0.Z;

            var nx = e1y * e2z - e1z * e2y;
            var ny = e1z * e2x - e1x * e2z;
            var nz = e1x * e2y - e1y * e2x;

            normals[tri.A * 3] += nx; normals[tri.A * 3 + 1] += ny; normals[tri.A * 3 + 2] += nz;
            normals[tri.B * 3] += nx; normals[tri.B * 3 + 1] += ny; normals[tri.B * 3 + 2] += nz;
            normals[tri.C * 3] += nx; normals[tri.C * 3 + 1] += ny; normals[tri.C * 3 + 2] += nz;
        }

        var result = new Vertex[mesh.Vertices.Count];
        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var nx = normals[i * 3];
            var ny = normals[i * 3 + 1];
            var nz = normals[i * 3 + 2];
            var len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len > 1e-12)
            {
                result[i] = new Vertex(nx / len, ny / len, nz / len);
            }
            else
            {
                result[i] = new Vertex(0, 1, 0);
            }
        }

        return result;
    }

    private static Mesh RemoveDegenerateTriangles(Mesh mesh)
    {
        if (mesh.Triangles.Count == 0) return mesh;

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
            var v0 = mesh.Vertices[tri.A];
            var v1 = mesh.Vertices[tri.B];
            var v2 = mesh.Vertices[tri.C];
            if (!IsFinite(v0) || !IsFinite(v1) || !IsFinite(v2))
                continue;
            clean.Triangles.Add(tri);
        }

        return clean;
    }

    private static bool IsFinite(Vertex v) =>
        double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);

    private static (double R, double G, double B) ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length < 6) return (0.5, 0.5, 0.5);
        var r = int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var g = int.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var b = int.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return (r, g, b);
    }
}
