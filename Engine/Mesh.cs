using System.IO;
using System.Linq;
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

    // Revolves a 2D profile around the Y axis. profile = [r0,y0, r1,y1, ...] in mm.
    public static Mesh RevolveProfile(float[] profile, float angleDeg = 360f, int segments = 32)
    {
        var mesh = new Mesh();
        int n = profile.Length / 2;
        if (n < 2) return mesh;
        float angleRad = angleDeg * MathF.PI / 180f;
        float step = angleRad / segments;
        for (int si = 0; si < segments; si++)
        {
            float a0 = si * step, a1 = (si + 1) * step;
            float c0 = MathF.Cos(a0), s0 = MathF.Sin(a0);
            float c1 = MathF.Cos(a1), s1 = MathF.Sin(a1);
            for (int pi = 0; pi < n - 1; pi++)
            {
                float r0 = profile[pi * 2],       y0 = profile[pi * 2 + 1];
                float r1 = profile[(pi + 1) * 2], y1 = profile[(pi + 1) * 2 + 1];
                var p00 = new Vector3(r0 * c0, y0, r0 * s0);
                var p01 = new Vector3(r0 * c1, y0, r0 * s1);
                var p10 = new Vector3(r1 * c0, y1, r1 * s0);
                var p11 = new Vector3(r1 * c1, y1, r1 * s1);
                if ((p00 - p10).LengthSquared() > 1e-6f) { mesh.AddTri(p00, p10, p11); mesh.AddTri(p00, p11, p01); }
            }
        }
        return mesh;
    }

    // Extrudes a closed 2D polygon (points = [x0,z0, x1,z1, ...] on XZ plane) by depth along Y.
    // Solid is centred: bottom at y = -depth/2, top at y = +depth/2.
    public static Mesh ExtrudePolygon(float[] pts, float depth)
    {
        var mesh = new Mesh();
        int n = pts.Length / 2;
        if (n < 3) return mesh;
        float hy = depth / 2f;
        var bot = new Vector3[n];
        var top = new Vector3[n];
        for (int i = 0; i < n; i++) { bot[i] = new Vector3(pts[i*2], -hy, pts[i*2+1]); top[i] = new Vector3(pts[i*2], hy, pts[i*2+1]); }
        // Side quads
        for (int i = 0; i < n; i++) { int j = (i+1)%n; mesh.AddTri(bot[i], bot[j], top[j]); mesh.AddTri(bot[i], top[j], top[i]); }
        // Caps via ear-clipping
        foreach (var (a,b,c) in TriangulateEarClip(pts)) { mesh.AddTri(bot[a], bot[c], bot[b]); mesh.AddTri(top[a], top[b], top[c]); }
        return mesh;
    }

    private static List<(int, int, int)> TriangulateEarClip(float[] pts)
    {
        var result = new List<(int, int, int)>();
        int n = pts.Length / 2;
        var idx = Enumerable.Range(0, n).ToList();
        static float Cross2D(float[] p, int a, int b, int c) {
            float ax = p[b*2]-p[a*2], ay = p[b*2+1]-p[a*2+1];
            float bx = p[c*2]-p[a*2], by = p[c*2+1]-p[a*2+1];
            return ax*by - ay*bx;
        }
        static bool InTri(float[] p, int a, int b, int c, int t) {
            float px = p[t*2], py = p[t*2+1];
            bool d1 = (p[b*2]-p[a*2])*(py-p[a*2+1]) - (p[b*2+1]-p[a*2+1])*(px-p[a*2]) < 0;
            bool d2 = (p[c*2]-p[b*2])*(py-p[b*2+1]) - (p[c*2+1]-p[b*2+1])*(px-p[b*2]) < 0;
            bool d3 = (p[a*2]-p[c*2])*(py-p[c*2+1]) - (p[a*2+1]-p[c*2+1])*(px-p[c*2]) < 0;
            return d1==d2 && d2==d3;
        }
        while (idx.Count > 3)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count && !clipped; i++)
            {
                int a = idx[(i-1+idx.Count)%idx.Count], b = idx[i], c = idx[(i+1)%idx.Count];
                if (Cross2D(pts, a, b, c) <= 0) continue;
                bool ear = true;
                for (int j = 0; j < idx.Count && ear; j++) { int p = idx[j]; if (p!=a&&p!=b&&p!=c&&InTri(pts,a,b,c,p)) ear=false; }
                if (ear) { result.Add((a, b, c)); idx.RemoveAt(i); clipped = true; }
            }
            if (!clipped) break;
        }
        if (idx.Count == 3) result.Add((idx[0], idx[1], idx[2]));
        return result;
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

    // Returns a new Mesh with every vertex offset by (dx, dy, dz).
    public Mesh Translated(float dx, float dy, float dz)
    {
        var offset = new Vector3(dx, dy, dz);
        var m = new Mesh();
        foreach (var t in Triangles)
            m.Triangles.Add(new Triangle(t.Normal, t.V0 + offset, t.V1 + offset, t.V2 + offset));
        return m;
    }

    private static void Write(BinaryWriter bw, Vector3 v)
    {
        bw.Write(v.X); bw.Write(v.Y); bw.Write(v.Z);
    }

    private static Vector3 ReadVec(BinaryReader br) =>
        new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
}
