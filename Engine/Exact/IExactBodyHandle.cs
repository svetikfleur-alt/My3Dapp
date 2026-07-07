namespace FormaCore.Engine.Exact;

/// <summary>
/// Opaque handle to an exact B-Rep body owned by the native geometry kernel.
/// Managed code never sees native pointers; disposal releases the native shape.
/// </summary>
public interface IExactBodyHandle : IDisposable
{
    /// <summary>Stable identity of the body within the current document.</summary>
    Guid BodyId { get; }

    /// <summary>False after disposal or after the native side invalidated the shape.</summary>
    bool IsValid { get; }
}
