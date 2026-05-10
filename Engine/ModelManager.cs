using System.IO;
using My3DApp.Core;

namespace My3DApp.Engine;

public class ModelManager
{
    private readonly SceneGraph _scene;

    public ModelManager(SceneGraph scene)
    {
        _scene = scene;
    }

    public SceneObject CreateBox(string name, float w, float h, float d)
    {
        var obj = _scene.Add(name, "box");
        RuntimeLog.Info("Model", $"Created box {name} [{w}x{h}x{d}]");
        return obj;
    }

    public SceneObject CreateCylinder(string name, float radius, float height, int segments = 32)
    {
        var obj = _scene.Add(name, "cylinder");
        RuntimeLog.Info("Model", $"Created cylinder {name} r={radius} h={height}");
        return obj;
    }

    public SceneObject CreateSphere(string name, float radius, int segments = 32)
    {
        var obj = _scene.Add(name, "sphere");
        RuntimeLog.Info("Model", $"Created sphere {name} r={radius}");
        return obj;
    }

    public async Task<SceneObject?> ImportAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            RuntimeLog.Warn("Model", $"Import file not found: {filePath}");
            return null;
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(filePath);

        var obj = _scene.Add(name, ext switch
        {
            ".stl" => "stl",
            ".obj" => "obj",
            ".step" or ".stp" => "step",
            ".3mf" => "3mf",
            _ => "mesh"
        });

        RuntimeLog.Info("Model", $"Imported '{name}' from {filePath}");
        await Task.CompletedTask;
        return obj;
    }

    public async Task ExportAsync(Guid objectId, string outputPath)
    {
        var obj = _scene.Find(objectId);
        if (obj is null)
        {
            RuntimeLog.Warn("Model", $"Export: object {objectId} not found.");
            return;
        }

        var ext = Path.GetExtension(outputPath).ToLowerInvariant();
        RuntimeLog.Info("Model", $"Exporting '{obj.Name}' as {ext} to {outputPath}");
        // Actual geometry serialisation via PicoGK goes here
        await Task.CompletedTask;
    }
}
