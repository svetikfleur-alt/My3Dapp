namespace FormaCore.Engine.Exact;

/// <summary>Exact-geometry export. STEP must come from real B-Rep, never from triangles.</summary>
public interface IExactCadExporter
{
    /// <summary>Writes the body to a STEP (AP214) file. Throws on kernel failure.</summary>
    void ExportStep(IExactBodyHandle body, string path);
}
