using FormaCore.Core;
using FormaCore.Engine;
using Xunit;

namespace EngineTests;

/// <summary>
/// Tests for MeshBuilder — verifies primitive tessellation produces valid meshes.
/// </summary>
public class MeshBuilderTests
{
    // ── Box ──────────────────────────────────────────────────────────────

    [Fact]
    public void CreateBox_ProducesValidMesh()
    {
        var mesh = MeshBuilder.CreateBox(10, 20, 30);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
        // A box has 8 vertices and 12 triangles (2 per face × 6 faces)
        Assert.Equal(8, mesh.Vertices.Count);
        Assert.Equal(12, mesh.Triangles.Count);
    }

    [Fact]
    public void CreateBox_AllIndicesInRange()
    {
        var mesh = MeshBuilder.CreateBox(5, 5, 5);
        AssertAllIndicesValid(mesh);
    }

    [Fact]
    public void CreateBox_NoNaNVertices()
    {
        var mesh = MeshBuilder.CreateBox(1, 1, 1);
        AssertNoNaNVertices(mesh);
    }

    // ── Cylinder ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateCylinder_ProducesValidMesh()
    {
        var mesh = MeshBuilder.CreateCylinder(5, 10);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
        AssertAllIndicesValid(mesh);
        AssertNoNaNVertices(mesh);
    }

    [Fact]
    public void CreateCylinder_HasCaps()
    {
        var mesh = MeshBuilder.CreateCylinder(5, 10);
        // Should have top and bottom caps — more than just a tube
        // A cylinder with N segments has 2*N side triangles + 2*(N-2) cap triangles
        Assert.True(mesh.Triangles.Count > 10, "Cylinder should have significant triangle count (caps + sides).");
    }

    // ── Sphere ───────────────────────────────────────────────────────────

    [Fact]
    public void CreateSphere_ProducesValidMesh()
    {
        var mesh = MeshBuilder.CreateSphere(10);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
        AssertAllIndicesValid(mesh);
        AssertNoNaNVertices(mesh);
    }

    // ── Cone ─────────────────────────────────────────────────────────────

    [Fact]
    public void CreateCone_ProducesValidMesh()
    {
        var mesh = MeshBuilder.CreateCone(0, 5, 10);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
        AssertAllIndicesValid(mesh);
    }

    // ── Torus ────────────────────────────────────────────────────────────

    [Fact]
    public void CreateTorus_ProducesValidMesh()
    {
        var mesh = MeshBuilder.CreateTorus(10, 3);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
        AssertAllIndicesValid(mesh);
        AssertNoNaNVertices(mesh);
    }

    // ── Polygon Prism ────────────────────────────────────────────────────

    [Fact]
    public void CreatePolygonPrism_SquareProfile_ProducesValidMesh()
    {
        var profile = new List<Vector2D>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };
        var mesh = MeshBuilder.CreatePolygonPrism(profile, 5);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
        AssertAllIndicesValid(mesh);
    }

    // ── ExtrudedProfile ──────────────────────────────────────────────────

    [Fact]
    public void CreateExtrudedProfile_RectangleProfile_ProducesValidMesh()
    {
        var profile = new List<Vector2D>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };
        var mesh = MeshBuilder.CreateExtrudedProfile(
            profile, 20, PlaneOrientation.Top, false, 0, 0);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
        AssertAllIndicesValid(mesh);
        AssertNoNaNVertices(mesh);
    }

    // ── Merge ────────────────────────────────────────────────────────────

    [Fact]
    public void Merge_CombinesTwoBoxes()
    {
        var a = MeshBuilder.CreateBox(5, 5, 5);
        var b = MeshBuilder.CreateBox(3, 3, 3);
        var merged = MeshBuilder.Merge(a, b);
        Assert.Equal(a.Vertices.Count + b.Vertices.Count, merged.Vertices.Count);
        Assert.Equal(a.Triangles.Count + b.Triangles.Count, merged.Triangles.Count);
        AssertAllIndicesValid(merged);
    }

    // ── Translate ────────────────────────────────────────────────────────

    [Fact]
    public void Translate_MovesAllVertices()
    {
        var mesh = MeshBuilder.CreateBox(2, 2, 2);
        var translated = MeshBuilder.Translate(mesh, 10, 20, 30);
        // All vertices should be shifted
        foreach (var v in translated.Vertices)
        {
            Assert.True(v.X >= 9, "Expected X translation");
            Assert.True(v.Y >= 19, "Expected Y translation");
            Assert.True(v.Z >= 29, "Expected Z translation");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void AssertAllIndicesValid(Mesh mesh)
    {
        foreach (var tri in mesh.Triangles)
        {
            Assert.InRange(tri.A, 0, mesh.Vertices.Count - 1);
            Assert.InRange(tri.B, 0, mesh.Vertices.Count - 1);
            Assert.InRange(tri.C, 0, mesh.Vertices.Count - 1);
        }
    }

    private static void AssertNoNaNVertices(Mesh mesh)
    {
        foreach (var v in mesh.Vertices)
        {
            Assert.False(double.IsNaN(v.X), "Vertex X is NaN");
            Assert.False(double.IsNaN(v.Y), "Vertex Y is NaN");
            Assert.False(double.IsNaN(v.Z), "Vertex Z is NaN");
            Assert.False(double.IsInfinity(v.X), "Vertex X is Infinity");
            Assert.False(double.IsInfinity(v.Y), "Vertex Y is Infinity");
            Assert.False(double.IsInfinity(v.Z), "Vertex Z is Infinity");
        }
    }
}
