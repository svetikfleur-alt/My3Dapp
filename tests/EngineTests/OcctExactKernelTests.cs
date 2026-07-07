using FormaCore.Engine.Exact;
using Xunit;

namespace EngineTests;

public class OcctExactKernelTests
{
    private const double Tol = 1e-7;

    [Fact]
    public void KernelLifecycle_CreateDispose_IsAliveReflectsState()
    {
        var kernel = new OcctExactKernel();
        Assert.True(kernel.IsAlive);
        kernel.Dispose();
        Assert.False(kernel.IsAlive);
        Assert.Throws<ObjectDisposedException>(() => kernel.CreateBox(1, 1, 1));
    }

    [Fact]
    public void CreateBox_ProducesExactBoundsInMm()
    {
        using var kernel = new OcctExactKernel();
        using var body = kernel.CreateBox(80, 60, 30);
        Assert.True(body.IsValid);
        Assert.NotEqual(Guid.Empty, body.BodyId);

        var b = ((OcctBodyHandle)body).GetBounds();
        Assert.Equal(0, b.XMin, Tol);
        Assert.Equal(80, b.XMax, Tol);
        Assert.Equal(60, b.YMax, Tol);
        Assert.Equal(30, b.ZMax, Tol);
    }

    [Fact]
    public void CreateCylinder_ProducesExactBounds()
    {
        using var kernel = new OcctExactKernel();
        using var body = kernel.CreateCylinder(12, 30);
        var b = ((OcctBodyHandle)body).GetBounds();
        Assert.Equal(-12, b.XMin, 1e-6);
        Assert.Equal(12, b.XMax, 1e-6);
        Assert.Equal(30, b.ZMax, Tol);
    }

    [Fact]
    public void CreateBox_InvalidDimensions_ThrowsWithKernelMessage()
    {
        using var kernel = new OcctExactKernel();
        var ex = Assert.Throws<InvalidOperationException>(() => kernel.CreateBox(-1, 10, 10));
        Assert.Contains("CreateBox", ex.Message);
    }

    [Fact]
    public void BooleanSubtract_BoxMinusCylinder_KeepsOuterBounds()
    {
        using var kernel = new OcctExactKernel();
        using var box = kernel.CreateBox(80, 60, 30);
        using var tool = kernel.CreateCylinder(12, 30);
        using var result = kernel.BooleanSubtract(box, tool);

        Assert.True(result.IsValid);
        var b = ((OcctBodyHandle)result).GetBounds();
        Assert.Equal(80, b.XMax, 1e-6);
        Assert.Equal(60, b.YMax, 1e-6);
        Assert.Equal(30, b.ZMax, 1e-6);
    }

    [Fact]
    public void BodyHandle_UseAfterDispose_Throws()
    {
        using var kernel = new OcctExactKernel();
        var body = kernel.CreateBox(10, 10, 10);
        body.Dispose();
        Assert.False(body.IsValid);
        using var other = kernel.CreateBox(5, 5, 5);
        Assert.Throws<ObjectDisposedException>(() => kernel.BooleanUnion(body, other));
    }

    [Fact]
    public void ExportStep_WritesRealStepFile()
    {
        using var kernel = new OcctExactKernel();
        using var box = kernel.CreateBox(80, 60, 30);
        using var hole = kernel.CreateCylinder(12, 30);
        using var body = kernel.BooleanSubtract(box, hole);

        var path = Path.Combine(Path.GetTempPath(), $"occt-p1-{Guid.NewGuid():N}.step");
        try
        {
            ((IExactCadExporter)kernel).ExportStep(body, path);
            var text = File.ReadAllText(path);
            Assert.True(new FileInfo(path).Length > 1000, "STEP file suspiciously small");
            Assert.StartsWith("ISO-10303-21", text);
            // Real B-Rep: exact solid entities present, no triangulation entities.
            Assert.Contains("MANIFOLD_SOLID_BREP", text);
            Assert.Contains("CYLINDRICAL_SURFACE", text);
            Assert.DoesNotContain("TRIANGULATED", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExportStep_InvalidPath_ThrowsInsteadOfFakeSuccess()
    {
        using var kernel = new OcctExactKernel();
        using var box = kernel.CreateBox(10, 10, 10);
        Assert.Throws<InvalidOperationException>(
            () => ((IExactCadExporter)kernel).ExportStep(box, @"Z:\nonexistent-dir-p1\x.step"));
    }
}
