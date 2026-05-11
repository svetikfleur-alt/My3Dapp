using System.IO;
using My3DApp.Core;

namespace My3DApp.Engine;

public class ModelManager
{
    private readonly SceneGraph _scene;

    public ModelManager(SceneGraph scene) => _scene = scene;

    // ── Primitive creation ────────────────────────────────────────────────────

    public SceneObject CreateBox(string name, float w, float h, float d,
                                 float x = 0, float y = 0, float z = 0)
    {
        var obj = _scene.Add(name, "box");
        obj.Solid = new BoxParams(w, h, d, x, y, z);
        RuntimeLog.Info("Model", $"Created box '{name}' [{w}×{h}×{d} mm] @ ({x},{y},{z})");
        return obj;
    }

    public SceneObject CreateCylinder(string name, float radius, float height,
                                      int segments = 32, float x = 0, float y = 0, float z = 0)
    {
        var obj = _scene.Add(name, "cylinder");
        obj.Solid = new CylinderParams(radius, height, segments, x, y, z);
        RuntimeLog.Info("Model", $"Created cylinder '{name}' r={radius} h={height} mm @ ({x},{y},{z})");
        return obj;
    }

    public SceneObject CreateSphere(string name, float radius,
                                    int segments = 32, float x = 0, float y = 0, float z = 0)
    {
        var obj = _scene.Add(name, "sphere");
        obj.Solid = new SphereParams(radius, segments, x, y, z);
        RuntimeLog.Info("Model", $"Created sphere '{name}' r={radius} mm @ ({x},{y},{z})");
        return obj;
    }

    public SceneObject CreateRevolveProfile(string name, float[] profile,
        float angleDeg = 360f, int segments = 32, float x = 0, float y = 0, float z = 0)
    {
        var obj = _scene.Add(name, "revolve");
        obj.Solid = new RevolveProfileParams(profile, angleDeg, segments, x, y, z);
        RuntimeLog.Info("Model", $"Created revolve '{name}' {angleDeg}° @ ({x},{y},{z})");
        return obj;
    }

    public SceneObject CreateExtrudePolygon(string name, float[] points2D, float depth,
        float x = 0, float y = 0, float z = 0)
    {
        var obj = _scene.Add(name, "extrude");
        obj.Solid = new ExtrudePolygonParams(points2D, depth, x, y, z);
        RuntimeLog.Info("Model", $"Created extrude '{name}' depth={depth}mm @ ({x},{y},{z})");
        return obj;
    }

    public SceneObject CreateBooleanUnion(string name, SolidParams a, SolidParams b)
    {
        var obj = _scene.Add(name, "boolean-union");
        obj.Solid = new BooleanUnionParams(a, b);
        RuntimeLog.Info("Model", $"Created union '{name}'");
        return obj;
    }

    public SceneObject CreateBooleanSubtract(string name, SolidParams target, SolidParams tool)
    {
        var obj = _scene.Add(name, "boolean-subtract");
        obj.Solid = new BooleanSubtractParams(target, tool);
        RuntimeLog.Info("Model", $"Created subtract '{name}'");
        return obj;
    }

    public SceneObject CreateBooleanIntersect(string name, SolidParams a, SolidParams b)
    {
        var obj = _scene.Add(name, "boolean-intersect");
        obj.Solid = new BooleanIntersectParams(a, b);
        RuntimeLog.Info("Model", $"Created intersect '{name}'");
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
                RuntimeLog.Info("Model", $"Imported ASCII STL '{name}' — parametric solid not available for ASCII format");
            }
            else
            {
                fs.Seek(0, SeekOrigin.Begin);
                var mesh = Mesh.ReadBinaryStl(fs);
                obj.Solid = new ImportedMeshParams(mesh);
                RuntimeLog.Info("Model", $"Imported binary STL '{name}' — {mesh.Triangles.Count} triangles");
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
        if (obj.Solid is null)
        {
            var ascii = $"solid {obj.Name}\nendsolid {obj.Name}\n";
            await File.WriteAllTextAsync(path, ascii);
            RuntimeLog.Warn("Model", $"Exported empty STL for '{obj.Name}' — no solid definition.");
            return;
        }

        var mesh = obj.Solid.ToMesh();
        await using var fs = File.Create(path);
        mesh.WriteBinaryStl(fs);
        RuntimeLog.Info("Model", $"Exported STL '{obj.Name}' ({mesh.Triangles.Count} tris) → {path}");
    }

    private static async Task ExportObjAsync(SceneObject obj, string path)
    {
        if (obj.Solid is null)
        {
            await File.WriteAllTextAsync(path, $"# {obj.Name}\n");
            RuntimeLog.Warn("Model", $"Exported empty OBJ for '{obj.Name}' — no solid definition.");
            return;
        }

        var mesh = obj.Solid.ToMesh();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Exported by My3DApp — {obj.Name}");
        sb.AppendLine($"o {obj.Name}");

        int vIdx = 1;
        foreach (var t in mesh.Triangles)
        {
            sb.AppendLine(Vert(t.V0));
            sb.AppendLine(Vert(t.V1));
            sb.AppendLine(Vert(t.V2));
            sb.AppendLine($"f {vIdx} {vIdx+1} {vIdx+2}");
            vIdx += 3;
        }

        await File.WriteAllTextAsync(path, sb.ToString());
        RuntimeLog.Info("Model", $"Exported OBJ '{obj.Name}' ({mesh.Triangles.Count} tris) → {path}");

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
