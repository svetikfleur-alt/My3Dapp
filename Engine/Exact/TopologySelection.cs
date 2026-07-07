namespace FormaCore.Engine.Exact;

public enum TopologyKind
{
    Body,
    Face,
    Edge,
    Vertex,
}

/// <summary>
/// A topology pick reaching managed code from the native viewport.
/// <para>
/// HONESTY RULE (P1): <see cref="TransientIndex"/> is the kernel's per-shape map index
/// and is valid ONLY while the body instance is unchanged. It is NOT a persistent name
/// and must not be stored across regeneration. Persistent naming arrives later via
/// <see cref="ITopologyNamingService"/> (OCAF/TNaming or equivalent).
/// </para>
/// </summary>
/// <param name="BodyId">Stable body identity in the document.</param>
/// <param name="Kind">Picked sub-shape kind.</param>
/// <param name="TransientIndex">Sub-shape index within the current unchanged body instance (1-based, kernel order); 0 for whole-body picks.</param>
/// <param name="Provenance">Where the pick came from (e.g. "viewport-click", "viewport-hover").</param>
public sealed record TopologySelection(Guid BodyId, TopologyKind Kind, int TransientIndex, string Provenance);

/// <summary>
/// Future persistent-naming boundary (OCAF/TNaming). P1 only establishes the interface;
/// no implementation claims persistent naming until a regeneration test proves it.
/// </summary>
public interface ITopologyNamingService
{
    /// <summary>Try to resolve a selection made on a previous body instance to the regenerated instance.</summary>
    bool TryRemap(TopologySelection previous, IExactBodyHandle regenerated, out TopologySelection? remapped);
}
