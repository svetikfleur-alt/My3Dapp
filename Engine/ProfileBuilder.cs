using FormaCore.Core;

namespace FormaCore.Engine;

public static class ProfileBuilder
{
    private const double Tolerance = 0.0001d;
    private const int CircleSegments = 64;
    private const int ArcSegments = 24;

    // Converts a closed SketchFeature into a Profile (ordered 2D contour).
    // Returns false with a descriptive error when the sketch cannot form a valid profile.
    public static bool TryBuild(SketchFeature sketch, out Profile profile, out string error)
    {
        profile = new Profile([]);
        error = string.Empty;

        var geometry = GetProfileGeometry(sketch.Entities);

        if (!CanBuildClosedProfile(geometry))
        {
            error = $"{sketch.Name} is not a closed profile.";
            return false;
        }

        if (geometry.Count == 1)
        {
            switch (geometry[0])
            {
                case CadSketchCircle circle:
                    profile = BuildCircleProfile(circle);
                    return true;
                case CadSketchPolygon polygon:
                    profile = BuildPolygonProfile(polygon);
                    return true;
                case CadSketchSlot slot:
                    profile = BuildSlotProfile(slot);
                    return true;
            }
        }

        return TryBuildSegmentLoopProfile(geometry, out profile, out error);
    }

    private static Profile BuildCircleProfile(CadSketchCircle c)
    {
        var points = new Vector2D[CircleSegments];
        for (var i = 0; i < CircleSegments; i++)
        {
            var angle = 2d * Math.PI * i / CircleSegments;
            points[i] = new Vector2D(
                c.CenterX + c.Radius * Math.Cos(angle),
                c.CenterY + c.Radius * Math.Sin(angle));
        }
        return new Profile(points);
    }

    private static Profile BuildPolygonProfile(CadSketchPolygon polygon)
    {
        var n = Math.Max(polygon.Sides, 3);
        var points = new Vector2D[n];
        for (var i = 0; i < n; i++)
        {
            var angle = (2d * Math.PI * i / n) - (Math.PI / 2d);
            points[i] = new Vector2D(
                polygon.CenterX + polygon.Radius * Math.Cos(angle),
                polygon.CenterY + polygon.Radius * Math.Sin(angle));
        }
        return new Profile(points);
    }

    private static Profile BuildSlotProfile(CadSketchSlot slot)
    {
        var dx = slot.Center2X - slot.Center1X;
        var dy = slot.Center2Y - slot.Center1Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var r = Math.Max(slot.Radius, 0.2d);
        const int halfSegs = 16;
        var points = new List<Vector2D>();
        double nx, ny;
        if (length < 0.001d)
        {
            nx = 0d;
            ny = 1d;
        }
        else
        {
            nx = -dy / length;
            ny = dx / length;
        }

        for (var i = 0; i <= halfSegs; i++)
        {
            var theta = Math.PI * i / halfSegs;
            var px = slot.Center2X + (r * ((Math.Cos(theta) * nx) + (Math.Sin(theta) * (dx / Math.Max(length, 0.001d)))));
            var py = slot.Center2Y + (r * ((Math.Cos(theta) * ny) + (Math.Sin(theta) * (dy / Math.Max(length, 0.001d)))));
            points.Add(new Vector2D(px, py));
        }

        for (var i = 0; i <= halfSegs; i++)
        {
            var theta = Math.PI + (Math.PI * i / halfSegs);
            var px = slot.Center1X + (r * ((Math.Cos(theta) * nx) + (Math.Sin(theta) * (dx / Math.Max(length, 0.001d)))));
            var py = slot.Center1Y + (r * ((Math.Cos(theta) * ny) + (Math.Sin(theta) * (dy / Math.Max(length, 0.001d)))));
            points.Add(new Vector2D(px, py));
        }

        return new Profile(points);
    }

    public static bool CanBuildClosedProfile(IReadOnlyList<CadSketchEntity> entities)
    {
        var geometry = GetProfileGeometry(entities);

        if (geometry.Count == 0)
        {
            return false;
        }

        if (geometry.Count == 1)
        {
            return geometry[0] is CadSketchCircle or CadSketchPolygon or CadSketchSlot;
        }

        return TryOrderConnectedSegments(geometry, out _, out _);
    }

    private static List<CadSketchEntity> GetProfileGeometry(IEnumerable<CadSketchEntity> entities)
    {
        return ExpandCompatibleGeometry(entities)
            .Where(entity => !entity.IsConstruction && entity is not CadSketchPoint)
            .ToList();
    }

    private static IEnumerable<CadSketchEntity> ExpandCompatibleGeometry(IEnumerable<CadSketchEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (entity is not CadSketchRectangle rectangle)
            {
                yield return entity;
                continue;
            }

            var minX = rectangle.X;
            var minY = rectangle.Y;
            var maxX = rectangle.X + rectangle.Width;
            var maxY = rectangle.Y + rectangle.Height;

            yield return new CadSketchLine
            {
                StartX = minX,
                StartY = minY,
                EndX = maxX,
                EndY = minY,
                IsConstruction = rectangle.IsConstruction,
                IsFixed = rectangle.IsFixed
            };
            yield return new CadSketchLine
            {
                StartX = maxX,
                StartY = minY,
                EndX = maxX,
                EndY = maxY,
                IsConstruction = rectangle.IsConstruction,
                IsFixed = rectangle.IsFixed
            };
            yield return new CadSketchLine
            {
                StartX = maxX,
                StartY = maxY,
                EndX = minX,
                EndY = maxY,
                IsConstruction = rectangle.IsConstruction,
                IsFixed = rectangle.IsFixed
            };
            yield return new CadSketchLine
            {
                StartX = minX,
                StartY = maxY,
                EndX = minX,
                EndY = minY,
                IsConstruction = rectangle.IsConstruction,
                IsFixed = rectangle.IsFixed
            };
        }
    }

    private static bool TryBuildSegmentLoopProfile(IReadOnlyList<CadSketchEntity> geometry, out Profile profile, out string error)
    {
        profile = new Profile([]);
        error = string.Empty;

        if (geometry.Count < 2)
        {
            error = "Profile needs at least 2 geometry segments.";
            return false;
        }

        if (!TryOrderConnectedSegments(geometry, out var orderedSegments, out error))
        {
            return false;
        }

        var points = new List<Vector2D>();
        foreach (var segment in orderedSegments)
        {
            var sampled = SampleSegment(segment).ToList();
            if (sampled.Count == 0)
            {
                continue;
            }

            if (points.Count == 0)
            {
                points.AddRange(sampled);
                continue;
            }

            if (PointsEqual(points[^1].X, points[^1].Y, sampled[0].X, sampled[0].Y))
            {
                points.AddRange(sampled.Skip(1));
            }
            else
            {
                points.AddRange(sampled);
            }
        }

        if (points.Count > 1 && PointsEqual(points[0].X, points[0].Y, points[^1].X, points[^1].Y))
        {
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count < 3)
        {
            error = "Profile needs at least 3 distinct corners.";
            return false;
        }

        var area = ComputeSignedArea(points);
        if (Math.Abs(area) <= Tolerance)
        {
            error = "Profile collapses to zero area.";
            return false;
        }

        // Normalize to CCW so all downstream code (shell offset, prism winding) sees consistent orientation.
        if (area < 0)
            points.Reverse();

        profile = new Profile(points);
        return true;
    }

    private static bool TryOrderConnectedSegments(
        IReadOnlyList<CadSketchEntity> geometry,
        out List<CadSketchEntity> ordered,
        out string error)
    {
        ordered = [];
        error = string.Empty;

        if (geometry.Any(entity => entity is not CadSketchLine and not CadSketchArc))
        {
            error = "Only connected line/arc loops are supported for this profile.";
            return false;
        }

        var remaining = geometry.ToList();
        ordered.Add(remaining[0]);
        remaining.RemoveAt(0);

        if (!TryGetEndpoints(ordered[0], out var chainStart, out var currentEnd))
        {
            error = "Sketch contains unsupported geometry.";
            return false;
        }

        while (remaining.Count > 0)
        {
            var matched = false;
            for (var index = 0; index < remaining.Count; index++)
            {
                var candidate = remaining[index];
                if (!TryGetEndpoints(candidate, out var candidateStart, out var candidateEnd))
                {
                    continue;
                }

                if (PointsEqual(currentEnd.X, currentEnd.Y, candidateStart.X, candidateStart.Y))
                {
                    ordered.Add(candidate);
                    currentEnd = candidateEnd;
                    remaining.RemoveAt(index);
                    matched = true;
                    break;
                }

                if (PointsEqual(currentEnd.X, currentEnd.Y, candidateEnd.X, candidateEnd.Y))
                {
                    ordered.Add(ReverseSegment(candidate));
                    currentEnd = candidateStart;
                    remaining.RemoveAt(index);
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                error = "Sketch geometry does not form one connected closed loop.";
                return false;
            }
        }

        if (!PointsEqual(currentEnd.X, currentEnd.Y, chainStart.X, chainStart.Y))
        {
            error = "Sketch loop does not close.";
            return false;
        }

        return true;
    }

    private static IEnumerable<Vector2D> SampleSegment(CadSketchEntity entity)
    {
        switch (entity)
        {
            case CadSketchLine line:
                yield return new Vector2D(line.StartX, line.StartY);
                yield return new Vector2D(line.EndX, line.EndY);
                yield break;

            case CadSketchArc arc:
                foreach (var point in SampleArc(arc))
                {
                    yield return point;
                }

                yield break;
        }
    }

    private static IEnumerable<Vector2D> SampleArc(CadSketchArc arc)
    {
        var start = NormalizeDegrees(arc.StartAngleDegrees);
        var end = NormalizeDegrees(arc.EndAngleDegrees);
        var sweep = ComputeSweep(start, end, arc.CounterClockwise);
        var segments = Math.Max(6, (int)Math.Ceiling(Math.Abs(sweep) / 15d));

        for (var i = 0; i <= segments; i++)
        {
            var angle = start + (sweep * i / segments);
            var radians = angle * Math.PI / 180d;
            yield return new Vector2D(
                arc.CenterX + (arc.Radius * Math.Cos(radians)),
                arc.CenterY + (arc.Radius * Math.Sin(radians)));
        }
    }

    private static CadSketchEntity ReverseSegment(CadSketchEntity entity)
    {
        return entity switch
        {
            CadSketchLine line => new CadSketchLine
            {
                Id = line.Id,
                StartX = line.EndX,
                StartY = line.EndY,
                EndX = line.StartX,
                EndY = line.StartY,
                IsConstruction = line.IsConstruction,
                IsFixed = line.IsFixed
            },
            CadSketchArc arc => new CadSketchArc
            {
                Id = arc.Id,
                CenterX = arc.CenterX,
                CenterY = arc.CenterY,
                Radius = arc.Radius,
                StartAngleDegrees = arc.EndAngleDegrees,
                EndAngleDegrees = arc.StartAngleDegrees,
                CounterClockwise = !arc.CounterClockwise,
                IsConstruction = arc.IsConstruction,
                IsFixed = arc.IsFixed
            },
            _ => entity
        };
    }

    private static bool TryGetEndpoints(CadSketchEntity entity, out Vector2D start, out Vector2D end)
    {
        switch (entity)
        {
            case CadSketchLine line:
                start = new Vector2D(line.StartX, line.StartY);
                end = new Vector2D(line.EndX, line.EndY);
                return true;
            case CadSketchArc arc:
                start = ArcPoint(arc, arc.StartAngleDegrees);
                end = ArcPoint(arc, arc.EndAngleDegrees);
                return true;
            default:
                start = default;
                end = default;
                return false;
        }
    }

    private static Vector2D ArcPoint(CadSketchArc arc, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Vector2D(
            arc.CenterX + (arc.Radius * Math.Cos(radians)),
            arc.CenterY + (arc.Radius * Math.Sin(radians)));
    }

    private static double NormalizeDegrees(double angle)
    {
        var normalized = angle % 360d;
        return normalized < 0d ? normalized + 360d : normalized;
    }

    private static double ComputeSweep(double start, double end, bool counterClockwise)
    {
        if (counterClockwise)
        {
            var sweep = end - start;
            return sweep < 0d ? sweep + 360d : sweep;
        }

        var clockwiseSweep = start - end;
        clockwiseSweep = clockwiseSweep < 0d ? clockwiseSweep + 360d : clockwiseSweep;
        return -clockwiseSweep;
    }

    public static PlaneOrientation GetOrientation(string planeName) =>
        planeName.Trim().ToLowerInvariant() switch
        {
            "front" => PlaneOrientation.Front,
            "right" => PlaneOrientation.Right,
            _ => PlaneOrientation.Top
        };

    private static bool PointsEqual(double ax, double ay, double bx, double by) =>
        Math.Abs(ax - bx) <= Tolerance && Math.Abs(ay - by) <= Tolerance;

    private static double ComputeSignedArea(IReadOnlyList<Vector2D> points)
    {
        var area = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }
        return area * 0.5d;
    }
}
