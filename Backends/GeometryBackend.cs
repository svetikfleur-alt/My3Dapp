using My3DApp.AvaloniaApp.Services;
using My3DApp.Engine;

namespace My3DApp.Backends;

/// <summary>
/// Bridges the Engine layer with PicoGK computational geometry operations.
/// PicoGK API calls are guarded so the project compiles even without the native runtime.
/// </summary>
public class GeometryBackend
{
    private readonly SceneGraph _scene;
    private bool _picoGkAvailable;

    public GeometryBackend(SceneGraph scene)
    {
        _scene = scene;
        TryInitPicoGk();
    }

    private void TryInitPicoGk()
    {
        try
        {
            // PicoGK requires its native library at runtime.
            // We probe for it so we can degrade gracefully if not present.
            var type = Type.GetType("PicoGK.Library, PicoGK");
            _picoGkAvailable = type != null;
            RuntimeLog.Info("Geometry", _picoGkAvailable
                ? "PicoGK native runtime found."
                : "PicoGK native runtime not found — geometry operations will be limited.");
        }
        catch (Exception ex)
        {
            _picoGkAvailable = false;
            RuntimeLog.Warn("Geometry", $"PicoGK probe failed: {ex.Message}");
        }
    }

    public async Task<SceneObject> ExtrudeAsync(Guid sketchId, float depth)
    {
        var sketch = _scene.Find(sketchId);
        var name = sketch != null ? $"Extrude({sketch.Name})" : "Extrude";
        var obj = _scene.Add(name, "extrude");

        if (_picoGkAvailable)
            await RunPicoGkExtrudeAsync(sketch, depth, obj);

        return obj;
    }

    public async Task<SceneObject> RevolveAsync(Guid profileId, float angle)
    {
        var profile = _scene.Find(profileId);
        var name = profile != null ? $"Revolve({profile.Name})" : "Revolve";
        var obj = _scene.Add(name, "revolve");

        if (_picoGkAvailable)
            await RunPicoGkRevolveAsync(profile, angle, obj);

        return obj;
    }

    public async Task<bool> BooleanUnionAsync(Guid targetId, Guid toolId)
    {
        var target = _scene.Find(targetId);
        var tool   = _scene.Find(toolId);
        if (target is null || tool is null) return false;

        RuntimeLog.Info("Geometry", $"Boolean Union: {target.Name} ∪ {tool.Name}");
        if (_picoGkAvailable)
            await Task.Run(() => { /* PicoGK voxel union */ });

        _scene.Remove(toolId);
        return true;
    }

    // Actual PicoGK calls isolated here to keep dependency optional
    private static Task RunPicoGkExtrudeAsync(SceneObject? sketch, float depth, SceneObject output)
    {
        return Task.Run(() =>
        {
            RuntimeLog.Info("Geometry", $"PicoGK extrude depth={depth}");
            // PicoGK.Voxels, PicoGK.Mesh etc. would go here
        });
    }

    private static Task RunPicoGkRevolveAsync(SceneObject? profile, float angle, SceneObject output)
    {
        return Task.Run(() =>
        {
            RuntimeLog.Info("Geometry", $"PicoGK revolve angle={angle}°");
        });
    }
}
