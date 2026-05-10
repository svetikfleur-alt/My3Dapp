using FormaCore.Core;
using System.Globalization;
using System.Text;

namespace FormaCore.Export;

public static class STLExporter
{
    public static void Export(Mesh mesh, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("solid model");

        foreach (var tri in mesh.Triangles)
        {
            var v1 = mesh.Vertices[tri.A];
            var v2 = mesh.Vertices[tri.B];
            var v3 = mesh.Vertices[tri.C];
            var normal = CalculateNormal(v1, v2, v3);

            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"facet normal {normal.X} {normal.Y} {normal.Z}"));
            sb.AppendLine(" outer loop");
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  vertex {v1.X} {v1.Y} {v1.Z}"));
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  vertex {v2.X} {v2.Y} {v2.Z}"));
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  vertex {v3.X} {v3.Y} {v3.Z}"));
            sb.AppendLine(" endloop");
            sb.AppendLine("endfacet");
        }

        sb.AppendLine("endsolid model");

        File.WriteAllText(path, sb.ToString());
    }

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
