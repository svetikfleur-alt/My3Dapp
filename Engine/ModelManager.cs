using System.IO;
using My3DApp.Core;

namespace My3DApp.Engine;

public class ModelManager
{
    private readonly SceneGraph _scene;

    public ModelManager(SceneGraph scene) => _scene = scene;

    // ── Primitive creation ────────────────────────────────────────────────────

    public SceneObject CreateBox(string name, float w, float h, float d)
    {
        var obj = _scene.Add(name, "box");
        obj.Geometry = Mesh.Box(w, h, d);
        RuntimeLog.Info("Model", $"Created box '{name}' [{w}×{h}×{d}] ({obj.Geometry.Triangles.Count} tris)");
        return obj;
    }

    public SceneObject CreateCylinder(string name, float radius, float height, int segments = 32)
    {
        var obj = _scene.Add(name, "cylinder");
        obj.Geometry = Mesh.Cylinder(radius, height, segments);
        RuntimeLog.Info("Model", $"Created cylinder '{name}' r={radius} h={height}");
        return obj;
    }

    public SceneObject CreateSphere(string name, float radius, int segments = 32)
    {
        var obj = _scene.Add(name, "sphere");
        obj.Geometry = Mesh.Sphere(radius, segments, segments);
        RuntimeLog.Info("Model", $"Created sphere '{name}' r={radius}");
        return obj;
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<SceneObject?> ImportAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            RuntimeLog.Warn("Model", $"Import file not found: {filePath}");
            return null;
        }

        var ext  = Path.GetExtension(filePath).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(filePath);

        var obj = _scene.Add(name, ext switch
        {
            ".stl"            => "stl",
            ".obj"            => "obj",
            ".step" or ".stp" => "step",
            ".3mf"            => "3mf",
            _                 => "mesh"
        });
        obj.SourcePath = filePath;

        // Parse STL into engine mesh so export can round-trip without re-reading the file
        if (ext == ".stl")
        {
            await using var fs = File.OpenRead(filePath);
            if (IsAsciiStl(fs))
            {
                // ASCII STL: viewport loads it fine via loadSTL, engine mesh left null
                RuntimeLog.Info("Model", $"Imported ASCII STL '{name}' — engine geometry not parsed (ASCII format)");
            }
            else
            {
                fs.Seek(0, SeekOrigin.Begin);
                obj.Geometry = Mesh.ReadBinaryStl(fs);
                RuntimeLog.Info("Model", $"Imported binary STL '{name}' — {obj.Geometry.Triangles.Count} triangles");
            }
        }
        else
        {
            RuntimeLog.Info("Model", $"Imported '{name}' from {filePath} (geometry not parsed for {ext})");
        }

        return obj;
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public async Task ExportAsync(Guid objectId, string outputPath)
    {
        var obj = _scene.Find(objectId);
        if (obj is null)
        {
            RuntimeLog.Warn("Model", $"Export: object {objectId} not found.");
            return;
        }

        var ext = Path.GetExtension(outputPath).ToLowerInvariant();

        switch (ext)
        {
            case ".stl":
                await ExportStlAsync(obj, outputPath);
                break;

            case ".obj":
                await ExportObjAsync(obj, outputPath);
                break;

            default:
                // Fallback: if we have a source file of the same type, just copy it
                if (obj.SourcePath != null && File.Exists(obj.SourcePath))
                {
                    File.Copy(obj.SourcePath, outputPath, overwrite: true);
                    RuntimeLog.Info("Model", $"Copied source file to {outputPath}");
                }
                else
                {
                    RuntimeLog.Warn("Model", $"No export handler for '{ext}' and no source file available.");
                }
                break;
        }
    }

    private static async Task ExportStlAsync(SceneObject obj, string path)
    {
        if (obj.Geometry is null)
        {
            // No computed geometry — write a placeholder ASCII STL
            var ascii = $"solid {obj.Name}\nendsolid {obj.Name}\n";
            await File.WriteAllTextAsync(path, ascii);
            RuntimeLog.Warn("Model", $"Exported empty STL for '{obj.Name}' (no geometry computed).");
            return;
        }

        await using var fs = File.Create(path);
        obj.Geometry.WriteBinaryStl(fs);
        RuntimeLog.Info("Model", $"Exported binary STL '{obj.Name}' ({obj.Geometry.Triangles.Count} tris) → {path}");
    }

    private static async Task ExportObjAsync(SceneObject obj, string path)
    {
        if (obj.Geometry is null)
        {
            await File.WriteAllTextAsync(path, $"# {obj.Name}\n");
            RuntimeLog.Warn("Model", $"Exported empty OBJ for '{obj.Name}' (no geometry).");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Exported by My3DApp — {obj.Name}");
        sb.AppendLine($"o {obj.Name}");

        int vIdx = 1;
        foreach (var t in obj.Geometry.Triangles)
        {
            sb.AppendLine(Vert(t.V0));
            sb.AppendLine(Vert(t.V1));
            sb.AppendLine(Vert(t.V2));
            sb.AppendLine($"f {vIdx} {vIdx+1} {vIdx+2}");
            vIdx += 3;
        }

        await File.WriteAllTextAsync(path, sb.ToString());
        RuntimeLog.Info("Model", $"Exported OBJ '{obj.Name}' ({obj.Geometry.Triangles.Count} tris) → {path}");

        static string Vert(System.Numerics.Vector3 v) =>
            $"v {v.X:F6} {v.Y:F6} {v.Z:F6}";
    }

    // Returns true if the stream contains an ASCII STL file (starts with "solid ").
    // A binary STL can also start with "solid" by chance, so we cross-check with the
    // file size: binary size = 84 + triangleCount*50 bytes.
    private static bool IsAsciiStl(Stream stream)
    {
        var header = new byte[80];
        var read = stream.Read(header, 0, header.Length);
        if (read < 5) return false;

        var prefix = System.Text.Encoding.ASCII.GetString(header, 0, Math.Min(6, read));
        if (!prefix.StartsWith("solid", StringComparison.OrdinalIgnoreCase)) return false;

        // Cross-check with binary size formula to avoid false positives
        if (read >= 80 && stream.CanSeek)
        {
            stream.Seek(80, SeekOrigin.Begin);
            var countBuf = new byte[4];
            if (stream.Read(countBuf, 0, 4) == 4)
            {
                var triCount = BitConverter.ToUInt32(countBuf, 0);
                var expectedSize = 84L + triCount * 50L;
                if (stream.Length == expectedSize) return false; // it's binary
            }
        }

        return true;
    }
}
