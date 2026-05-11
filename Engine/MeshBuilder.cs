using FormaCore.Core;

namespace FormaCore.Engine;

public static class MeshBuilder
{
    // 🔷 BOX
    public static Mesh CreateBox(double w, double d, double h)
    {
        var mesh = new Mesh();

        double x = w / 2;
        double y = d / 2;
        double z = h / 2;

        mesh.Vertices.AddRange(new[]
        {
            new Vertex(-x,-y,-z),
            new Vertex(x,-y,-z),
            new Vertex(x,y,-z),
            new Vertex(-x,y,-z),
            new Vertex(-x,-y,z),
            new Vertex(x,-y,z),
            new Vertex(x,y,z),
            new Vertex(-x,y,z)
        });

        int[,] faces = {
            {0,1,2},{0,2,3},
            {4,5,6},{4,6,7},
            {0,1,5},{0,5,4},
            {2,3,7},{2,7,6},
            {1,2,6},{1,6,5},
            {0,3,7},{0,7,4}
        };

        for (int i = 0; i < faces.GetLength(0); i++)
        {
            mesh.Triangles.Add(new Triangle(
                faces[i, 0],
                faces[i, 1],
                faces[i, 2]
            ));
        }

        return mesh;
    }

    public static Mesh CreateCylinder(double radius, double height, int segments = 64, bool capTop = true, bool capBottom = true)
{
    var mesh = new Mesh();
    double half = height / 2;

    var bottom = new List<int>();
    var top = new List<int>();

    // 🔷 создаём вершины круга (ОДИН раз!)
    for (int i = 0; i < segments; i++)
    {
        double angle = 2 * Math.PI * i / segments;

        double x = Math.Cos(angle) * radius;
        double y = Math.Sin(angle) * radius;

        // нижняя точка
        bottom.Add(mesh.Vertices.Count);
        mesh.Vertices.Add(new Vertex(x, y, -half));

        // верхняя точка
        top.Add(mesh.Vertices.Count);
        mesh.Vertices.Add(new Vertex(x, y, half));
    }

    // 🔷 боковые стенки
    for (int i = 0; i < segments; i++)
    {
        int next = (i + 1) % segments;

        int b0 = bottom[i];
        int b1 = bottom[next];
        int t0 = top[i];
        int t1 = top[next];

        mesh.Triangles.Add(new Triangle(b0, t0, b1));
        mesh.Triangles.Add(new Triangle(b1, t0, t1));
    }

    // 🔷 центры крышек
    int topCenter = mesh.Vertices.Count;
    mesh.Vertices.Add(new Vertex(0, 0, half));

    int bottomCenter = mesh.Vertices.Count;
    mesh.Vertices.Add(new Vertex(0, 0, -half));

    // 🔷 крышки
    for (int i = 0; i < segments; i++)
    {
        int next = (i + 1) % segments;

        // верх
        mesh.Triangles.Add(new Triangle(topCenter, top[i], top[next]));

        // низ
        mesh.Triangles.Add(new Triangle(bottomCenter, bottom[next], bottom[i]));
    }

    return mesh;
}

    public static Mesh CreateSphere(double radius, int stacks = 32, int sectors = 64)
    {
        var mesh = new Mesh();
        var rings = new List<List<int>>();

        int topPole = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(0, 0, radius));

        for (int i = 1; i < stacks; i++)
        {
            double phi = Math.PI * i / stacks;
            double sinPhi = Math.Sin(phi);
            double cosPhi = Math.Cos(phi);
            var ring = new List<int>();

            for (int j = 0; j < sectors; j++)
            {
                double theta = 2 * Math.PI * j / sectors;
                double x = radius * sinPhi * Math.Cos(theta);
                double y = radius * sinPhi * Math.Sin(theta);
                double z = radius * cosPhi;
                ring.Add(mesh.Vertices.Count);
                mesh.Vertices.Add(new Vertex(x, y, z));
            }

            rings.Add(ring);
        }

        int bottomPole = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(0, 0, -radius));

        if (rings.Count == 0)
        {
            return mesh;
        }

        var firstRing = rings[0];
        for (int j = 0; j < sectors; j++)
        {
            int next = (j + 1) % sectors;
            mesh.Triangles.Add(new Triangle(topPole, firstRing[j], firstRing[next]));
        }

        for (int i = 0; i < rings.Count - 1; i++)
        {
            var currentRing = rings[i];
            var nextRing = rings[i + 1];
            for (int j = 0; j < sectors; j++)
            {
                int next = (j + 1) % sectors;
                int v00 = currentRing[j];
                int v01 = currentRing[next];
                int v10 = nextRing[j];
                int v11 = nextRing[next];
                mesh.Triangles.Add(new Triangle(v00, v10, v01));
                mesh.Triangles.Add(new Triangle(v01, v10, v11));
            }
        }

        var lastRing = rings[^1];
        for (int j = 0; j < sectors; j++)
        {
            int next = (j + 1) % sectors;
            mesh.Triangles.Add(new Triangle(bottomPole, lastRing[next], lastRing[j]));
        }

        return mesh;
    }

    public static Mesh CreateCone(double rTop, double rBottom, double height, int segments = 64)
    {
        var mesh = new Mesh();
        double half = height / 2;

        var bottom = new List<int>();
        var top = new List<int>();

        for (int i = 0; i < segments; i++)
        {
            double angle = 2 * Math.PI * i / segments;
            double cx = Math.Cos(angle);
            double cy = Math.Sin(angle);

            bottom.Add(mesh.Vertices.Count);
            mesh.Vertices.Add(new Vertex(cx * rBottom, cy * rBottom, -half));

            if (rTop > 0)
            {
                top.Add(mesh.Vertices.Count);
                mesh.Vertices.Add(new Vertex(cx * rTop, cy * rTop, half));
            }
        }

        if (rTop > 0)
        {
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.Triangles.Add(new Triangle(bottom[i], top[i], bottom[next]));
                mesh.Triangles.Add(new Triangle(bottom[next], top[i], top[next]));
            }

            int topCenter = mesh.Vertices.Count;
            mesh.Vertices.Add(new Vertex(0, 0, half));
            for (int i = 0; i < segments; i++)
                mesh.Triangles.Add(new Triangle(topCenter, top[i], top[(i + 1) % segments]));
        }
        else
        {
            int apex = mesh.Vertices.Count;
            mesh.Vertices.Add(new Vertex(0, 0, half));
            for (int i = 0; i < segments; i++)
                mesh.Triangles.Add(new Triangle(bottom[i], apex, bottom[(i + 1) % segments]));
        }

        int bottomCenter = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(0, 0, -half));
        for (int i = 0; i < segments; i++)
            mesh.Triangles.Add(new Triangle(bottomCenter, bottom[(i + 1) % segments], bottom[i]));

        return mesh;
    }

    public static Mesh CreateTorus(double majorRadius, double minorRadius, int majorSegments = 64, int minorSegments = 32)
    {
        var mesh = new Mesh();

        for (int i = 0; i < majorSegments; i++)
        {
            double phi = 2 * Math.PI * i / majorSegments;
            double cosPhi = Math.Cos(phi);
            double sinPhi = Math.Sin(phi);

            for (int j = 0; j < minorSegments; j++)
            {
                double theta = 2 * Math.PI * j / minorSegments;
                double cosTheta = Math.Cos(theta);
                double sinTheta = Math.Sin(theta);

                double x = (majorRadius + minorRadius * cosTheta) * cosPhi;
                double y = (majorRadius + minorRadius * cosTheta) * sinPhi;
                double z = minorRadius * sinTheta;
                mesh.Vertices.Add(new Vertex(x, y, z));
            }
        }

        for (int i = 0; i < majorSegments; i++)
        {
            int iNext = (i + 1) % majorSegments;
            for (int j = 0; j < minorSegments; j++)
            {
                int jNext = (j + 1) % minorSegments;
                int v00 = i * minorSegments + j;
                int v01 = i * minorSegments + jNext;
                int v10 = iNext * minorSegments + j;
                int v11 = iNext * minorSegments + jNext;
                mesh.Triangles.Add(new Triangle(v00, v10, v01));
                mesh.Triangles.Add(new Triangle(v01, v10, v11));
            }
        }

        return mesh;
    }

    public static Mesh CreatePyramid(double baseWidth, double baseDepth, double height)
    {
        var mesh = new Mesh();
        double hw = baseWidth / 2;
        double hd = baseDepth / 2;
        double hh = height / 2;

        // 4 base corners + apex
        mesh.Vertices.AddRange(new[]
        {
            new Vertex(-hw, -hd, -hh), // 0 front-left
            new Vertex( hw, -hd, -hh), // 1 front-right
            new Vertex( hw,  hd, -hh), // 2 back-right
            new Vertex(-hw,  hd, -hh), // 3 back-left
            new Vertex(  0,   0,  hh), // 4 apex
        });

        // Bottom face
        mesh.Triangles.Add(new Triangle(0, 2, 1));
        mesh.Triangles.Add(new Triangle(0, 3, 2));
        // Side faces
        mesh.Triangles.Add(new Triangle(0, 1, 4));
        mesh.Triangles.Add(new Triangle(1, 2, 4));
        mesh.Triangles.Add(new Triangle(2, 3, 4));
        mesh.Triangles.Add(new Triangle(3, 0, 4));

        return mesh;
    }

    public static Mesh CreateWedge(double width, double depth, double height)
    {
        var mesh = new Mesh();
        double hw = width / 2;
        double hd = depth / 2;
        double hh = height / 2;

        // 4 bottom corners + 2 top-back corners (ridge at back)
        mesh.Vertices.AddRange(new[]
        {
            new Vertex(-hw, -hd, -hh), // 0 bottom-front-left
            new Vertex( hw, -hd, -hh), // 1 bottom-front-right
            new Vertex( hw,  hd, -hh), // 2 bottom-back-right
            new Vertex(-hw,  hd, -hh), // 3 bottom-back-left
            new Vertex(-hw,  hd,  hh), // 4 top-back-left
            new Vertex( hw,  hd,  hh), // 5 top-back-right
        });

        // Bottom rectangle
        mesh.Triangles.Add(new Triangle(0, 1, 2));
        mesh.Triangles.Add(new Triangle(0, 2, 3));
        // Back rectangle
        mesh.Triangles.Add(new Triangle(3, 2, 5));
        mesh.Triangles.Add(new Triangle(3, 5, 4));
        // Left triangle
        mesh.Triangles.Add(new Triangle(0, 3, 4));
        // Right triangle
        mesh.Triangles.Add(new Triangle(1, 5, 2));
        // Sloped face
        mesh.Triangles.Add(new Triangle(0, 4, 5));
        mesh.Triangles.Add(new Triangle(0, 5, 1));

        return mesh;
    }

    public static Mesh CreateEllipsoid(double radiusX, double radiusY, double radiusZ, int stacks = 32, int sectors = 64)
    {
        var mesh = new Mesh();
        var rings = new List<List<int>>();

        int topPole = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(0, 0, radiusZ));

        for (int i = 1; i < stacks; i++)
        {
            double phi = Math.PI * i / stacks;
            double sinPhi = Math.Sin(phi);
            double cosPhi = Math.Cos(phi);
            var ring = new List<int>();

            for (int j = 0; j < sectors; j++)
            {
                double theta = 2 * Math.PI * j / sectors;
                double x = radiusX * sinPhi * Math.Cos(theta);
                double y = radiusY * sinPhi * Math.Sin(theta);
                double z = radiusZ * cosPhi;
                ring.Add(mesh.Vertices.Count);
                mesh.Vertices.Add(new Vertex(x, y, z));
            }

            rings.Add(ring);
        }

        int bottomPole = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(0, 0, -radiusZ));

        if (rings.Count == 0)
        {
            return mesh;
        }

        var firstRing = rings[0];
        for (int j = 0; j < sectors; j++)
        {
            int next = (j + 1) % sectors;
            mesh.Triangles.Add(new Triangle(topPole, firstRing[j], firstRing[next]));
        }

        for (int i = 0; i < rings.Count - 1; i++)
        {
            var currentRing = rings[i];
            var nextRing = rings[i + 1];
            for (int j = 0; j < sectors; j++)
            {
                int next = (j + 1) % sectors;
                int v00 = currentRing[j];
                int v01 = currentRing[next];
                int v10 = nextRing[j];
                int v11 = nextRing[next];
                mesh.Triangles.Add(new Triangle(v00, v10, v01));
                mesh.Triangles.Add(new Triangle(v01, v10, v11));
            }
        }

        var lastRing = rings[^1];
        for (int j = 0; j < sectors; j++)
        {
            int next = (j + 1) % sectors;
            mesh.Triangles.Add(new Triangle(bottomPole, lastRing[next], lastRing[j]));
        }

        return mesh;
    }

    public static Mesh CreateCapsule(double radius, double height, int segments = 64, int hemisphereStacks = 16)
    {
        var mesh = new Mesh();
        double half = height / 2;
        var rings = new List<List<int>>();

        // Bottom hemisphere rings (from bottom pole upward, excluding pole itself)
        for (int i = hemisphereStacks; i >= 1; i--)
        {
            double phi = Math.PI / 2.0 * i / hemisphereStacks;
            double ringRadius = radius * Math.Cos(phi);
            double z = -half - radius * Math.Sin(phi);
            var ring = new List<int>();
            for (int j = 0; j < segments; j++)
            {
                double theta = 2 * Math.PI * j / segments;
                ring.Add(mesh.Vertices.Count);
                mesh.Vertices.Add(new Vertex(ringRadius * Math.Cos(theta), ringRadius * Math.Sin(theta), z));
            }
            rings.Add(ring);
        }

        // Bottom equator
        var bottomEquator = new List<int>();
        for (int j = 0; j < segments; j++)
        {
            double theta = 2 * Math.PI * j / segments;
            bottomEquator.Add(mesh.Vertices.Count);
            mesh.Vertices.Add(new Vertex(radius * Math.Cos(theta), radius * Math.Sin(theta), -half));
        }
        rings.Add(bottomEquator);

        // Top equator
        var topEquator = new List<int>();
        for (int j = 0; j < segments; j++)
        {
            double theta = 2 * Math.PI * j / segments;
            topEquator.Add(mesh.Vertices.Count);
            mesh.Vertices.Add(new Vertex(radius * Math.Cos(theta), radius * Math.Sin(theta), half));
        }
        rings.Add(topEquator);

        // Top hemisphere rings (from equator upward, excluding top pole)
        for (int i = 1; i <= hemisphereStacks; i++)
        {
            double phi = Math.PI / 2.0 * i / hemisphereStacks;
            double ringRadius = radius * Math.Cos(phi);
            double z = half + radius * Math.Sin(phi);
            var ring = new List<int>();
            for (int j = 0; j < segments; j++)
            {
                double theta = 2 * Math.PI * j / segments;
                ring.Add(mesh.Vertices.Count);
                mesh.Vertices.Add(new Vertex(ringRadius * Math.Cos(theta), ringRadius * Math.Sin(theta), z));
            }
            rings.Add(ring);
        }

        // Connect adjacent rings with quads
        for (int r = 0; r < rings.Count - 1; r++)
        {
            var r0 = rings[r];
            var r1 = rings[r + 1];
            for (int j = 0; j < segments; j++)
            {
                int next = (j + 1) % segments;
                mesh.Triangles.Add(new Triangle(r0[j], r1[j], r0[next]));
                mesh.Triangles.Add(new Triangle(r0[next], r1[j], r1[next]));
            }
        }

        // Bottom pole cap
        int bottomPole = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(0, 0, -(half + radius)));
        var first = rings[0];
        for (int j = 0; j < segments; j++)
            mesh.Triangles.Add(new Triangle(bottomPole, first[(j + 1) % segments], first[j]));

        // Top pole cap
        int topPole = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(0, 0, half + radius));
        var last = rings[rings.Count - 1];
        for (int j = 0; j < segments; j++)
            mesh.Triangles.Add(new Triangle(topPole, last[j], last[(j + 1) % segments]));

        return mesh;
    }

    public static Mesh CreateHemisphere(double radius, int stacks = 16, int sectors = 64)
    {
        var mesh = new Mesh();
        var rings = new List<List<int>>();

        int topPole = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(0, 0, radius));

        for (int i = 1; i <= stacks; i++)
        {
            double phi = Math.PI / 2.0 * i / stacks;
            double sinPhi = Math.Sin(phi);
            double cosPhi = Math.Cos(phi);
            var ring = new List<int>();
            for (int j = 0; j < sectors; j++)
            {
                double theta = 2 * Math.PI * j / sectors;
                ring.Add(mesh.Vertices.Count);
                mesh.Vertices.Add(new Vertex(
                    radius * sinPhi * Math.Cos(theta),
                    radius * sinPhi * Math.Sin(theta),
                    radius * cosPhi));
            }

            rings.Add(ring);
        }

        if (rings.Count == 0)
        {
            return mesh;
        }

        var firstRing = rings[0];
        for (int j = 0; j < sectors; j++)
        {
            int next = (j + 1) % sectors;
            mesh.Triangles.Add(new Triangle(topPole, firstRing[j], firstRing[next]));
        }

        for (int i = 0; i < rings.Count - 1; i++)
        {
            var currentRing = rings[i];
            var nextRing = rings[i + 1];
            for (int j = 0; j < sectors; j++)
            {
                int next = (j + 1) % sectors;
                int v00 = currentRing[j];
                int v01 = currentRing[next];
                int v10 = nextRing[j];
                int v11 = nextRing[next];
                mesh.Triangles.Add(new Triangle(v00, v10, v01));
                mesh.Triangles.Add(new Triangle(v01, v10, v11));
            }
        }

        // Flat bottom cap at z=0
        int center = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(0, 0, 0));
        var equator = rings[^1];
        for (int j = 0; j < sectors; j++)
            mesh.Triangles.Add(new Triangle(center, equator[(j + 1) % sectors], equator[j]));

        return mesh;
    }

    public static Mesh CreatePrism(double radius, double height, int sides)
    {
        return CreateCylinder(radius, height, sides);
    }

    public static Mesh CreatePolygonPrism(IReadOnlyList<Vector2D> profile, double height)
    {
        if (profile.Count < 3)
        {
            throw new ArgumentException("Polygon prism requires at least three profile points.", nameof(profile));
        }

        var mesh = new Mesh();
        var bottom = new List<int>(profile.Count);
        var top = new List<int>(profile.Count);
        var signedArea = ComputeSignedArea(profile);
        var isCounterClockwise = signedArea >= 0d;

        for (var index = 0; index < profile.Count; index++)
        {
            var point = profile[index];
            bottom.Add(mesh.Vertices.Count);
            mesh.Vertices.Add(new Vertex(point.X, point.Y, 0d));

            top.Add(mesh.Vertices.Count);
            mesh.Vertices.Add(new Vertex(point.X, point.Y, height));
        }

        for (var index = 0; index < profile.Count; index++)
        {
            var next = (index + 1) % profile.Count;
            var b0 = bottom[index];
            var b1 = bottom[next];
            var t0 = top[index];
            var t1 = top[next];

            if (isCounterClockwise)
            {
                // CCW profile: outward normal = cross(b1-b0, t0-b0) points away from interior
                mesh.Triangles.Add(new Triangle(b0, b1, t0));
                mesh.Triangles.Add(new Triangle(b1, t1, t0));
                continue;
            }

            // CW profile: reverse winding to keep normals pointing outward
            mesh.Triangles.Add(new Triangle(b0, t0, b1));
            mesh.Triangles.Add(new Triangle(b1, t0, t1));
        }

        for (var index = 1; index < profile.Count - 1; index++)
        {
            if (isCounterClockwise)
            {
                mesh.Triangles.Add(new Triangle(top[0], top[index], top[index + 1]));
                mesh.Triangles.Add(new Triangle(bottom[0], bottom[index + 1], bottom[index]));
                continue;
            }

            mesh.Triangles.Add(new Triangle(top[0], top[index + 1], top[index]));
            mesh.Triangles.Add(new Triangle(bottom[0], bottom[index], bottom[index + 1]));
        }

        return mesh;
    }

    public static Mesh CreateDisk(double outerRadius, double innerRadius = 0, int segments = 64)
    {
        var mesh = new Mesh();

        if (innerRadius <= 0)
        {
            int center = mesh.Vertices.Count;
            mesh.Vertices.Add(new Vertex(0, 0, 0));
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                mesh.Vertices.Add(new Vertex(outerRadius * Math.Cos(angle), outerRadius * Math.Sin(angle), 0));
            }
            for (int i = 0; i < segments; i++)
            {
                int v0 = 1 + i;
                int v1 = 1 + (i + 1) % segments;
                mesh.Triangles.Add(new Triangle(center, v0, v1));
            }
        }
        else
        {
            var outer = new List<int>();
            var inner = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                outer.Add(mesh.Vertices.Count);
                mesh.Vertices.Add(new Vertex(outerRadius * cos, outerRadius * sin, 0));
                inner.Add(mesh.Vertices.Count);
                mesh.Vertices.Add(new Vertex(innerRadius * cos, innerRadius * sin, 0));
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.Triangles.Add(new Triangle(outer[i], inner[i], outer[next]));
                mesh.Triangles.Add(new Triangle(outer[next], inner[i], inner[next]));
            }
        }

        return mesh;
    }

    public static Mesh CreateArrow(double shaftRadius, double shaftHeight, double headRadius, double headHeight, int segments = 64)
    {
        var shaft = CreateCylinder(shaftRadius, shaftHeight, segments);
        shaft.Triangles.RemoveRange(segments * 2, segments);
        shaft = Translate(shaft, 0, 0, shaftHeight / 2);

        var head = CreateCone(0, headRadius, headHeight, segments);
        head.Triangles.RemoveRange(head.Triangles.Count - segments, segments);
        head = Translate(head, 0, 0, shaftHeight + headHeight / 2);

        return Merge(shaft, head);
    }

    public static Mesh CreateTetrahedron(double radius)
    {
        var mesh = new Mesh();
        double s = radius / Math.Sqrt(3.0);

        mesh.Vertices.Add(new Vertex( s,  s,  s));
        mesh.Vertices.Add(new Vertex( s, -s, -s));
        mesh.Vertices.Add(new Vertex(-s,  s, -s));
        mesh.Vertices.Add(new Vertex(-s, -s,  s));

        mesh.Triangles.Add(new Triangle(0, 2, 1));
        mesh.Triangles.Add(new Triangle(0, 1, 3));
        mesh.Triangles.Add(new Triangle(0, 3, 2));
        mesh.Triangles.Add(new Triangle(1, 2, 3));

        return mesh;
    }

    public static Mesh CreateOctahedron(double radius)
    {
        var mesh = new Mesh();

        mesh.Vertices.Add(new Vertex( radius,  0,      0));
        mesh.Vertices.Add(new Vertex(-radius,  0,      0));
        mesh.Vertices.Add(new Vertex( 0,       radius, 0));
        mesh.Vertices.Add(new Vertex( 0,      -radius, 0));
        mesh.Vertices.Add(new Vertex( 0,       0,      radius));
        mesh.Vertices.Add(new Vertex( 0,       0,     -radius));

        int[,] faces = {
            {0,2,4},{2,1,4},{1,3,4},{3,0,4},
            {0,5,2},{2,5,1},{1,5,3},{3,5,0},
        };
        for (int i = 0; i < faces.GetLength(0); i++)
            mesh.Triangles.Add(new Triangle(faces[i, 0], faces[i, 1], faces[i, 2]));

        return mesh;
    }

    public static Mesh CreateIcosahedron(double radius)
    {
        var mesh = new Mesh();
        double t = (1.0 + Math.Sqrt(5.0)) / 2.0;
        double scale = radius / Math.Sqrt(1.0 + t * t);

        double[][] verts = {
            new[]{-1,  t,  0}, new[]{ 1,  t,  0}, new[]{-1, -t,  0}, new[]{ 1, -t,  0},
            new[]{ 0, -1,  t}, new[]{ 0,  1,  t}, new[]{ 0, -1, -t}, new[]{ 0,  1, -t},
            new[]{ t,  0, -1}, new[]{ t,  0,  1}, new[]{-t,  0, -1}, new[]{-t,  0,  1},
        };
        foreach (var v in verts)
            mesh.Vertices.Add(new Vertex(v[0] * scale, v[1] * scale, v[2] * scale));

        int[,] faces = {
            {0,11,5},{0,5,1},{0,1,7},{0,7,10},{0,10,11},
            {1,5,9},{5,11,4},{11,10,2},{10,7,6},{7,1,8},
            {3,9,4},{3,4,2},{3,2,6},{3,6,8},{3,8,9},
            {4,9,5},{2,4,11},{6,2,10},{8,6,7},{9,8,1},
        };
        for (int i = 0; i < faces.GetLength(0); i++)
            mesh.Triangles.Add(new Triangle(faces[i, 0], faces[i, 1], faces[i, 2]));

        return mesh;
    }

    public static Mesh CreateIcosphere(double radius, int subdivisions = 2)
    {
        var mesh = CreateIcosahedron(radius);
        for (int i = 0; i < subdivisions; i++)
            mesh = SubdivideIcosphere(mesh, radius);
        return mesh;
    }

    private static Mesh SubdivideIcosphere(Mesh mesh, double radius)
    {
        var result = new Mesh();
        result.Vertices.AddRange(mesh.Vertices);
        var midCache = new Dictionary<long, int>();

        int GetMidpoint(int a, int b)
        {
            long key = a < b ? ((long)a << 32 | (uint)b) : ((long)b << 32 | (uint)a);
            if (midCache.TryGetValue(key, out int idx)) return idx;

            var va = result.Vertices[a];
            var vb = result.Vertices[b];
            double mx = (va.X + vb.X) / 2;
            double my = (va.Y + vb.Y) / 2;
            double mz = (va.Z + vb.Z) / 2;
            double len = Math.Sqrt(mx * mx + my * my + mz * mz);

            idx = result.Vertices.Count;
            result.Vertices.Add(new Vertex(radius * mx / len, radius * my / len, radius * mz / len));
            midCache[key] = idx;
            return idx;
        }

        foreach (var tri in mesh.Triangles)
        {
            int m01 = GetMidpoint(tri.A, tri.B);
            int m12 = GetMidpoint(tri.B, tri.C);
            int m20 = GetMidpoint(tri.C, tri.A);
            result.Triangles.Add(new Triangle(tri.A, m01, m20));
            result.Triangles.Add(new Triangle(tri.B, m12, m01));
            result.Triangles.Add(new Triangle(tri.C, m20, m12));
            result.Triangles.Add(new Triangle(m01, m12, m20));
        }

        return result;
    }

    public static Mesh CreateSpring(double coilRadius, double tubeRadius, int coils, double height, int coilSegments = 64, int tubeSegments = 16)
    {
        var mesh = new Mesh();
        int totalSegments = coils * coilSegments;
        double heightPerRad = height / (2 * Math.PI * coils);

        for (int i = 0; i <= totalSegments; i++)
        {
            double t = (double)i / totalSegments;
            double angle = 2 * Math.PI * coils * t;
            double cx = coilRadius * Math.Cos(angle);
            double cy = coilRadius * Math.Sin(angle);
            double cz = height * t - height / 2;

            // Tangent along helix
            double tx = -Math.Sin(angle);
            double ty =  Math.Cos(angle);
            double tz = heightPerRad / coilRadius;
            double tLen = Math.Sqrt(tx * tx + ty * ty + tz * tz);
            tx /= tLen; ty /= tLen; tz /= tLen;

            // Outward normal from helix axis
            double nx = Math.Cos(angle);
            double ny = Math.Sin(angle);
            double nz = 0;

            // Binormal = tangent × normal
            double bx = ty * nz - tz * ny;
            double by = tz * nx - tx * nz;
            double bz = tx * ny - ty * nx;

            for (int j = 0; j < tubeSegments; j++)
            {
                double phi = 2 * Math.PI * j / tubeSegments;
                double cos = Math.Cos(phi);
                double sin = Math.Sin(phi);
                mesh.Vertices.Add(new Vertex(
                    cx + tubeRadius * (cos * nx + sin * bx),
                    cy + tubeRadius * (cos * ny + sin * by),
                    cz + tubeRadius * (cos * nz + sin * bz)));
            }
        }

        for (int i = 0; i < totalSegments; i++)
        {
            for (int j = 0; j < tubeSegments; j++)
            {
                int next = (j + 1) % tubeSegments;
                int v00 = i * tubeSegments + j;
                int v01 = i * tubeSegments + next;
                int v10 = (i + 1) * tubeSegments + j;
                int v11 = (i + 1) * tubeSegments + next;
                mesh.Triangles.Add(new Triangle(v00, v10, v01));
                mesh.Triangles.Add(new Triangle(v01, v10, v11));
            }
        }

        return mesh;
    }

    // 🔷 TRANSLATE
    public static Mesh Translate(Mesh mesh, double x, double y, double z)
    {
        var result = new Mesh();

        foreach (var v in mesh.Vertices)
            result.Vertices.Add(new Vertex(v.X + x, v.Y + y, v.Z + z));

        result.Triangles.AddRange(mesh.Triangles);

        return result;
    }

    // 🔷 ROTATE around X/Y/Z axis (degrees)
    public static Mesh RotateAroundAxis(Mesh mesh, MirrorAxis axis, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var result = new Mesh();
        foreach (var v in mesh.Vertices)
        {
            result.Vertices.Add(axis switch
            {
                MirrorAxis.X => new Vertex(v.X, v.Y * cos - v.Z * sin, v.Y * sin + v.Z * cos),
                MirrorAxis.Z => new Vertex(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos, v.Z),
                _ => new Vertex(v.X * cos + v.Z * sin, v.Y, -v.X * sin + v.Z * cos)
            });
        }
        result.Triangles.AddRange(mesh.Triangles);
        return result;
    }

    // 🔷 MERGE (Union без boolean)
    public static Mesh Merge(Mesh a, Mesh b)
    {
        var result = new Mesh();

        result.Vertices.AddRange(a.Vertices);
        result.Triangles.AddRange(a.Triangles);

        int offset = a.Vertices.Count;

        result.Vertices.AddRange(b.Vertices);

        foreach (var t in b.Triangles)
        {
            result.Triangles.Add(new Triangle(
                t.A + offset,
                t.B + offset,
                t.C + offset
            ));
        }

        return result;
    }

    // ── EXTENDED EXTRUDE ──────────────────────────────────────────────────────

    public static Mesh CreateExtrudedProfile(
        IReadOnlyList<Vector2D> profile,
        double height,
        PlaneOrientation plane,
        bool symmetric,
        double taperDegrees,
        double shellThickness)
    {
        double startD = symmetric ? -height / 2d : 0d;
        double endD   = symmetric ?  height / 2d : height;
        var topProfile = taperDegrees > 0.0001d
            ? ApplyTaper(profile, height, taperDegrees)
            : profile;

        return shellThickness > 0.0001d
            ? BuildShellMesh(profile, topProfile, startD, endD, plane, shellThickness)
            : BuildSolidPrismMesh(profile, topProfile, startD, endD, plane);
    }

    // Face-plane extrude: maps sketch UV coordinates into world space using an arbitrary face frame
    public static Mesh CreateExtrudedProfileFace(
        IReadOnlyList<Vector2D> profile,
        double height,
        FacePlaneData fp,
        bool symmetric)
    {
        double startD = symmetric ? -height / 2d : 0d;
        double endD   = symmetric ?  height / 2d : height;
        var mesh = new Mesh();
        var n = profile.Count;
        var bottom = new int[n];
        var top    = new int[n];
        for (var i = 0; i < n; i++)
        {
            bottom[i] = mesh.Vertices.Count; mesh.Vertices.Add(Map3DFace(profile[i], startD, fp));
            top[i]    = mesh.Vertices.Count; mesh.Vertices.Add(Map3DFace(profile[i], endD,   fp));
        }
        var isCcw = ComputeSignedArea(profile) >= 0d;
        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            int b0 = bottom[i], b1 = bottom[next], t0 = top[i], t1 = top[next];
            if (isCcw) { mesh.Triangles.Add(new Triangle(b0, b1, t0)); mesh.Triangles.Add(new Triangle(b1, t1, t0)); }
            else        { mesh.Triangles.Add(new Triangle(b0, t0, b1)); mesh.Triangles.Add(new Triangle(b1, t0, t1)); }
        }
        AddPolygonCap(mesh, bottom, profile, isCcw, false);
        AddPolygonCap(mesh, top,    profile, isCcw, true);
        return mesh;
    }

    private static Mesh BuildSolidPrismMesh(
        IReadOnlyList<Vector2D> botP,
        IReadOnlyList<Vector2D> topP,
        double startD,
        double endD,
        PlaneOrientation plane)
    {
        var mesh = new Mesh();
        var n = botP.Count;
        var bottom = new int[n];
        var top    = new int[n];
        for (var i = 0; i < n; i++)
        {
            bottom[i] = mesh.Vertices.Count; mesh.Vertices.Add(Map3D(botP[i], startD, plane));
            top[i]    = mesh.Vertices.Count; mesh.Vertices.Add(Map3D(topP[i], endD,   plane));
        }

        var isCcw = ComputeSignedArea(botP) >= 0d;
        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            int b0 = bottom[i], b1 = bottom[next], t0 = top[i], t1 = top[next];
            if (isCcw) { mesh.Triangles.Add(new Triangle(b0, b1, t0)); mesh.Triangles.Add(new Triangle(b1, t1, t0)); }
            else        { mesh.Triangles.Add(new Triangle(b0, t0, b1)); mesh.Triangles.Add(new Triangle(b1, t0, t1)); }
        }

        AddPolygonCap(mesh, bottom, topP, isCcw, false);
        AddPolygonCap(mesh, top,    topP, isCcw, true);
        return mesh;
    }

    private static Mesh BuildShellMesh(
        IReadOnlyList<Vector2D> outerBot,
        IReadOnlyList<Vector2D> outerTop,
        double startD,
        double endD,
        PlaneOrientation plane,
        double thickness)
    {
        var innerBot = OffsetPolygon(outerBot, -thickness);
        var innerTop = OffsetPolygon(outerTop, -thickness);
        if (innerBot is null || innerTop is null)
            return BuildSolidPrismMesh(outerBot, outerTop, startD, endD, plane);

        var mesh = new Mesh();
        var n = outerBot.Count;
        var ob = new int[n]; var ot = new int[n];
        var ib = new int[n]; var it = new int[n];
        for (var i = 0; i < n; i++)
        {
            ob[i] = mesh.Vertices.Count; mesh.Vertices.Add(Map3D(outerBot[i], startD, plane));
            ot[i] = mesh.Vertices.Count; mesh.Vertices.Add(Map3D(outerTop[i], endD,   plane));
            ib[i] = mesh.Vertices.Count; mesh.Vertices.Add(Map3D(innerBot[i], startD, plane));
            it[i] = mesh.Vertices.Count; mesh.Vertices.Add(Map3D(innerTop[i], endD,   plane));
        }

        bool isCcw = ComputeSignedArea(outerBot) >= 0d;
        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            // outer sides
            if (isCcw) { mesh.Triangles.Add(new Triangle(ob[i], ob[next], ot[i])); mesh.Triangles.Add(new Triangle(ob[next], ot[next], ot[i])); }
            else        { mesh.Triangles.Add(new Triangle(ob[i], ot[i], ob[next])); mesh.Triangles.Add(new Triangle(ob[next], ot[i], ot[next])); }
            // inner sides (reversed winding = inward-facing)
            if (isCcw) { mesh.Triangles.Add(new Triangle(ib[i], it[i], ib[next])); mesh.Triangles.Add(new Triangle(ib[next], it[i], it[next])); }
            else        { mesh.Triangles.Add(new Triangle(ib[i], ib[next], it[i])); mesh.Triangles.Add(new Triangle(ib[next], it[next], it[i])); }
            // bottom ring
            mesh.Triangles.Add(new Triangle(ob[i], ib[next], ob[next]));
            mesh.Triangles.Add(new Triangle(ob[i], ib[i],    ib[next]));
            // top ring
            mesh.Triangles.Add(new Triangle(ot[i], ot[next], it[next]));
            mesh.Triangles.Add(new Triangle(ot[i], it[next], it[i]));
        }

        return mesh;
    }

    private static void AddPolygonCap(Mesh mesh, int[] ring, IReadOnlyList<Vector2D> profile, bool isCcw, bool flipNormal)
    {
        for (var i = 1; i < ring.Length - 1; i++)
        {
            if (isCcw ^ flipNormal)
                mesh.Triangles.Add(new Triangle(ring[0], ring[i], ring[i + 1]));
            else
                mesh.Triangles.Add(new Triangle(ring[0], ring[i + 1], ring[i]));
        }
    }

    // Offset a CCW polygon inward by |amount| (negative amount = inward for CCW)
    public static IReadOnlyList<Vector2D>? OffsetPolygon(IReadOnlyList<Vector2D> poly, double amount)
    {
        var n = poly.Count;
        if (n < 3) return null;

        // Compute inward-offset edges
        var offsets = new (double ox, double oy, double ex, double ey)[n];
        for (var i = 0; i < n; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % n];
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return null;
            var nx = -dy / len * amount; var ny = dx / len * amount;
            offsets[i] = (a.X + nx, a.Y + ny, b.X + nx, b.Y + ny);
        }

        // Intersect adjacent offset edges to find new vertices
        var result = new List<Vector2D>(n);
        for (var i = 0; i < n; i++)
        {
            var prev = (i - 1 + n) % n;
            var (p1x, p1y, p2x, p2y) = offsets[prev];
            var (p3x, p3y, p4x, p4y) = offsets[i];
            var d = (p2x - p1x) * (p4y - p3y) - (p2y - p1y) * (p4x - p3x);
            if (Math.Abs(d) < 1e-9)
            {
                result.Add(new Vector2D(p3x, p3y));
                continue;
            }
            var t = ((p3x - p1x) * (p4y - p3y) - (p3y - p1y) * (p4x - p3x)) / d;
            result.Add(new Vector2D(p1x + t * (p2x - p1x), p1y + t * (p2y - p1y)));
        }

        // Validate: area must be positive and large enough
        if (ComputeSignedArea(result) <= 0.0001d) return null;
        return result;
    }

    // Chamfer polygon corners (flat cuts at each corner by the given distance)
    public static IReadOnlyList<Vector2D> ChamferPolygonCorners(IReadOnlyList<Vector2D> poly, double distance)
    {
        var n = poly.Count;
        var result = new List<Vector2D>(n * 2);
        for (var i = 0; i < n; i++)
        {
            var prev = poly[(i - 1 + n) % n];
            var curr = poly[i];
            var next = poly[(i + 1) % n];

            var d0x = prev.X - curr.X; var d0y = prev.Y - curr.Y;
            var d1x = next.X - curr.X; var d1y = next.Y - curr.Y;
            var len0 = Math.Sqrt(d0x * d0x + d0y * d0y);
            var len1 = Math.Sqrt(d1x * d1x + d1y * d1y);
            if (len0 < 1e-9 || len1 < 1e-9) { result.Add(curr); continue; }

            var t0 = Math.Min(distance, len0 * 0.45d);
            var t1 = Math.Min(distance, len1 * 0.45d);
            result.Add(new Vector2D(curr.X + d0x / len0 * t0, curr.Y + d0y / len0 * t0));
            result.Add(new Vector2D(curr.X + d1x / len1 * t1, curr.Y + d1y / len1 * t1));
        }
        return result;
    }

    // Round polygon corners in-place (2D fillet)
    public static IReadOnlyList<Vector2D> RoundPolygonCorners(IReadOnlyList<Vector2D> poly, double radius, int arcPoints = 6)
    {
        var n = poly.Count;
        var result = new List<Vector2D>();

        for (var i = 0; i < n; i++)
        {
            var prev = poly[(i - 1 + n) % n];
            var curr = poly[i];
            var next = poly[(i + 1) % n];

            var d0x = prev.X - curr.X; var d0y = prev.Y - curr.Y;
            var d1x = next.X - curr.X; var d1y = next.Y - curr.Y;
            var len0 = Math.Sqrt(d0x * d0x + d0y * d0y);
            var len1 = Math.Sqrt(d1x * d1x + d1y * d1y);
            if (len0 < 1e-9 || len1 < 1e-9) { result.Add(curr); continue; }

            d0x /= len0; d0y /= len0;
            d1x /= len1; d1y /= len1;

            // Half-angle between incoming and outgoing edges
            var cosA = d0x * d1x + d0y * d1y;
            cosA = Math.Clamp(cosA, -1d, 1d);
            var halfAngle = Math.Acos(cosA) / 2d;
            if (halfAngle < 1e-6) { result.Add(curr); continue; }

            var t = Math.Min(radius / Math.Tan(halfAngle), Math.Min(len0, len1) * 0.45d);

            var p0 = new Vector2D(curr.X + d0x * t, curr.Y + d0y * t);
            var p1 = new Vector2D(curr.X + d1x * t, curr.Y + d1y * t);

            // Arc center
            var bisectX = d0x + d1x; var bisectY = d0y + d1y;
            var bisectLen = Math.Sqrt(bisectX * bisectX + bisectY * bisectY);
            if (bisectLen < 1e-9) { result.Add(curr); continue; }
            bisectX /= bisectLen; bisectY /= bisectLen;

            var arcRadius = t * Math.Tan(halfAngle);
            var cx = curr.X + bisectX * arcRadius / Math.Sin(halfAngle);
            var cy = curr.Y + bisectY * arcRadius / Math.Sin(halfAngle);

            var startAngle = Math.Atan2(p0.Y - cy, p0.X - cx);
            var endAngle   = Math.Atan2(p1.Y - cy, p1.X - cx);

            // Determine sweep direction
            var cross = d0x * d1y - d0y * d1x;
            if (cross > 0)
            {
                while (endAngle < startAngle) endAngle += 2 * Math.PI;
            }
            else
            {
                while (endAngle > startAngle) endAngle -= 2 * Math.PI;
            }

            for (var j = 0; j <= arcPoints; j++)
            {
                var a = startAngle + (endAngle - startAngle) * j / arcPoints;
                result.Add(new Vector2D(cx + Math.Cos(a) * arcRadius, cy + Math.Sin(a) * arcRadius));
            }
        }

        return result;
    }

    // ── REVOLVE ──────────────────────────────────────────────────────────────

    // Profile revolves around a local sketch axis and is then mapped into the sketch plane.
    public static Mesh CreateRevolution(
        IReadOnlyList<Vector2D> profile,
        double angleDegrees,
        PlaneOrientation plane,
        ProfileAxis axis,
        int segments = 64)
    {
        var mesh = new Mesh();
        var n = profile.Count;
        angleDegrees = Math.Clamp(angleDegrees, 1d, 360d);
        var full = angleDegrees >= 359.9d;
        var radians = angleDegrees * Math.PI / 180d;
        var actualSegments = full ? segments : Math.Max(3, (int)(segments * angleDegrees / 360d));

        // Build vertex rings: ring[seg][profilePt]
        var rings = new int[actualSegments + 1][];
        for (var s = 0; s <= actualSegments; s++)
        {
            var theta = radians * s / actualSegments;
            var cos = Math.Cos(theta); var sin = Math.Sin(theta);
            rings[s] = new int[n];
            for (var p = 0; p < n; p++)
            {
                var sample = MapRevolvedPoint(profile[p], cos, sin, plane, axis);
                rings[s][p] = mesh.Vertices.Count;
                mesh.Vertices.Add(sample);
            }
        }

        // Side quads between adjacent rings
        var maxS = full ? actualSegments : actualSegments;
        for (var s = 0; s < maxS; s++)
        {
            var sNext = full ? (s + 1) % (actualSegments + 1) : s + 1;
            for (var p = 0; p < n - 1; p++)
            {
                int a = rings[s][p],    b = rings[s][p + 1];
                int c = rings[sNext][p], d = rings[sNext][p + 1];
                mesh.Triangles.Add(new Triangle(a, c, b));
                mesh.Triangles.Add(new Triangle(b, c, d));
            }
        }

        // Caps for partial revolutions
        if (!full)
        {
            AddRevolveCap(mesh, rings[0],                profile, false);
            AddRevolveCap(mesh, rings[actualSegments], profile, true);
        }

        return mesh;
    }

    // ── LOFT ──────────────────────────────────────────────────────────────────

    public static Mesh CreateLoft(
        IReadOnlyList<Vector2D> profileA,
        IReadOnlyList<Vector2D> profileB,
        double distance,
        PlaneOrientation plane,
        int steps = 8)
    {
        var mesh = new Mesh();
        var n = Math.Max(profileA.Count, profileB.Count);
        var a = ResampleProfile(profileA, n);
        var b = ResampleProfile(profileB, n);
        distance = Math.Max(0.01d, distance);

        var isCcw = ComputeSignedArea(a) >= 0d;

        var rings = new int[steps + 1][];
        for (var s = 0; s <= steps; s++)
        {
            var t = (double)s / steps;
            var depth = distance * t;
            rings[s] = new int[n];
            for (var i = 0; i < n; i++)
            {
                var px = a[i].X + (b[i].X - a[i].X) * t;
                var py = a[i].Y + (b[i].Y - a[i].Y) * t;
                rings[s][i] = mesh.Vertices.Count;
                mesh.Vertices.Add(Map3D(new Vector2D(px, py), depth, plane));
            }
        }

        for (var s = 0; s < steps; s++)
        {
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                int a0 = rings[s][i], a1 = rings[s][next];
                int b0 = rings[s + 1][i], b1 = rings[s + 1][next];
                if (isCcw) { mesh.Triangles.Add(new Triangle(a0, a1, b0)); mesh.Triangles.Add(new Triangle(a1, b1, b0)); }
                else        { mesh.Triangles.Add(new Triangle(a0, b0, a1)); mesh.Triangles.Add(new Triangle(a1, b0, b1)); }
            }
        }

        AddPolygonCap(mesh, rings[0],     a, isCcw, false);
        AddPolygonCap(mesh, rings[steps], b, isCcw, true);

        return mesh;
    }

    private static IReadOnlyList<Vector2D> ResampleProfile(IReadOnlyList<Vector2D> profile, int n)
    {
        if (profile.Count == n) return profile;
        var count = profile.Count;

        var arcLen = new double[count + 1];
        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            var dx = profile[next].X - profile[i].X;
            var dy = profile[next].Y - profile[i].Y;
            arcLen[i + 1] = arcLen[i] + Math.Sqrt(dx * dx + dy * dy);
        }
        var total = arcLen[count];
        if (total < 1e-9) return Enumerable.Repeat(profile[0], n).ToList();

        var result = new List<Vector2D>(n);
        for (var i = 0; i < n; i++)
        {
            var target = (double)i / n * total;
            var seg = 0;
            for (var j = 0; j < count; j++)
            {
                if (arcLen[j + 1] >= target) { seg = j; break; }
            }
            var segLen = arcLen[seg + 1] - arcLen[seg];
            var localT = segLen > 1e-9 ? (target - arcLen[seg]) / segLen : 0d;
            var nextPt = (seg + 1) % count;
            result.Add(new Vector2D(
                profile[seg].X + (profile[nextPt].X - profile[seg].X) * localT,
                profile[seg].Y + (profile[nextPt].Y - profile[seg].Y) * localT));
        }
        return result;
    }

    // ── SWEEP ─────────────────────────────────────────────────────────────────

    public static Mesh CreateSweep(
        IReadOnlyList<Vector2D> profile,
        double distance,
        PlaneOrientation plane,
        double twistDegrees = 0d,
        int segments = 32)
    {
        var mesh = new Mesh();
        var n = profile.Count;
        distance = Math.Max(0.01d, distance);
        segments = Math.Max(2, segments);
        var isCcw = ComputeSignedArea(profile) >= 0d;

        // Build vertex rings along the sweep direction
        var rings = new int[segments + 1][];
        for (var s = 0; s <= segments; s++)
        {
            var t = (double)s / segments;
            var depth = distance * t;
            var twistRad = twistDegrees * t * Math.PI / 180d;
            var cos = Math.Cos(twistRad);
            var sin = Math.Sin(twistRad);
            rings[s] = new int[n];
            for (var p = 0; p < n; p++)
            {
                var pt = profile[p];
                var rx = pt.X * cos - pt.Y * sin;
                var ry = pt.X * sin + pt.Y * cos;
                rings[s][p] = mesh.Vertices.Count;
                mesh.Vertices.Add(Map3D(new Vector2D(rx, ry), depth, plane));
            }
        }

        // Side quads between adjacent rings
        for (var s = 0; s < segments; s++)
        {
            for (var p = 0; p < n; p++)
            {
                var next = (p + 1) % n;
                int a = rings[s][p], b = rings[s][next];
                int c = rings[s + 1][p], d = rings[s + 1][next];
                if (isCcw) { mesh.Triangles.Add(new Triangle(a, b, c)); mesh.Triangles.Add(new Triangle(b, d, c)); }
                else        { mesh.Triangles.Add(new Triangle(a, c, b)); mesh.Triangles.Add(new Triangle(b, c, d)); }
            }
        }

        // Caps
        AddPolygonCap(mesh, rings[0],        profile, isCcw, false);
        AddPolygonCap(mesh, rings[segments],  profile, isCcw, true);

        return mesh;
    }

    private static Vertex MapRevolvedPoint(
        Vector2D point,
        double cos,
        double sin,
        PlaneOrientation plane,
        ProfileAxis axis)
    {
        if (axis == ProfileAxis.X)
        {
            var linear = point.X;
            var radius = Math.Abs(point.Y);
            return plane switch
            {
                PlaneOrientation.Front => new Vertex(radius * sin, linear, radius * cos),
                PlaneOrientation.Right => new Vertex(linear, radius * sin, radius * cos),
                _ => new Vertex(linear, radius * cos, radius * sin)
            };
        }

        var radiusFromX = Math.Abs(point.X);
        var linearFromY = point.Y;
        return plane switch
        {
            PlaneOrientation.Front => new Vertex(radiusFromX * sin, radiusFromX * cos, linearFromY),
            PlaneOrientation.Right => new Vertex(radiusFromX * cos, radiusFromX * sin, linearFromY),
            _ => new Vertex(radiusFromX * cos, linearFromY, radiusFromX * sin)
        };
    }

    private static void AddRevolveCap(Mesh mesh, int[] ring, IReadOnlyList<Vector2D> profile, bool flip)
    {
        var n = ring.Length;
        for (var i = 1; i < n - 1; i++)
        {
            if (!flip) mesh.Triangles.Add(new Triangle(ring[0], ring[i], ring[i + 1]));
            else        mesh.Triangles.Add(new Triangle(ring[0], ring[i + 1], ring[i]));
        }
    }

    // ── MIRROR ───────────────────────────────────────────────────────────────

    public static Mesh MirrorMesh(Mesh source, MirrorAxis axis)
    {
        var result = new Mesh();
        foreach (var v in source.Vertices)
            result.Vertices.Add(axis switch
            {
                MirrorAxis.X => new Vertex(-v.X, v.Y, v.Z),
                MirrorAxis.Y => new Vertex(v.X, -v.Y, v.Z),
                _ => new Vertex(v.X, v.Y, -v.Z)
            });
        // Reverse winding to maintain outward normals after reflection
        foreach (var t in source.Triangles)
            result.Triangles.Add(new Triangle(t.A, t.C, t.B));
        return result;
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private static Vertex Map3D(Vector2D p, double depth, PlaneOrientation plane) =>
        plane switch
        {
            PlaneOrientation.Front => new Vertex(depth, p.X, p.Y),
            PlaneOrientation.Right => new Vertex(p.X, depth, p.Y),
            _ => new Vertex(p.X, p.Y, depth)
        };

    private static Vertex Map3DFace(Vector2D p, double depth, FacePlaneData fp) =>
        new Vertex(
            fp.OriginX + fp.UX * p.X + fp.VX * p.Y + fp.NX * depth,
            fp.OriginY + fp.UY * p.X + fp.VY * p.Y + fp.NY * depth,
            fp.OriginZ + fp.UZ * p.X + fp.VZ * p.Y + fp.NZ * depth);

    private static IReadOnlyList<Vector2D> ApplyTaper(
        IReadOnlyList<Vector2D> profile, double height, double taperDegrees)
    {
        var centroid = ComputeCentroid(profile);
        var inset = height * Math.Tan(taperDegrees * Math.PI / 180d);
        return profile.Select(p =>
        {
            var dx = p.X - centroid.X; var dy = p.Y - centroid.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1e-9) return p;
            var scale = Math.Max(0d, (dist - inset) / dist);
            return new Vector2D(centroid.X + dx * scale, centroid.Y + dy * scale);
        }).ToList();
    }

    private static Vector2D ComputeCentroid(IReadOnlyList<Vector2D> points)
    {
        var sx = 0d; var sy = 0d;
        foreach (var p in points) { sx += p.X; sy += p.Y; }
        return new Vector2D(sx / points.Count, sy / points.Count);
    }

    private static double ComputeSignedArea(IReadOnlyList<Vector2D> profile)
    {
        var area = 0d;
        for (var index = 0; index < profile.Count; index++)
        {
            var current = profile[index];
            var next = profile[(index + 1) % profile.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area * 0.5d;
    }
}
