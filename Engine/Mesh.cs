using System.IO;
using System.Numerics;

namespace My3DApp.Engine;

public readonly record struct Triangle(Vector3 Normal, Vector3 V0, Vector3 V1, Vector3 V2);

public class Mesh
{
    public List<Triangle> Triangles { get; } = new();

    // ── Primitive factories ───────────────────────────────────────────────────

    public static Mesh Box(float w, float h, float d)
    {
        var mesh = new Mesh();
        float hw = w / 2, hh = h / 2, hd = d / 2;

        // 6 faces × 2 triangles each
        // +X face
        mesh.AddQuad(new(hw, -hh, -hd), new(hw,  hh, -hd),
                     new(hw,  hh,  hd), new(hw, -hh,  hd));
        // -X face
        mesh.AddQuad(new(-hw, -hh,  hd), new(-hw,  hh,  hd),
                     new(-hw,  hh, -hd), new(-hw, -hh, -hd));
        // +Y face
        mesh.AddQuad(new(-hw, hh, -hd), new(-hw, hh,  hd),
                     new( hw, hh,  hd), new( hw, hh, -hd));
        // -Y face
        mesh.AddQuad(new(-hw, -hh,  hd), new(-hw, -hh, -hd),
                     new( hw, -hh, -hd), new( hw, -hh,  hd));
        // +Z face
        mesh.AddQuad(new(-hw, -hh, hd), new( hw, -hh, hd),
                     new( hw,  hh, hd), new(-hw,  hh, hd));
        // -Z face
        mesh.AddQuad(new( hw, -hh, -hd), new(-hw, -hh, -hd),
                     new(-hw,  hh, -hd), new( hw,  hh, -hd));
        return mesh;
    }

    public static Mesh Cylinder(float radius, float height, int segments = 32)
    {
        var mesh = new Mesh();
        float hh = height / 2;
        var step = MathF.Tau / segments;

        for (int i = 0; i < segments; i++)
        {
            float a0 = i * step, a1 = (i + 1) * step;
            float x0 = MathF.Cos(a0) * radius, z0 = MathF.Sin(a0) * radius;
            float x1 = MathF.Cos(a1) * radius, z1 = MathF.Sin(a1) * radius;

            // Side quad
            var bl = new Vector3(x0, -hh, z0);
            var br = new Vector3(x1, -hh, z1);
            var tr = new Vector3(x1,  hh, z1);
            var tl = new Vector3(x0,  hh, z0);
            mesh.AddTri(bl, tr, br);
            mesh.AddTri(bl, tl, tr);

            // Top cap
            mesh.AddTri(new(0, hh, 0), new(x0, hh, z0), new(x1, hh, z1));
            // Bottom cap
            mesh.AddTri(new(0, -hh, 0), new(x1, -hh, z1), new(x0, -hh, z0));
        }
        return mesh;
    }

    public static Mesh Sphere(float radius, int stacks = 16, int slices = 32)
    {
        var mesh = new Mesh();
        for (int st = 0; st < stacks; st++)
        {
            float phi0 = MathF.PI * st / stacks - MathF.PI / 2;
            float phi1 = MathF.PI * (st + 1) / stacks - MathF.PI / 2;

            for (int sl = 0; sl < slices; sl++)
            {
                float th0 = MathF.Tau * sl / slices;
                float th1 = MathF.Tau * (sl + 1) / slices;

                Vector3 P(float phi, float theta) => new(
                    radius * MathF.Cos(phi) * MathF.Cos(theta),
                    radius * MathF.Sin(phi),
                    radius * MathF.Cos(phi) * MathF.Sin(theta));

                var v00 = P(phi0, th0); var v10 = P(phi1, th0);
                var v01 = P(phi0, th1); var v11 = P(phi1, th1);

                if (st > 0)        mesh.AddTri(v00, v10, v11);
                if (st < stacks-1) mesh.AddTri(v00, v11, v01);
            }
        }
        return mesh;
    }

    // ── STL I/O ───────────────────────────────────────────────────────────────

    public void WriteBinaryStl(Stream output)
    {
        using var bw = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);
        bw.Write(new byte[80]); // header
        bw.Write((uint)Triangles.Count);
        foreach (var t in Triangles)
        {
            Write(bw, t.Normal);
            Write(bw, t.V0);
            Write(bw, t.V1);
            Write(bw, t.V2);
            bw.Write((ushort)0); // attribute byte count
        }
    }

    public static Mesh ReadBinaryStl(Stream input)
    {
        var mesh = new Mesh();
        using var br = new BinaryReader(input, System.Text.Encoding.ASCII, leaveOpen: true);
        br.ReadBytes(80); // skip header
        uint count = br.ReadUInt32();
        for (uint i = 0; i < count; i++)
        {
            var n  = ReadVec(br);
            var v0 = ReadVec(br);
            var v1 = ReadVec(br);
            var v2 = ReadVec(br);
            br.ReadUInt16(); // attribute
            mesh.Triangles.Add(new Triangle(n, v0, v1, v2));
        }
        return mesh;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddTri(Vector3 a, Vector3 b, Vector3 c)
    {
        var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        Triangles.Add(new Triangle(n, a, b, c));
    }

    private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        AddTri(a, b, c);
        AddTri(a, c, d);
    }

    private static void Write(BinaryWriter bw, Vector3 v)
    {
        bw.Write(v.X); bw.Write(v.Y); bw.Write(v.Z);
    }

    private static Vector3 ReadVec(BinaryReader br) =>
        new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
}
