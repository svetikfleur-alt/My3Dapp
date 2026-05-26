using FormaCore.Core;
using FormaCore.Engine;
using Xunit;

namespace EngineTests;

/// <summary>
/// Tests for ProfileBuilder — verifies sketch → closed profile conversion.
/// </summary>
public class ProfileBuilderTests
{
    // ── Rectangle → Profile ──────────────────────────────────────────────

    [Fact]
    public void Rectangle_ProducesClosedProfile()
    {
        var sketch = CreateSketchWithRectangle(0, 0, 10, 10);
        Assert.True(ProfileBuilder.TryBuild(sketch, out var profile, out var error),
            $"Expected success but got: {error}");
        Assert.True(profile.Points.Count >= 4,
            $"Expected at least 4 points, got {profile.Points.Count}");
    }

    [Fact]
    public void Rectangle_IsClosedProfile()
    {
        var sketch = CreateSketchWithRectangle(0, 0, 20, 15);
        Assert.True(ProfileBuilder.CanBuildClosedProfile(sketch.Entities));
    }

    // ── Circle → Profile ─────────────────────────────────────────────────

    [Fact]
    public void Circle_ProducesClosedProfile()
    {
        var sketch = CreateSketchWithCircle(0, 0, 10);
        Assert.True(ProfileBuilder.TryBuild(sketch, out var profile, out var error),
            $"Expected success but got: {error}");
        Assert.True(profile.Points.Count >= 16,
            $"Expected many points for circle tessellation, got {profile.Points.Count}");
    }

    [Fact]
    public void Circle_IsClosedProfile()
    {
        var sketch = CreateSketchWithCircle(5, 5, 8);
        Assert.True(ProfileBuilder.CanBuildClosedProfile(sketch.Entities));
    }

    // ── Polygon → Profile ────────────────────────────────────────────────

    [Fact]
    public void Polygon_ProducesClosedProfile()
    {
        var sketch = CreateSketchWithPolygon(0, 0, 10, 6);
        Assert.True(ProfileBuilder.TryBuild(sketch, out var profile, out var error),
            $"Expected success but got: {error}");
        Assert.Equal(6, profile.Points.Count);
    }

    // ── Connected lines → Profile ────────────────────────────────────────

    [Fact]
    public void TriangleFromLines_ProducesClosedProfile()
    {
        var sketch = CreateSketchWithTriangle();
        Assert.True(ProfileBuilder.TryBuild(sketch, out var profile, out var error),
            $"Expected success but got: {error}");
        Assert.True(profile.Points.Count >= 3);
    }

    // ── Rejection cases ──────────────────────────────────────────────────

    [Fact]
    public void EmptySketch_Rejected()
    {
        var sketch = new SketchFeature { Name = "Empty" };
        Assert.False(ProfileBuilder.TryBuild(sketch, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void SingleLine_NotClosed()
    {
        var sketch = new SketchFeature
        {
            Name = "OpenLine",
            Entities = new List<CadSketchEntity>
            {
                new CadSketchLine { StartX = 0, StartY = 0, EndX = 10, EndY = 0 }
            }
        };
        Assert.False(ProfileBuilder.TryBuild(sketch, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void OpenChain_NotClosed()
    {
        var sketch = new SketchFeature
        {
            Name = "OpenChain",
            Entities = new List<CadSketchEntity>
            {
                new CadSketchLine { StartX = 0, StartY = 0, EndX = 10, EndY = 0 },
                new CadSketchLine { StartX = 10, StartY = 0, EndX = 10, EndY = 10 }
                // Missing closing segment
            }
        };
        Assert.False(ProfileBuilder.TryBuild(sketch, out _, out var error));
        Assert.Contains("close", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConstructionOnly_NotClosed()
    {
        var sketch = new SketchFeature
        {
            Name = "ConstructionOnly",
            Entities = new List<CadSketchEntity>
            {
                new CadSketchCircle { CenterX = 0, CenterY = 0, Radius = 5, IsConstruction = true }
            }
        };
        Assert.False(ProfileBuilder.TryBuild(sketch, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void PointOnly_NotClosed()
    {
        var sketch = new SketchFeature
        {
            Name = "PointOnly",
            Entities = new List<CadSketchEntity>
            {
                new CadSketchPoint { X = 5, Y = 5 }
            }
        };
        Assert.False(ProfileBuilder.TryBuild(sketch, out _, out var error));
        Assert.NotEmpty(error);
    }

    // ── Plane Orientation ────────────────────────────────────────────────

    [Theory]
    [InlineData("Top", PlaneOrientation.Top)]
    [InlineData("top", PlaneOrientation.Top)]
    [InlineData("Front", PlaneOrientation.Front)]
    [InlineData("front", PlaneOrientation.Front)]
    [InlineData("Right", PlaneOrientation.Right)]
    [InlineData("right", PlaneOrientation.Right)]
    [InlineData("Unknown", PlaneOrientation.Top)]
    public void GetOrientation_MapsCorrectly(string input, PlaneOrientation expected)
    {
        Assert.Equal(expected, ProfileBuilder.GetOrientation(input));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static SketchFeature CreateSketchWithRectangle(double x, double y, double w, double h)
    {
        return new SketchFeature
        {
            Name = "TestRect",
            Entities = new List<CadSketchEntity>
            {
                new CadSketchRectangle { X = x, Y = y, Width = w, Height = h }
            }
        };
    }

    private static SketchFeature CreateSketchWithCircle(double cx, double cy, double r)
    {
        return new SketchFeature
        {
            Name = "TestCircle",
            Entities = new List<CadSketchEntity>
            {
                new CadSketchCircle { CenterX = cx, CenterY = cy, Radius = r }
            }
        };
    }

    private static SketchFeature CreateSketchWithPolygon(double cx, double cy, double r, int sides)
    {
        return new SketchFeature
        {
            Name = "TestPolygon",
            Entities = new List<CadSketchEntity>
            {
                new CadSketchPolygon { CenterX = cx, CenterY = cy, Radius = r, Sides = sides }
            }
        };
    }

    private static SketchFeature CreateSketchWithTriangle()
    {
        return new SketchFeature
        {
            Name = "TestTriangle",
            Entities = new List<CadSketchEntity>
            {
                new CadSketchLine { StartX = 0, StartY = 0, EndX = 10, EndY = 0 },
                new CadSketchLine { StartX = 10, StartY = 0, EndX = 5, EndY = 10 },
                new CadSketchLine { StartX = 5, StartY = 10, EndX = 0, EndY = 0 }
            }
        };
    }
}
