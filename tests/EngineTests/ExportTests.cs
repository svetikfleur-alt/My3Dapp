using FormaCore.Core;
using FormaCore.Engine;
using FormaCore.Export;
using Xunit;

namespace EngineTests;

/// <summary>
/// Tests for STL and OBJ export — verifies export produces valid file content.
/// </summary>
public class ExportTests
{
    // ── STL Binary ───────────────────────────────────────────────────────

    [Fact]
    public void StlBinary_ProducesValidFormat()
    {
        var mesh = MeshBuilder.CreateBox(10, 10, 10);
        using var stream = new MemoryStream();
        STLExporter.ExportToStream(mesh, stream, binary: true);
        var bytes = stream.ToArray();

        // Binary STL: 80-byte header + 4-byte count + 50 bytes per triangle
        Assert.True(bytes.Length >= 84, "Binary STL too small for header + count");

        var triangleCount = BitConverter.ToUInt32(bytes, 80);
        Assert.Equal((uint)mesh.Triangles.Count, triangleCount);

        var expectedSize = 84 + (triangleCount * 50);
        Assert.Equal(expectedSize, (uint)bytes.Length);
    }

    [Fact]
    public void StlBinary_HeaderContainsAppName()
    {
        var mesh = MeshBuilder.CreateBox(5, 5, 5);
        using var stream = new MemoryStream();
        STLExporter.ExportToStream(mesh, stream, binary: true);
        var bytes = stream.ToArray();
        var header = System.Text.Encoding.ASCII.GetString(bytes, 0, 80);
        Assert.Contains("My3DApp", header);
    }

    // ── STL ASCII ────────────────────────────────────────────────────────

    [Fact]
    public void StlAscii_ProducesParseableOutput()
    {
        var mesh = MeshBuilder.CreateBox(10, 10, 10);
        using var stream = new MemoryStream();
        STLExporter.ExportToStream(mesh, stream, binary: false);
        stream.Position = 0;
        var text = new StreamReader(stream).ReadToEnd();

        Assert.StartsWith("solid model", text);
        Assert.Contains("facet normal", text);
        Assert.Contains("vertex", text);
        Assert.Contains("endsolid model", text);
    }

    [Fact]
    public void StlAscii_HasCorrectFacetCount()
    {
        var mesh = MeshBuilder.CreateBox(5, 5, 5);
        using var stream = new MemoryStream();
        STLExporter.ExportToStream(mesh, stream, binary: false);
        stream.Position = 0;
        var text = new StreamReader(stream).ReadToEnd();
        var facetCount = text.Split("facet normal").Length - 1;
        Assert.Equal(mesh.Triangles.Count, facetCount);
    }

    // ── STL File Export ──────────────────────────────────────────────────

    [Fact]
    public void StlExport_WritesToDisk()
    {
        var mesh = MeshBuilder.CreateBox(5, 5, 5);
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.stl");
        try
        {
            STLExporter.Export(mesh, path, binary: true);
            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 84, "File too small to be a valid binary STL");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ── OBJ ──────────────────────────────────────────────────────────────

    [Fact]
    public void ObjExport_ContainsVerticesAndFaces()
    {
        var mesh = MeshBuilder.CreateBox(10, 10, 10);
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");
        try
        {
            OBJExporter.Export(mesh, path);
            Assert.True(File.Exists(path));
            var text = File.ReadAllText(path);
            Assert.Contains("v ", text);
            Assert.Contains("f ", text);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ObjExport_ContainsNormals()
    {
        var mesh = MeshBuilder.CreateBox(10, 10, 10);
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");
        try
        {
            OBJExporter.Export(mesh, path);
            var text = File.ReadAllText(path);
            Assert.Contains("vn ", text);
            // Faces should reference normals: f v//vn v//vn v//vn
            Assert.Contains("//", text);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ObjExport_FaceIndicesAre1Based()
    {
        var mesh = MeshBuilder.CreateBox(5, 5, 5);
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");
        try
        {
            OBJExporter.Export(mesh, path);
            var lines = File.ReadAllLines(path);
            foreach (var line in lines.Where(l => l.StartsWith("f ")))
            {
                // OBJ face indices must be 1-based (no "f 0")
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts.Skip(1))
                {
                    var idx = int.Parse(part.Split('/')[0]);
                    Assert.True(idx >= 1, $"OBJ face index must be >= 1, got {idx}");
                }
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ObjExportWithMaterial_CreatesMtlFile()
    {
        var mesh = MeshBuilder.CreateBox(5, 5, 5);
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");
        var mtlPath = Path.ChangeExtension(path, ".mtl");
        try
        {
            OBJExporter.ExportWithMaterial(mesh, path, "#FF5500");
            Assert.True(File.Exists(path));
            Assert.True(File.Exists(mtlPath));
            var mtlText = File.ReadAllText(mtlPath);
            Assert.Contains("newmtl", mtlText);
            Assert.Contains("Kd", mtlText);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(mtlPath)) File.Delete(mtlPath);
        }
    }

    // ── Full Pipeline ────────────────────────────────────────────────────

    [Fact]
    public void FullPipeline_SolidToStlBytes()
    {
        // End-to-end: Solid → Tessellate → STL binary bytes
        var mesher = new SolidMesher();
        var solid = new BoxSolid(15, 10, 5) { Name = "TestBox" };
        var mesh = mesher.Tessellate(solid);
        using var stream = new MemoryStream();
        STLExporter.ExportToStream(mesh, stream, binary: true);
        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 84);
        var triCount = BitConverter.ToUInt32(bytes, 80);
        Assert.True(triCount > 0);
    }

    [Fact]
    public void FullPipeline_ExtrudeToObj()
    {
        // End-to-end: Profile → ExtrudeSolid → Tessellate → OBJ
        var mesher = new SolidMesher();
        var profile = new Profile(new List<Vector2D>
        {
            new(-10, -10), new(10, -10), new(10, 10), new(-10, 10)
        });
        var extrude = new ExtrudeSolid(profile, 25);
        var mesh = mesher.Tessellate(extrude);
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");
        try
        {
            OBJExporter.Export(mesh, path);
            var text = File.ReadAllText(path);
            Assert.Contains("v ", text);
            Assert.Contains("vn ", text);
            Assert.Contains("f ", text);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ── Degenerate mesh handling ─────────────────────────────────────────

    [Fact]
    public void StlExport_EmptyMesh_DoesNotCrash()
    {
        var mesh = new Mesh();
        using var stream = new MemoryStream();
        STLExporter.ExportToStream(mesh, stream, binary: true);
        var bytes = stream.ToArray();
        // Should produce a valid header + 0 triangles
        Assert.True(bytes.Length >= 84);
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, 80));
    }

    [Fact]
    public void ObjExport_EmptyMesh_DoesNotCrash()
    {
        var mesh = new Mesh();
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.obj");
        try
        {
            OBJExporter.Export(mesh, path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
