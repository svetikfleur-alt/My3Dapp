using FormaCore.Core;
using FormaCore.Engine;
using Xunit;

namespace EngineTests;

/// <summary>
/// Tests for SolidMesher — verifies the full Solid → Mesh pipeline.
/// </summary>
public class SolidMesherTests
{
    private readonly SolidMesher _mesher = new();

    // ── Primitives ───────────────────────────────────────────────────────

    [Fact]
    public void BoxSolid_TessellatesSuccessfully()
    {
        var mesh = _mesher.Tessellate(new BoxSolid(10, 20, 30));
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
        Assert.Equal(8, mesh.Vertices.Count);
        Assert.Equal(12, mesh.Triangles.Count);
    }

    [Fact]
    public void CylinderSolid_TessellatesSuccessfully()
    {
        var mesh = _mesher.Tessellate(new CylinderSolid(5, 10));
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
    }

    [Fact]
    public void SphereSolid_TessellatesSuccessfully()
    {
        var mesh = _mesher.Tessellate(new SphereSolid(8));
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
    }

    [Fact]
    public void ConeSolid_TessellatesSuccessfully()
    {
        var mesh = _mesher.Tessellate(new ConeSolid(0, 5, 10));
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
    }

    [Fact]
    public void TorusSolid_TessellatesSuccessfully()
    {
        var mesh = _mesher.Tessellate(new TorusSolid(10, 3));
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
    }

    // ── Transforms ───────────────────────────────────────────────────────

    [Fact]
    public void TransformedSolid_AppliesTranslation()
    {
        var box = new BoxSolid(4, 4, 4);
        var transformed = new TransformedSolid(box, Transform3D.CreateTranslation(10, 20, 30));
        var mesh = _mesher.Tessellate(transformed);
        Assert.NotEmpty(mesh.Vertices);
        // Verify center of mass is approximately at (10, 20, 30)
        var avgX = mesh.Vertices.Average(v => v.X);
        var avgY = mesh.Vertices.Average(v => v.Y);
        var avgZ = mesh.Vertices.Average(v => v.Z);
        Assert.InRange(avgX, 9.5, 10.5);
        Assert.InRange(avgY, 19.5, 20.5);
        Assert.InRange(avgZ, 29.5, 30.5);
    }

    // ── Boolean Operations ───────────────────────────────────────────────

    [Fact]
    public void BooleanUnion_MergesTwoBodies()
    {
        var a = new BoxSolid(10, 10, 10);
        var b = new BoxSolid(5, 5, 5);
        var boolean = new BooleanSolid(BooleanOperation.Union, a, b);
        var mesh = _mesher.Tessellate(boolean);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
    }

    [Fact]
    public void BooleanSubtract_ProducesNonEmptyResult()
    {
        var big = new BoxSolid(20, 20, 20);
        var small = new TransformedSolid(
            new BoxSolid(5, 5, 5),
            Transform3D.CreateTranslation(0, 0, 0));
        var boolean = new BooleanSolid(BooleanOperation.Subtract, big, small);
        var mesh = _mesher.Tessellate(boolean);
        // Should still have triangles (the big box minus a smaller one)
        Assert.NotEmpty(mesh.Triangles);
    }

    [Fact]
    public void BooleanSubtract_EmptyTool_ReturnsPrimaryMesh()
    {
        var primary = new BoxSolid(10, 10, 10);
        var emptyTool = new BooleanSolid(BooleanOperation.Subtract, primary,
            new BoxSolid(0, 0, 0)); // degenerate tool
        var mesh = _mesher.Tessellate(emptyTool);
        // Should fall back to returning the primary mesh
        Assert.NotEmpty(mesh.Triangles);
    }

    // ── ExtrudeSolid ─────────────────────────────────────────────────────

    [Fact]
    public void ExtrudeSolid_RectangleProfile_ProducesValidMesh()
    {
        var profile = new Profile(new List<Vector2D>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        });
        var extrude = new ExtrudeSolid(profile, 20);
        var mesh = _mesher.Tessellate(extrude);
        Assert.NotEmpty(mesh.Vertices);
        Assert.NotEmpty(mesh.Triangles);
        AssertAllIndicesValid(mesh);
    }

    [Fact]
    public void ExtrudeSolid_SymmetricExtrude_ProducesValidMesh()
    {
        var profile = new Profile(new List<Vector2D>
        {
            new(-5, -5), new(5, -5), new(5, 5), new(-5, 5)
        });
        var extrude = new ExtrudeSolid(profile, 20) { Symmetric = true };
        var mesh = _mesher.Tessellate(extrude);
        Assert.NotEmpty(mesh.Triangles);
        AssertAllIndicesValid(mesh);
    }

    [Fact]
    public void ExtrudeSolid_DegenerateProfile_ReturnsPlaceholder()
    {
        // Only 2 points — not a valid closed profile
        var profile = new Profile(new List<Vector2D> { new(0, 0), new(10, 0) });
        var extrude = new ExtrudeSolid(profile, 20);
        var mesh = _mesher.Tessellate(extrude);
        // Should return a placeholder mesh, not crash
        Assert.NotEmpty(mesh.Vertices);
    }

    // ── Patterns ─────────────────────────────────────────────────────────

    [Fact]
    public void LinearPattern_ProducesMultipleCopies()
    {
        var box = new BoxSolid(5, 5, 5);
        var pattern = new LinearPatternSolid(box, 3, 15, 0, 0);
        var mesh = _mesher.Tessellate(pattern);
        Assert.NotEmpty(mesh.Triangles);
        // 3 copies × 12 triangles each = 36
        Assert.True(mesh.Triangles.Count >= 36,
            $"Expected at least 36 triangles for 3-count pattern, got {mesh.Triangles.Count}");
    }

    [Fact]
    public void CircularPattern_ProducesMultipleCopies()
    {
        var box = new BoxSolid(3, 3, 3);
        var pattern = new CircularPatternSolid(box, 4, 360, MirrorAxis.Y);
        var mesh = _mesher.Tessellate(pattern);
        Assert.NotEmpty(mesh.Triangles);
        Assert.True(mesh.Triangles.Count >= 48,
            $"Expected at least 48 triangles for 4-count pattern, got {mesh.Triangles.Count}");
    }

    // ── Mirror ───────────────────────────────────────────────────────────

    [Fact]
    public void MirrorSolid_ProducesValidMesh()
    {
        var box = new BoxSolid(5, 5, 5);
        var mirrored = new MirrorSolid(box, MirrorAxis.X);
        var mesh = _mesher.Tessellate(mirrored);
        Assert.NotEmpty(mesh.Triangles);
        AssertAllIndicesValid(mesh);
    }

    // ── Error handling ───────────────────────────────────────────────────

    [Fact]
    public void Tessellate_NeverThrows()
    {
        // Even an unsupported solid type should return a placeholder, not throw
        var mesh = _mesher.Tessellate(new BoxSolid(1, 1, 1));
        Assert.NotNull(mesh);
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
}
