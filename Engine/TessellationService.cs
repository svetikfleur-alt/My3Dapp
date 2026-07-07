using FormaCore.Core;

namespace FormaCore.Engine;

/// <summary>
/// Engine-owned tessellation for viewport payloads and boundary (STL/OBJ) export.
/// Owns the mesher and the experimental PicoGK fallback so no UI-layer class
/// performs geometry work. Meshes produced here are transport only and must
/// never enter the domain model.
/// </summary>
public sealed class TessellationService
{
    private readonly SolidMesher _mesher = new();

    public bool UsePicoGkKernel { get; set; }

    public Mesh Tessellate(Solid solid)
    {
        if (UsePicoGkKernel)
        {
            Mesh? result = null;
            try
            {
                PicoGkExperimentalGeometryKernelAdapter.RunInSession(0.5f, adapter =>
                {
                    var compiler = new KernelCompiler(adapter, _mesher);
                    var kernelBody = compiler.Compile(solid);
                    result = adapter.Tessellate(kernelBody);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"PicoGK Kernel failure: {ex.Message}");
            }

            if (result != null)
            {
                return result;
            }
        }

        return _mesher.Tessellate(solid);
    }
}
