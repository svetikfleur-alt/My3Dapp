using System.Collections.ObjectModel;
using My3DApp.Core;

namespace My3DApp.Engine;

// ── Parametric solid definitions ──────────────────────────────────────────────
// Solids are stored as parameters; Mesh is only materialised at export time.

public abstract record SolidParams
{
    public abstract Mesh ToMesh();
}

public record BoxParams(float W, float H, float D) : SolidParams
{
    public override Mesh ToMesh() => Mesh.Box(W, H, D);
    public override string ToString() => $"Box {W}×{H}×{D} mm";
}

public record CylinderParams(float Radius, float Height, int Segments = 32) : SolidParams
{
    public override Mesh ToMesh() => Mesh.Cylinder(Radius, Height, Segments);
    public override string ToString() => $"Cylinder r={Radius} h={Height} mm";
}

public record SphereParams(float Radius, int Segments = 32) : SolidParams
{
    public override Mesh ToMesh() => Mesh.Sphere(Radius, Segments, Segments);
    public override string ToString() => $"Sphere r={Radius} mm";
}

public record ImportedMeshParams(Mesh Mesh) : SolidParams
{
    public override Mesh ToMesh() => Mesh;
    public override string ToString() => $"Imported ({Mesh.Triangles.Count} tris)";
}

// ── Scene object ──────────────────────────────────────────────────────────────

public class SceneObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Object";
    public string Type { get; set; } = "mesh";
    public bool Visible { get; set; } = true;
    public float[] Transform { get; set; } = Identity4x4();

    // Parametric solid definition — null for objects with no computable geometry
    public SolidParams? Solid { get; set; }

    // Source file path for imported assets
    public string? SourcePath { get; set; }

    private static float[] Identity4x4() =>
    [
        1,0,0,0,
        0,1,0,0,
        0,0,1,0,
        0,0,0,1
    ];
}

public class SceneGraph
{
    private readonly List<SceneObject> _objects = new();
    public IReadOnlyList<SceneObject> Objects => _objects.AsReadOnly();

    public event Action<SceneObject>? ObjectAdded;
    public event Action<Guid>? ObjectRemoved;
    public event Action? SceneCleared;

    public SceneObject Add(string name, string type = "mesh")
    {
        var obj = new SceneObject { Name = name, Type = type };
        _objects.Add(obj);
        ObjectAdded?.Invoke(obj);
        RuntimeLog.Info("Scene", $"Added '{name}' ({type}) id={obj.Id}");
        return obj;
    }

    public bool Remove(Guid id)
    {
        var obj = _objects.FirstOrDefault(o => o.Id == id);
        if (obj is null) return false;
        _objects.Remove(obj);
        ObjectRemoved?.Invoke(id);
        return true;
    }

    public void Clear()
    {
        _objects.Clear();
        SceneCleared?.Invoke();
    }

    public SceneObject? Find(Guid id) => _objects.FirstOrDefault(o => o.Id == id);

    // Re-inserts a previously removed SceneObject, preserving its original Id.
    public void AddExisting(SceneObject obj)
    {
        if (_objects.Any(o => o.Id == obj.Id)) return; // already present
        _objects.Add(obj);
        ObjectAdded?.Invoke(obj);
        RuntimeLog.Info("Scene", $"Restored '{obj.Name}' ({obj.Type}) id={obj.Id}");
    }
}
