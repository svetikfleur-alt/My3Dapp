using System.Collections.ObjectModel;
using My3DApp.Core;

namespace My3DApp.Engine;

public class SceneObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Object";
    public string Type { get; set; } = "mesh";
    public bool Visible { get; set; } = true;
    public float[] Transform { get; set; } = Identity4x4();

    // Optional computed geometry — null for features that haven't been meshed yet
    public Mesh? Geometry { get; set; }

    // Path to the source file if this was imported
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
}
