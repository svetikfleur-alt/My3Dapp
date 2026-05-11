using FormaCore.Core;
using System.Globalization;
using System.Text;

namespace FormaCore.Export;

public static class OBJExporter
{
    public static void Export(Mesh mesh, string path)
    {
        var sb = new StringBuilder();

        foreach (var v in mesh.Vertices)
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"v {v.X} {v.Y} {v.Z}"));

        foreach (var t in mesh.Triangles)
            sb.AppendLine($"f {t.A + 1} {t.B + 1} {t.C + 1}");

        File.WriteAllText(path, sb.ToString());
    }
}
