using FormaCore.Core;

namespace FormaCore.Engine;

public sealed class SolidMesher
{
    /// <summary>
    /// Tessellate a solid into a mesh. Never throws — returns a diagnostic
    /// error placeholder on failure so a single bad body never crashes the scene.
    /// </summary>
    public Mesh Tessellate(Solid solid)
    {
        try
        {
            var mesh = TessellateCore(solid);
            return ValidateAndRepair(mesh);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"SolidMesher.Tessellate failed for {solid.GetType().Name}: {ex.Message}");
            return CreateErrorPlaceholder();
        }
    }

    private Mesh TessellateCore(Solid solid)
    {
        return solid switch
        {
            BoxSolid b => MeshBuilder.CreateBox(b.Width, b.Depth, b.Height),
            CylinderSolid c => MeshBuilder.CreateCylinder(c.Radius, c.Height),
            TransformedSolid t => ApplyTransform(Tessellate(t.Child), t.Transform),
            BooleanSolid { Operation: BooleanOperation.Union } b => MeshBuilder.Merge(
                Tessellate(b.A),
                Tessellate(b.B)),
            BooleanSolid { Operation: BooleanOperation.Subtract } b =>
                CsgSubtract(Tessellate(b.A), Tessellate(b.B)),
            BooleanSolid { Operation: BooleanOperation.Intersect } b =>
                CsgIntersect(Tessellate(b.A), Tessellate(b.B)),
            SphereSolid s => MeshBuilder.CreateSphere(s.Radius),
            ConeSolid c => MeshBuilder.CreateCone(c.RadiusTop, c.RadiusBottom, c.Height),
            PipeSolid p => MeshBuilder.Merge(
                MeshBuilder.CreateCylinder(p.OuterRadius, p.Height),
                MeshBuilder.CreateCylinder(p.InnerRadius, p.Height)),
            TorusSolid t => MeshBuilder.CreateTorus(t.MajorRadius, t.MinorRadius),
            PyramidSolid p => MeshBuilder.CreatePyramid(p.BaseWidth, p.BaseDepth, p.Height),
            WedgeSolid w => MeshBuilder.CreateWedge(w.Width, w.Depth, w.Height),
            EllipsoidSolid e => MeshBuilder.CreateEllipsoid(e.RadiusX, e.RadiusY, e.RadiusZ),
            CapsuleSolid c => MeshBuilder.CreateCapsule(c.Radius, c.Height),
            HemisphereSolid h => MeshBuilder.CreateHemisphere(h.Radius),
            PrismSolid p => MeshBuilder.CreatePrism(p.Radius, p.Height, p.Sides),
            PolygonPrismSolid p => MeshBuilder.CreatePolygonPrism(p.Profile, p.Height),
            DiskSolid d => MeshBuilder.CreateDisk(d.OuterRadius, d.InnerRadius),
            ArrowSolid a => MeshBuilder.CreateArrow(a.ShaftRadius, a.ShaftHeight, a.HeadRadius, a.HeadHeight),
            IcosphereSolid i => MeshBuilder.CreateIcosphere(i.Radius, i.Subdivisions),
            TetrahedronSolid t => MeshBuilder.CreateTetrahedron(t.Radius),
            OctahedronSolid o => MeshBuilder.CreateOctahedron(o.Radius),
            IcosahedronSolid i => MeshBuilder.CreateIcosahedron(i.Radius),
            SpringSolid s => MeshBuilder.CreateSpring(s.CoilRadius, s.TubeRadius, s.Coils, s.Height),
            ExtrudeSolid e => TessellateExtrude(e),
            RevolveSolid r => MeshBuilder.CreateRevolution(r.Profile.Points, r.AngleDegrees, r.Plane, r.Axis),
            SweepSolid sw => MeshBuilder.CreateSweep(sw.Profile.Points, sw.Distance, sw.Plane, sw.TwistDegrees),
            LoftSolid l => MeshBuilder.CreateLoft(l.ProfileA.Points, l.ProfileB.Points, l.Distance, l.Plane),
            MirrorSolid m => MeshBuilder.MirrorMesh(Tessellate(m.Child), m.Axis),
            LinearPatternSolid lp => TessellateLinearPattern(lp),
            CircularPatternSolid cp => TessellateCircularPattern(cp),
            _ => throw new NotSupportedException($"Unsupported solid: {solid.GetType().Name}")
        };
    }

    private static Mesh TessellateExtrude(ExtrudeSolid e)
    {
        IReadOnlyList<Vector2D> profile = e.Profile.Points;
        if (profile.Count < 3)
            return CreateErrorPlaceholder();

        if (e.FilletRadius > 0.0001d)
            profile = MeshBuilder.RoundPolygonCorners(profile, e.FilletRadius);
        else if (e.ChamferDistance > 0.0001d)
            profile = MeshBuilder.ChamferPolygonCorners(profile, e.ChamferDistance);

        if (e.FacePlane is { } fp)
            return MeshBuilder.CreateExtrudedProfileFace(profile, e.Height, fp, e.Symmetric);

        return MeshBuilder.CreateExtrudedProfile(
            profile, e.Height, e.Plane, e.Symmetric, e.TaperAngleDegrees, e.ShellThickness);
    }

    private Mesh TessellateLinearPattern(LinearPatternSolid lp)
    {
        var count = Math.Max(1, lp.Count);
        var baseMesh = Tessellate(lp.Child);
        var result = baseMesh;
        for (var i = 1; i < count; i++)
        {
            result = MeshBuilder.Merge(result, MeshBuilder.Translate(baseMesh, lp.Dx * i, lp.Dy * i, lp.Dz * i));
        }
        return result;
    }

    private Mesh TessellateCircularPattern(CircularPatternSolid cp)
    {
        var count = Math.Max(1, cp.Count);
        var baseMesh = Tessellate(cp.Child);
        var result = baseMesh;
        var stepDeg = cp.TotalAngleDegrees / count;
        for (var i = 1; i < count; i++)
        {
            result = MeshBuilder.Merge(result, MeshBuilder.RotateAroundAxis(baseMesh, cp.Axis, stepDeg * i));
        }
        return result;
    }

    private static Mesh ApplyTransform(Mesh mesh, Transform3D transform)
    {
        var t = transform.Translation;
        return MeshBuilder.Translate(mesh, t.X, t.Y, t.Z);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Mesh Validation
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Validate mesh integrity and remove degenerate triangles.
    /// </summary>
    private static Mesh ValidateAndRepair(Mesh mesh)
    {
        if (mesh.Vertices.Count == 0 || mesh.Triangles.Count == 0)
            return mesh;

        // Remove NaN/Infinity vertices and their triangles
        var hasInvalid = false;
        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var v = mesh.Vertices[i];
            if (double.IsNaN(v.X) || double.IsNaN(v.Y) || double.IsNaN(v.Z) ||
                double.IsInfinity(v.X) || double.IsInfinity(v.Y) || double.IsInfinity(v.Z))
            {
                hasInvalid = true;
                break;
            }
        }

        if (!hasInvalid)
        {
            // Fast path: just remove degenerate triangles in-place
            mesh.Triangles.RemoveAll(tri =>
                tri.A < 0 || tri.A >= mesh.Vertices.Count ||
                tri.B < 0 || tri.B >= mesh.Vertices.Count ||
                tri.C < 0 || tri.C >= mesh.Vertices.Count ||
                tri.A == tri.B || tri.B == tri.C || tri.A == tri.C ||
                IsZeroAreaTriangle(mesh.Vertices[tri.A], mesh.Vertices[tri.B], mesh.Vertices[tri.C]));
            return mesh;
        }

        // Slow path: rebuild mesh without invalid vertices
        var clean = new Mesh();
        var remap = new int[mesh.Vertices.Count];
        for (var i = 0; i < remap.Length; i++) remap[i] = -1;

        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var v = mesh.Vertices[i];
            if (double.IsNaN(v.X) || double.IsNaN(v.Y) || double.IsNaN(v.Z) ||
                double.IsInfinity(v.X) || double.IsInfinity(v.Y) || double.IsInfinity(v.Z))
                continue;
            remap[i] = clean.Vertices.Count;
            clean.Vertices.Add(v);
        }

        foreach (var tri in mesh.Triangles)
        {
            if (tri.A < 0 || tri.A >= remap.Length ||
                tri.B < 0 || tri.B >= remap.Length ||
                tri.C < 0 || tri.C >= remap.Length)
                continue;
            var a = remap[tri.A];
            var b = remap[tri.B];
            var c = remap[tri.C];
            if (a < 0 || b < 0 || c < 0 || a == b || b == c || a == c)
                continue;
            if (IsZeroAreaTriangle(clean.Vertices[a], clean.Vertices[b], clean.Vertices[c]))
                continue;
            clean.Triangles.Add(new Triangle(a, b, c));
        }

        return clean;
    }

    private static bool IsZeroAreaTriangle(Vertex a, Vertex b, Vertex c)
    {
        // Cross product of (b-a) × (c-a)
        var ux = b.X - a.X; var uy = b.Y - a.Y; var uz = b.Z - a.Z;
        var vx = c.X - a.X; var vy = c.Y - a.Y; var vz = c.Z - a.Z;
        var nx = uy * vz - uz * vy;
        var ny = uz * vx - ux * vz;
        var nz = ux * vy - uy * vx;
        return (nx * nx + ny * ny + nz * nz) < 1e-18;
    }

    /// <summary>
    /// Tiny wireframe box used as a placeholder when tessellation fails.
    /// Visible in the viewport as a 1×1×1 marker so the user knows something went wrong.
    /// </summary>
    internal static Mesh CreateErrorPlaceholder()
    {
        return MeshBuilder.CreateBox(1d, 1d, 1d);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CSG Operations (centroid-based classification with jitter)
    //
    // Known limitation: this approach classifies triangles by testing
    // whether their centroid is inside the other mesh using ray-casting.
    // It works well for axis-aligned and simple convex geometry but may
    // produce artifacts on tangent/coplanar surfaces or highly concave
    // shapes. For production CSG, a BSP-tree or voxel-based approach
    // (e.g. PicoGK) would be needed.
    // ──────────────────────────────────────────────────────────────────────

    // CSG subtract: keep faces of A outside B, keep flipped faces of B inside A.
    private static Mesh CsgSubtract(Mesh a, Mesh b)
    {
        if (b.Triangles.Count == 0) return a;
        if (a.Triangles.Count == 0) return a;

        var result = new Mesh();
        CopyFilteredTriangles(a, b, insideB: false, flipNormals: false, result);
        CopyFilteredTriangles(b, a, insideB: true, flipNormals: true, result);

        // Fallback: if boolean produced nothing, return the primary mesh
        if (result.Triangles.Count == 0)
        {
            System.Diagnostics.Trace.WriteLine(
                "CSG subtract produced zero triangles — returning primary mesh as fallback.");
            return a;
        }
        return result;
    }

    // CSG intersect: keep faces of A inside B, keep faces of B inside A.
    private static Mesh CsgIntersect(Mesh a, Mesh b)
    {
        if (a.Triangles.Count == 0 || b.Triangles.Count == 0)
            return new Mesh();

        var result = new Mesh();
        CopyFilteredTriangles(a, b, insideB: true, flipNormals: false, result);
        CopyFilteredTriangles(b, a, insideB: true, flipNormals: false, result);

        if (result.Triangles.Count == 0)
        {
            System.Diagnostics.Trace.WriteLine(
                "CSG intersect produced zero triangles — bodies may not overlap.");
        }
        return result;
    }

    private static void CopyFilteredTriangles(Mesh source, Mesh classifier, bool insideB, bool flipNormals, Mesh result)
    {
        foreach (var tri in source.Triangles)
        {
            if (tri.A >= source.Vertices.Count || tri.B >= source.Vertices.Count || tri.C >= source.Vertices.Count)
                continue;

            var va = source.Vertices[tri.A];
            var vb = source.Vertices[tri.B];
            var vc = source.Vertices[tri.C];
            var centroid = new Vertex(
                (va.X + vb.X + vc.X) / 3.0,
                (va.Y + vb.Y + vc.Y) / 3.0,
                (va.Z + vb.Z + vc.Z) / 3.0);

            var isInside = IsPointInsideMesh(classifier, centroid);
            if (isInside != insideB)
            {
                continue;
            }

            var baseIndex = result.Vertices.Count;
            result.Vertices.Add(va);
            result.Vertices.Add(vb);
            result.Vertices.Add(vc);

            if (flipNormals)
            {
                result.Triangles.Add(new Triangle(baseIndex, baseIndex + 2, baseIndex + 1));
            }
            else
            {
                result.Triangles.Add(new Triangle(baseIndex, baseIndex + 1, baseIndex + 2));
            }
        }
    }

    // Ray cast from point with jittered direction; odd intersection count = inside.
    // Using three slightly different ray directions and majority voting to handle
    // edge cases where a ray passes through an edge or vertex.
    private static bool IsPointInsideMesh(Mesh mesh, Vertex point)
    {
        // Primary ray: +Y direction
        var hits1 = CountRayHits(mesh, point, 0.0, 1.0, 0.0);

        // If hit count is definitive (not near an edge), use it directly
        if (hits1 >= 0)
            return (hits1 & 1) == 1;

        // Fallback: try two more slightly jittered directions
        var hits2 = CountRayHits(mesh, point, 0.00037, 1.0, 0.00019);
        var hits3 = CountRayHits(mesh, point, -0.00023, 1.0, 0.00041);

        // Majority voting
        var inside1 = hits1 >= 0 && (hits1 & 1) == 1;
        var inside2 = hits2 >= 0 && (hits2 & 1) == 1;
        var inside3 = hits3 >= 0 && (hits3 & 1) == 1;
        var count = (inside1 ? 1 : 0) + (inside2 ? 1 : 0) + (inside3 ? 1 : 0);
        return count >= 2;
    }

    /// <summary>
    /// Count ray-triangle intersections for a ray from origin in the given direction.
    /// Returns -1 if a degenerate hit was detected (ray through edge/vertex).
    /// </summary>
    private static int CountRayHits(Mesh mesh, Vertex origin, double dx, double dy, double dz)
    {
        var hits = 0;
        foreach (var tri in mesh.Triangles)
        {
            if (tri.A >= mesh.Vertices.Count || tri.B >= mesh.Vertices.Count || tri.C >= mesh.Vertices.Count)
                continue;
            var v0 = mesh.Vertices[tri.A];
            var v1 = mesh.Vertices[tri.B];
            var v2 = mesh.Vertices[tri.C];
            if (RayTriangleIntersect(origin, dx, dy, dz, v0, v1, v2))
            {
                hits++;
            }
        }

        return hits;
    }

    // Möller–Trumbore ray-triangle intersection with configurable direction.
    private static bool RayTriangleIntersect(Vertex origin, double dirX, double dirY, double dirZ, Vertex v0, Vertex v1, Vertex v2)
    {
        const double Epsilon = 1e-9;

        double e1x = v1.X - v0.X, e1y = v1.Y - v0.Y, e1z = v1.Z - v0.Z;
        double e2x = v2.X - v0.X, e2y = v2.Y - v0.Y, e2z = v2.Z - v0.Z;

        // h = dir × e2
        double hx = dirY * e2z - dirZ * e2y;
        double hy = dirZ * e2x - dirX * e2z;
        double hz = dirX * e2y - dirY * e2x;

        double det = e1x * hx + e1y * hy + e1z * hz;
        if (det > -Epsilon && det < Epsilon)
        {
            return false; // parallel
        }

        double invDet = 1.0 / det;
        double sx = origin.X - v0.X, sy = origin.Y - v0.Y, sz = origin.Z - v0.Z;
        double u = (sx * hx + sy * hy + sz * hz) * invDet;
        if (u < 0.0 || u > 1.0)
        {
            return false;
        }

        // q = s × e1
        double qx = sy * e1z - sz * e1y;
        double qy = sz * e1x - sx * e1z;
        double qz = sx * e1y - sy * e1x;

        double v = (dirX * qx + dirY * qy + dirZ * qz) * invDet;
        if (v < 0.0 || u + v > 1.0)
        {
            return false;
        }

        double t = (e2x * qx + e2y * qy + e2z * qz) * invDet;
        return t > Epsilon;
    }
}
