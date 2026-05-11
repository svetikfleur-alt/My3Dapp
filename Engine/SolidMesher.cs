using FormaCore.Core;

namespace FormaCore.Engine;

public sealed class SolidMesher
{
    public Mesh Tessellate(Solid solid)
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

    // CSG subtract: keep faces of A outside B, keep flipped faces of B inside A.
    // Uses ray-cast centroid classification (no triangle splitting at seams).
    private static Mesh CsgSubtract(Mesh a, Mesh b)
    {
        var result = new Mesh();
        CopyFilteredTriangles(a, b, insideB: false, flipNormals: false, result);
        CopyFilteredTriangles(b, a, insideB: true, flipNormals: true, result);
        return result;
    }

    // CSG intersect: keep faces of A inside B, keep faces of B inside A.
    private static Mesh CsgIntersect(Mesh a, Mesh b)
    {
        var result = new Mesh();
        CopyFilteredTriangles(a, b, insideB: true, flipNormals: false, result);
        CopyFilteredTriangles(b, a, insideB: true, flipNormals: false, result);
        return result;
    }

    private static void CopyFilteredTriangles(Mesh source, Mesh classifier, bool insideB, bool flipNormals, Mesh result)
    {
        foreach (var tri in source.Triangles)
        {
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

    // Ray cast from point in +Y direction; odd intersection count = inside.
    private static bool IsPointInsideMesh(Mesh mesh, Vertex point)
    {
        var hits = 0;
        foreach (var tri in mesh.Triangles)
        {
            var v0 = mesh.Vertices[tri.A];
            var v1 = mesh.Vertices[tri.B];
            var v2 = mesh.Vertices[tri.C];
            if (RayTriangleIntersect(point, v0, v1, v2))
            {
                hits++;
            }
        }

        return (hits & 1) == 1;
    }

    // Möller–Trumbore ray-triangle intersection, ray direction = +Y.
    // Returns true if the ray from origin in +Y hits the triangle (t > 0).
    private static bool RayTriangleIntersect(Vertex origin, Vertex v0, Vertex v1, Vertex v2)
    {
        const double Epsilon = 1e-9;

        // edge vectors
        double e1x = v1.X - v0.X, e1y = v1.Y - v0.Y, e1z = v1.Z - v0.Z;
        double e2x = v2.X - v0.X, e2y = v2.Y - v0.Y, e2z = v2.Z - v0.Z;

        // ray direction is (0, 1, 0)
        // h = rayDir cross e2 = (1*e2z - 0*e2y, 0*e2x - 0*e2z, 0*e2y - 1*e2x)
        //                     = (e2z, 0, -e2x)
        double hx = e2z, hy = 0.0, hz = -e2x;

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

        // q = s cross e1
        double qx = sy * e1z - sz * e1y;
        double qy = sz * e1x - sx * e1z;
        double qz = sx * e1y - sy * e1x;

        // ray direction dot q = 1*qy
        double v = qy * invDet;
        if (v < 0.0 || u + v > 1.0)
        {
            return false;
        }

        // t = e2 dot q
        double t = (e2x * qx + e2y * qy + e2z * qz) * invDet;
        return t > Epsilon;
    }
}
