using My3DApp.Occt;

namespace FormaCore.Engine.Exact;

/// <summary>
/// IExactCadKernel/IExactCadExporter over the My3DApp.Occt C++/CLI bridge.
/// The only place (besides the viewport adapter) allowed to touch OCCT types.
/// </summary>
public sealed class OcctExactKernel : IExactCadKernel, IExactCadExporter
{
    private OcctKernel? _kernel = new();

    public bool IsAlive => _kernel is { IsAlive: true };

    public IExactBodyHandle CreateBox(double dx, double dy, double dz)
        => new OcctBodyHandle(Kernel.CreateBox(dx, dy, dz));

    public IExactBodyHandle CreateCylinder(double radius, double height)
        => new OcctBodyHandle(Kernel.CreateCylinder(radius, height));

    public IExactBodyHandle CreateWire(double[] points2d, bool closed)
        => new OcctBodyHandle(Kernel.CreateWire(points2d, closed));

    public IExactBodyHandle CreateCircleWire(double radius)
        => new OcctBodyHandle(Kernel.CreateCircleWire(radius));

    public IExactBodyHandle CreateFace(IExactBodyHandle wire)
        => new OcctBodyHandle(Kernel.CreateFace(Unwrap(wire)));

    public IExactBodyHandle CreatePrism(IExactBodyHandle face, double dx, double dy, double dz)
        => new OcctBodyHandle(Kernel.CreatePrism(Unwrap(face), dx, dy, dz));

    public IExactBodyHandle CreateCompound(System.Collections.Generic.IReadOnlyList<IExactBodyHandle> bodies)
    {
        var arr = new My3DApp.Occt.OcctBody[bodies.Count];
        for (int i = 0; i < bodies.Count; i++) arr[i] = Unwrap(bodies[i]);
        return new OcctBodyHandle(Kernel.CreateCompound(arr));
    }

    public IExactBodyHandle Translated(IExactBodyHandle body, double dx, double dy, double dz)
        => new OcctBodyHandle(Kernel.Translated(Unwrap(body), dx, dy, dz));

    public IExactBodyHandle BooleanSubtract(IExactBodyHandle target, IExactBodyHandle tool)
        => new OcctBodyHandle(Kernel.BooleanSubtract(Unwrap(target), Unwrap(tool)));

    public IExactBodyHandle BooleanUnion(IExactBodyHandle a, IExactBodyHandle b)
        => new OcctBodyHandle(Kernel.BooleanUnion(Unwrap(a), Unwrap(b)));

    public void ExportStep(IExactBodyHandle body, string path)
        => Kernel.ExportStep(Unwrap(body), path);

    public void Dispose()
    {
        _kernel?.Dispose();
        _kernel = null;
    }

    private OcctKernel Kernel => _kernel ?? throw new ObjectDisposedException(nameof(OcctExactKernel));

    internal static OcctBody Unwrap(IExactBodyHandle handle)
    {
        if (handle is not OcctBodyHandle occt)
            throw new ArgumentException($"handle is not an OCCT body ({handle.GetType().Name})", nameof(handle));
        return occt.Body;
    }
}

/// <summary>Opaque managed wrapper over an exact OCCT B-Rep body.</summary>
public sealed class OcctBodyHandle : IExactBodyHandle
{
    internal OcctBody Body { get; }

    internal OcctBodyHandle(OcctBody body) => Body = body;

    public Guid BodyId => Body.BodyId;

    public bool IsValid => Body.IsValid;

    /// <summary>Bounding box in mm — for tests and export verification.</summary>
    public (double XMin, double YMin, double ZMin, double XMax, double YMax, double ZMax) GetBounds()
    {
        Body.GetBounds(out var xmin, out var ymin, out var zmin, out var xmax, out var ymax, out var zmax);
        return (xmin, ymin, zmin, xmax, ymax, zmax);
    }

    public void Dispose() => Body.Dispose();
}
