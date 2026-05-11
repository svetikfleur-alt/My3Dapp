using System.Collections.ObjectModel;
using My3DApp.Core;

namespace My3DApp.Engine;

// ── Parametric solid definitions ──────────────────────────────────────────────
// Solids are stored as parameters; Mesh is only materialised at export time.

public abstract record SolidParams
{
    public abstract Mesh ToMesh();
}

public record BoxParams(float W, float H, float D, float X = 0, float Y = 0, float Z = 0) : SolidParams
{
    public override Mesh ToMesh()
    {
        var m = Mesh.Box(W, H, D);
        return (X != 0 || Y != 0 || Z != 0) ? m.Translated(X, Y, Z) : m;
    }
    public override string ToString() => $"Box {W}×{H}×{D} mm @ ({X},{Y},{Z})";
}

public record CylinderParams(float Radius, float Height, int Segments = 32, float X = 0, float Y = 0, float Z = 0) : SolidParams
{
    public override Mesh ToMesh()
    {
        var m = Mesh.Cylinder(Radius, Height, Segments);
        return (X != 0 || Y != 0 || Z != 0) ? m.Translated(X, Y, Z) : m;
    }
    public override string ToString() => $"Cylinder r={Radius} h={Height} mm @ ({X},{Y},{Z})";
}

public record SphereParams(float Radius, int Segments = 32, float X = 0, float Y = 0, float Z = 0) : SolidParams
{
    public override Mesh ToMesh()
    {
        var m = Mesh.Sphere(Radius, Segments, Segments);
        return (X != 0 || Y != 0 || Z != 0) ? m.Translated(X, Y, Z) : m;
    }
    public override string ToString() => $"Sphere r={Radius} mm @ ({X},{Y},{Z})";
}

public record ImportedMeshParams(Mesh Mesh) : SolidParams
{
    public override Mesh ToMesh() => Mesh;
    public override string ToString() => $"Imported ({Mesh.Triangles.Count} tris)";
}

public record RevolveProfileParams(float[] Profile, float AngleDeg = 360f, int Segments = 32,
                                   float X = 0, float Y = 0, float Z = 0) : SolidParams
{
    public override Mesh ToMesh()
    {
        var m = Mesh.RevolveProfile(Profile, AngleDeg, Segments);
        return (X != 0 || Y != 0 || Z != 0) ? m.Translated(X, Y, Z) : m;
    }
    public override string ToString() => $"Revolve {AngleDeg}° {Segments}seg @ ({X},{Y},{Z})";
}

public record ExtrudePolygonParams(float[] Points2D, float Depth,
                                   float X = 0, float Y = 0, float Z = 0) : SolidParams
{
    public override Mesh ToMesh()
    {
        var m = Mesh.ExtrudePolygon(Points2D, Depth);
        return (X != 0 || Y != 0 || Z != 0) ? m.Translated(X, Y, Z) : m;
    }
    public override string ToString() => $"ExtrudePolygon depth={Depth}mm @ ({X},{Y},{Z})";
}

// Boolean union: merges both meshes into one combined mesh.
public record BooleanUnionParams(SolidParams A, SolidParams B) : SolidParams
{
    public override Mesh ToMesh()
    {
        var m = new Mesh();
        foreach (var t in A.ToMesh().Triangles) m.Triangles.Add(t);
        foreach (var t in B.ToMesh().Triangles) m.Triangles.Add(t);
        return m;
    }
    public override string ToString() => $"Union ({A}) ∪ ({B})";
}

// Boolean subtract: result is approximated as the target solid (tool body removed on export).
// Full CSG requires PicoGK; this stores params for future implementation.
public record BooleanSubtractParams(SolidParams Target, SolidParams Tool) : SolidParams
{
    public override Mesh ToMesh() => Target.ToMesh(); // approximation — returns target only
    public override string ToString() => $"Subtract ({Target}) − ({Tool})";
}

// Boolean intersect: approximated as solid A (intersection requires true CSG via PicoGK).
public record BooleanIntersectParams(SolidParams A, SolidParams B) : SolidParams
{
    public override Mesh ToMesh() => A.ToMesh(); // approximation — returns A only
    public override string ToString() => $"Intersect ({A}) ∩ ({B})";
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
