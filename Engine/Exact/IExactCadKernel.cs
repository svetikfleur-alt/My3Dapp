namespace FormaCore.Engine.Exact;

/// <summary>
/// Engine-owned boundary to the exact B-Rep kernel (OCCT behind My3DApp.Occt).
/// UI issues modeling commands; the kernel owns geometry. All dimensions in mm.
/// </summary>
public interface IExactCadKernel : IDisposable
{
    bool IsAlive { get; }

    IExactBodyHandle CreateBox(double dx, double dy, double dz);

    IExactBodyHandle CreateCylinder(double radius, double height);

    /// <summary>Returns a new translated body; the input remains owned by the caller.</summary>
    IExactBodyHandle Translated(IExactBodyHandle body, double dx, double dy, double dz);

    /// <summary>Returns a new body; inputs remain owned by the caller.</summary>
    IExactBodyHandle BooleanSubtract(IExactBodyHandle target, IExactBodyHandle tool);

    IExactBodyHandle BooleanUnion(IExactBodyHandle a, IExactBodyHandle b);
}
