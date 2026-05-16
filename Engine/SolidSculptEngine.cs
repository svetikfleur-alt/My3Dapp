using FormaCore.Core;

namespace FormaCore.Engine;

/// <summary>
/// Organic mesh sculpting operations applied directly to triangle meshes.
/// All operations work in-place on a Mesh clone so callers can undo safely.
/// </summary>
public static class SolidSculptEngine
{
    // ── Topology ──────────────────────────────────────────────────────────────

    /// <summary>Loop subdivision (Catmull-Clark-like midpoint split): each triangle → 4 triangles.</summary>
    public static Mesh Subdivide(Mesh mesh, int iterations = 1)
    {
        var m = CloneMesh(mesh);
        for (var iter = 0; iter < iterations; iter++)
            m = SubdivideOnce(m);
        return m;
    }

    private static Mesh SubdivideOnce(Mesh src)
    {
        var dst = new Mesh();
        dst.Vertices.AddRange(src.Vertices);

        var edgeMidpoints = new Dictionary<(int, int), int>();

        int GetOrAddMidpoint(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (edgeMidpoints.TryGetValue(key, out var idx)) return idx;
            idx = dst.Vertices.Count;
            var va = src.Vertices[a]; var vb = src.Vertices[b];
            dst.Vertices.Add(new Vertex((va.X + vb.X) * 0.5, (va.Y + vb.Y) * 0.5, (va.Z + vb.Z) * 0.5));
            edgeMidpoints[key] = idx;
            return idx;
        }

        foreach (var tri in src.Triangles)
        {
            int mAB = GetOrAddMidpoint(tri.A, tri.B);
            int mBC = GetOrAddMidpoint(tri.B, tri.C);
            int mCA = GetOrAddMidpoint(tri.C, tri.A);
            dst.Triangles.Add(new Triangle(tri.A, mAB, mCA));
            dst.Triangles.Add(new Triangle(tri.B, mBC, mAB));
            dst.Triangles.Add(new Triangle(tri.C, mCA, mBC));
            dst.Triangles.Add(new Triangle(mAB, mBC, mCA));
        }
        return dst;
    }

    // ── Smoothing ─────────────────────────────────────────────────────────────

    /// <summary>Laplacian smoothing: move each vertex toward the average of its neighbours.</summary>
    public static Mesh Smooth(Mesh mesh, int iterations = 1, double strength = 0.5)
    {
        strength = Math.Clamp(strength, 0.0, 1.0);
        var m = CloneMesh(mesh);

        var neighbors = BuildAdjacency(m);

        for (var iter = 0; iter < iterations; iter++)
        {
            var newVerts = new Vertex[m.Vertices.Count];
            for (var i = 0; i < m.Vertices.Count; i++)
            {
                var nbs = neighbors[i];
                if (nbs.Count == 0) { newVerts[i] = m.Vertices[i]; continue; }
                double ax = 0, ay = 0, az = 0;
                foreach (var nb in nbs) { ax += m.Vertices[nb].X; ay += m.Vertices[nb].Y; az += m.Vertices[nb].Z; }
                ax /= nbs.Count; ay /= nbs.Count; az /= nbs.Count;
                var v = m.Vertices[i];
                newVerts[i] = new Vertex(
                    v.X + (ax - v.X) * strength,
                    v.Y + (ay - v.Y) * strength,
                    v.Z + (az - v.Z) * strength);
            }
            for (var i = 0; i < m.Vertices.Count; i++) m.Vertices[i] = newVerts[i];
        }
        return m;
    }

    // ── Displacement ──────────────────────────────────────────────────────────

    /// <summary>Push vertices along their computed normal by <paramref name="amount"/>.</summary>
    public static Mesh Inflate(Mesh mesh, double amount)
    {
        var m = CloneMesh(mesh);
        var normals = ComputeVertexNormals(m);
        for (var i = 0; i < m.Vertices.Count; i++)
        {
            var v = m.Vertices[i]; var n = normals[i];
            m.Vertices[i] = new Vertex(v.X + n.X * amount, v.Y + n.Y * amount, v.Z + n.Z * amount);
        }
        return m;
    }

    /// <summary>Add coherent Perlin-like noise displacement along vertex normals.</summary>
    public static Mesh Noise(Mesh mesh, double scale = 0.1, double strength = 5.0, int seed = 42)
    {
        var m = CloneMesh(mesh);
        var normals = ComputeVertexNormals(m);
        var rng = new Random(seed);
        // Simple value-noise using hash of quantized position
        for (var i = 0; i < m.Vertices.Count; i++)
        {
            var v = m.Vertices[i]; var n = normals[i];
            double nx = ValueNoise(v.X * scale, v.Y * scale, v.Z * scale, seed);
            m.Vertices[i] = new Vertex(v.X + n.X * nx * strength, v.Y + n.Y * nx * strength, v.Z + n.Z * nx * strength);
        }
        return m;
    }

    /// <summary>Pull vertices toward or away from the mesh centroid (pinch inward, expand outward).</summary>
    public static Mesh Pinch(Mesh mesh, double strength = 0.1, double falloffRadius = 50.0)
    {
        var m = CloneMesh(mesh);
        var (cx, cy, cz) = Centroid(m);
        for (var i = 0; i < m.Vertices.Count; i++)
        {
            var v = m.Vertices[i];
            double dx = cx - v.X, dy = cy - v.Y, dz = cz - v.Z;
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double falloff = Math.Max(0, 1.0 - dist / falloffRadius);
            m.Vertices[i] = new Vertex(
                v.X + dx * strength * falloff,
                v.Y + dy * strength * falloff,
                v.Z + dz * strength * falloff);
        }
        return m;
    }

    // ── Deformations ──────────────────────────────────────────────────────────

    /// <summary>Twist the mesh around the Z axis — upper vertices rotate more.</summary>
    public static Mesh Twist(Mesh mesh, double maxAngleDegrees)
    {
        var m = CloneMesh(mesh);
        double minZ = double.MaxValue, maxZ = double.MinValue;
        foreach (var v in m.Vertices) { minZ = Math.Min(minZ, v.Z); maxZ = Math.Max(maxZ, v.Z); }
        double range = maxZ - minZ;
        if (range < 1e-9) return m;
        double maxRad = maxAngleDegrees * Math.PI / 180.0;
        for (var i = 0; i < m.Vertices.Count; i++)
        {
            var v = m.Vertices[i];
            double t = (v.Z - minZ) / range;
            double angle = t * maxRad;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            m.Vertices[i] = new Vertex(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos, v.Z);
        }
        return m;
    }

    /// <summary>Bend the mesh along the X axis toward Y.</summary>
    public static Mesh Bend(Mesh mesh, double angleDegrees)
    {
        var m = CloneMesh(mesh);
        double minX = double.MaxValue, maxX = double.MinValue;
        foreach (var v in m.Vertices) { minX = Math.Min(minX, v.X); maxX = Math.Max(maxX, v.X); }
        double range = maxX - minX;
        if (range < 1e-9) return m;
        double totalRad = angleDegrees * Math.PI / 180.0;
        double radius = range / (Math.Abs(totalRad) < 1e-9 ? 1e-9 : totalRad);
        for (var i = 0; i < m.Vertices.Count; i++)
        {
            var v = m.Vertices[i];
            double t = (v.X - minX) / range;
            double angle = t * totalRad;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            // arc bending: x→arc, z shifts by radius*(1-cos)
            m.Vertices[i] = new Vertex(
                radius * sin,
                v.Y,
                v.Z - radius * (1 - cos));
        }
        return m;
    }

    /// <summary>Taper — scale XY plane uniformly based on Z height.</summary>
    public static Mesh Taper(Mesh mesh, double topScale, double bottomScale = 1.0)
    {
        var m = CloneMesh(mesh);
        double minZ = double.MaxValue, maxZ = double.MinValue;
        foreach (var v in m.Vertices) { minZ = Math.Min(minZ, v.Z); maxZ = Math.Max(maxZ, v.Z); }
        double range = maxZ - minZ;
        if (range < 1e-9) return m;
        for (var i = 0; i < m.Vertices.Count; i++)
        {
            var v = m.Vertices[i];
            double t = (v.Z - minZ) / range;
            double scale = bottomScale + t * (topScale - bottomScale);
            m.Vertices[i] = new Vertex(v.X * scale, v.Y * scale, v.Z);
        }
        return m;
    }

    /// <summary>Spherize — blend vertices toward a sphere of given radius.</summary>
    public static Mesh Spherize(Mesh mesh, double strength = 0.5, double radius = -1)
    {
        var m = CloneMesh(mesh);
        var (cx, cy, cz) = Centroid(m);
        if (radius < 0)
        {
            // auto radius: average distance from centroid
            double sum = 0;
            foreach (var v in m.Vertices)
            {
                double dx = v.X - cx, dy = v.Y - cy, dz = v.Z - cz;
                sum += Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            radius = sum / Math.Max(1, m.Vertices.Count);
        }
        for (var i = 0; i < m.Vertices.Count; i++)
        {
            var v = m.Vertices[i];
            double dx = v.X - cx, dy = v.Y - cy, dz = v.Z - cz;
            double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-9) continue;
            double tx = cx + dx / len * radius;
            double ty = cy + dy / len * radius;
            double tz = cz + dz / len * radius;
            m.Vertices[i] = new Vertex(
                v.X + (tx - v.X) * strength,
                v.Y + (ty - v.Y) * strength,
                v.Z + (tz - v.Z) * strength);
        }
        return m;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Mesh CloneMesh(Mesh src)
    {
        var dst = new Mesh();
        dst.Vertices.AddRange(src.Vertices);
        dst.Triangles.AddRange(src.Triangles);
        return dst;
    }

    private static List<List<int>> BuildAdjacency(Mesh mesh)
    {
        var adj = new List<List<int>>(mesh.Vertices.Count);
        for (var i = 0; i < mesh.Vertices.Count; i++) adj.Add([]);
        foreach (var tri in mesh.Triangles)
        {
            void Add(int a, int b) { if (!adj[a].Contains(b)) adj[a].Add(b); }
            Add(tri.A, tri.B); Add(tri.A, tri.C);
            Add(tri.B, tri.A); Add(tri.B, tri.C);
            Add(tri.C, tri.A); Add(tri.C, tri.B);
        }
        return adj;
    }

    private static Vertex[] ComputeVertexNormals(Mesh mesh)
    {
        var normals = new double[mesh.Vertices.Count, 3];
        foreach (var tri in mesh.Triangles)
        {
            var va = mesh.Vertices[tri.A]; var vb = mesh.Vertices[tri.B]; var vc = mesh.Vertices[tri.C];
            double ax = vb.X - va.X, ay = vb.Y - va.Y, az = vb.Z - va.Z;
            double bx = vc.X - va.X, by = vc.Y - va.Y, bz = vc.Z - va.Z;
            double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
            foreach (var idx in new[] { tri.A, tri.B, tri.C })
            { normals[idx, 0] += nx; normals[idx, 1] += ny; normals[idx, 2] += nz; }
        }
        var result = new Vertex[mesh.Vertices.Count];
        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            double nx = normals[i, 0], ny = normals[i, 1], nz = normals[i, 2];
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            result[i] = len < 1e-9 ? new Vertex(0, 1, 0) : new Vertex(nx / len, ny / len, nz / len);
        }
        return result;
    }

    private static (double, double, double) Centroid(Mesh mesh)
    {
        if (mesh.Vertices.Count == 0) return (0, 0, 0);
        double sx = 0, sy = 0, sz = 0;
        foreach (var v in mesh.Vertices) { sx += v.X; sy += v.Y; sz += v.Z; }
        int n = mesh.Vertices.Count;
        return (sx / n, sy / n, sz / n);
    }

    private static double ValueNoise(double x, double y, double z, int seed)
    {
        // Hash-based pseudo-random noise in [-1,1]
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
        double fx = x - ix, fy = y - iy, fz = z - iz;
        // Smooth interpolation
        double ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy), uz = fz * fz * (3 - 2 * fz);
        double v000 = Hash(ix, iy, iz, seed), v100 = Hash(ix + 1, iy, iz, seed);
        double v010 = Hash(ix, iy + 1, iz, seed), v110 = Hash(ix + 1, iy + 1, iz, seed);
        double v001 = Hash(ix, iy, iz + 1, seed), v101 = Hash(ix + 1, iy, iz + 1, seed);
        double v011 = Hash(ix, iy + 1, iz + 1, seed), v111 = Hash(ix + 1, iy + 1, iz + 1, seed);
        return Lerp(Lerp(Lerp(v000, v100, ux), Lerp(v010, v110, ux), uy),
                    Lerp(Lerp(v001, v101, ux), Lerp(v011, v111, ux), uy), uz);
    }

    private static double Hash(int x, int y, int z, int seed)
    {
        uint h = (uint)(x * 1619 + y * 31337 + z * 6271 + seed * 1013904223);
        h = h ^ (h >> 16); h *= 0x45d9f3b; h = h ^ (h >> 16);
        return (h & 0xFFFF) / 32767.5 - 1.0;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
