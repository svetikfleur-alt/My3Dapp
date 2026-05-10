using FormaCore.Core;

namespace FormaCore.Engine;

public sealed class MeshCompiler
{
    private readonly SolidCompiler _solidCompiler = new();
    private readonly SolidMesher _solidMesher = new();

    public Mesh Compile(ShapeNode node)
    {
        var solid = _solidCompiler.Compile(node);
        return _solidMesher.Tessellate(solid);
    }
}
