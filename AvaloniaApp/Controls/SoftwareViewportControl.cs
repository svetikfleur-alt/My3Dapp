#pragma warning disable CA1416 // Windows-only GDI+ APIs intentional (desktop-only target)
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Windows.Forms;
using FormaCore.Engine;

namespace My3DApp.AvaloniaApp.Controls;

internal sealed class SoftwareViewportControl : Control
{
    private readonly List<HitProxy> _hitProxies = [];
    private Point _lastMousePoint;
    private Point _mouseDownPoint;
    private bool _orbiting;
    private bool _panning;
    private bool _pendingSelection;
    private bool _gridVisible;
    private float _yaw = -0.68f;
    private float _pitch = 0.96f;
    private float _distance = 48f;
    private Vector3 _pivot = Vector3.Zero;
    private StudioThemeMode _themeMode = StudioThemeMode.Light;

    public SoftwareViewportControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(232, 237, 244);
        Cursor = Cursors.Cross;
    }

    public ViewportRenderState? SceneState { get; private set; }

    public event Action<CadEntityKind, Guid>? EntitySelected;

    public event EventHandler<ViewportSketchPlacementEventArgs>? SketchPlacementRequested;

    public event EventHandler<ViewportSketchPreviewEventArgs>? SketchPreviewRequested;

    public event EventHandler? SketchPreviewCleared;

    public void SetTheme(StudioThemeMode mode)
    {
        _themeMode = mode;
        BackColor = mode == StudioThemeMode.Dark
            ? Color.FromArgb(32, 38, 47)
            : Color.FromArgb(232, 237, 244);
        Invalidate();
    }

    public void SetScene(ViewportRenderState state)
    {
        SceneState = state;
        Invalidate();
    }

    public void SetGridVisible(bool visible)
    {
        _gridVisible = visible;
        Invalidate();
    }

    public void FocusSelection()
    {
        if (SceneState is null)
        {
            return;
        }

        if (SceneState.SelectedBodyId is Guid bodyId)
        {
            var body = SceneState.Bodies.FirstOrDefault(item => item.BodyId == bodyId);
            if (body is not null && TryGetBodyBounds(body, out var min, out var max))
            {
                _pivot = (min + max) * 0.5f;
                var extent = max - min;
                var radius = MathF.Max(8f, extent.Length() * 0.5f);
                _distance = Math.Clamp(radius * 4.2f, 28f, 1600f);
                Invalidate();
                return;
            }
        }

        _pivot = Vector3.Zero;
        _distance = 48f;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _lastMousePoint = e.Location;
        _mouseDownPoint = e.Location;

        if (e.Button == MouseButtons.Left)
        {
            _pendingSelection = true;
        }
        else if (e.Button == MouseButtons.Right)
        {
            _orbiting = true;
            Cursor = Cursors.SizeAll;
        }
        else if (e.Button == MouseButtons.Middle)
        {
            _panning = true;
            Cursor = Cursors.Hand;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var dx = e.X - _lastMousePoint.X;
        var dy = e.Y - _lastMousePoint.Y;
        _lastMousePoint = e.Location;

        if (_pendingSelection && (Math.Abs(e.X - _mouseDownPoint.X) > 4 || Math.Abs(e.Y - _mouseDownPoint.Y) > 4))
        {
            _pendingSelection = false;
        }

        if (_orbiting)
        {
            _yaw += dx * 0.01f;
            _pitch += dy * 0.01f;
            NormalizeAngles();
            Invalidate();
            return;
        }

        if (_panning)
        {
            PanByPixels(dx, dy);
            Invalidate();
            return;
        }

        if (TryHandleSketchPreview(e.Location))
        {
            return;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left && _pendingSelection)
        {
            if (TryHandleSketchPlacement(e.Location))
            {
                _pendingSelection = false;
                Cursor = Cursors.Cross;
                return;
            }

            var hit = HitTest(e.Location);
            if (hit is not null)
            {
                EntitySelected?.Invoke(hit.Value.Kind, hit.Value.EntityId);
            }
        }

        _pendingSelection = false;
        _orbiting = false;
        _panning = false;
        Cursor = Cursors.Cross;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        var forward = Vector3.Normalize(CameraVectorToWorld(new Vector3(0f, 1f, 0f)));
        BuildRay(e.Location, out var beforeOrigin, out var beforeDirection);
        var anchorBefore = TryIntersectRayWithPlane(beforeOrigin, beforeDirection, _pivot, forward, out var beforePoint)
            ? beforePoint
            : _pivot;

        var notchCount = e.Delta / 120f;
        var step = MathF.Max(1f, _distance * 0.09f);
        _distance = Math.Clamp(_distance - notchCount * step, 18f, 3200f);

        BuildRay(e.Location, out var afterOrigin, out var afterDirection);
        if (TryIntersectRayWithPlane(afterOrigin, afterDirection, _pivot, forward, out var afterPoint))
        {
            _pivot += anchorBefore - afterPoint;
        }

        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (SceneState is null || !string.Equals(SceneState.Mode, "Sketch", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SketchPreviewCleared?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode != Keys.Escape)
        {
            return;
        }

        if (SceneState is null || !string.Equals(SceneState.Mode, "Sketch", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SketchPreviewCleared?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        _hitProxies.Clear();

        DrawBackdrop(e.Graphics);
        DrawPlanes(e.Graphics);
        if (_gridVisible) DrawSketchGrid(e.Graphics);
        DrawBodies(e.Graphics);
        DrawSketches(e.Graphics);
        DrawOriginMarker(e.Graphics);
        DrawHud(e.Graphics);
    }

    private void DrawSketchGrid(Graphics graphics)
    {
        if (SceneState is null || !string.Equals(SceneState.Mode, "Sketch", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var plane = ResolveSketchPlane();
        if (plane is null)
        {
            return;
        }

        var planeNormal = plane.Kind.Trim().ToLowerInvariant() switch
        {
            "front" => new Vector3(1f, 0f, 0f),
            "right" => new Vector3(0f, 1f, 0f),
            _ => new Vector3(0f, 0f, 1f)
        };

        var screenCorners = new[]
        {
            new Point(0, 0), new Point(ClientSize.Width, 0),
            new Point(0, ClientSize.Height), new Point(ClientSize.Width, ClientSize.Height)
        };

        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;
        foreach (var corner in screenCorners)
        {
            BuildRay(corner, out var ro, out var rd);
            if (!TryIntersectRayWithPlane(ro, rd, Vector3.Zero, planeNormal, out var hit))
            {
                continue;
            }

            var (u, v) = plane.Kind.Trim().ToLowerInvariant() switch
            {
                "front" => (hit.Y, hit.Z),
                "right" => (hit.X, hit.Z),
                _ => (hit.X, hit.Y)
            };
            if (u < minU) minU = u;
            if (u > maxU) maxU = u;
            if (v < minV) minV = v;
            if (v > maxV) maxV = v;
        }

        if (minU >= maxU || minV >= maxV)
        {
            return;
        }

        var worldA = plane.Kind.Trim().ToLowerInvariant() switch
        {
            "front" => new Vector3(0f, minU, minV),
            "right" => new Vector3(minU, 0f, minV),
            _ => new Vector3(minU, minV, 0f)
        };
        var worldB = plane.Kind.Trim().ToLowerInvariant() switch
        {
            "front" => new Vector3(0f, minU + 1f, minV),
            "right" => new Vector3(minU + 1f, 0f, minV),
            _ => new Vector3(minU + 1f, minV, 0f)
        };

        var pA = Project(worldA);
        var pB = Project(worldB);
        var pxPerUnit = (pA.IsValid && pB.IsValid)
            ? MathF.Sqrt(MathF.Pow(pB.Point.X - pA.Point.X, 2) + MathF.Pow(pB.Point.Y - pA.Point.Y, 2))
            : 20f;

        float[] spacings = [0.1f, 0.2f, 0.5f, 1f, 2f, 5f, 10f, 20f, 50f, 100f, 200f, 500f];
        var spacing = 1f;
        foreach (var s in spacings)
        {
            if (pxPerUnit * s >= 40f)
            {
                spacing = s;
                break;
            }
        }

        var gridAlpha = 26;
        var gridColor = _themeMode == StudioThemeMode.Dark
            ? Color.FromArgb(gridAlpha, 200, 215, 240)
            : Color.FromArgb(gridAlpha, 50, 70, 100);
        using var gridPen = new Pen(gridColor, 1f);

        var startU = MathF.Ceiling(minU / spacing) * spacing;
        var startV = MathF.Ceiling(minV / spacing) * spacing;
        var extU = maxU + 1f;
        var extV = maxV + 1f;

        for (var u = startU; u <= extU; u += spacing)
        {
            var wa = plane.Kind.Trim().ToLowerInvariant() switch
            {
                "front" => new Vector3(0f, u, minV),
                "right" => new Vector3(u, 0f, minV),
                _ => new Vector3(u, minV, 0f)
            };
            var wb = plane.Kind.Trim().ToLowerInvariant() switch
            {
                "front" => new Vector3(0f, u, maxV),
                "right" => new Vector3(u, 0f, maxV),
                _ => new Vector3(u, maxV, 0f)
            };
            var pa2 = Project(wa);
            var pb2 = Project(wb);
            if (pa2.IsValid && pb2.IsValid)
            {
                graphics.DrawLine(gridPen, pa2.Point, pb2.Point);
            }
        }

        for (var v = startV; v <= extV; v += spacing)
        {
            var wa = plane.Kind.Trim().ToLowerInvariant() switch
            {
                "front" => new Vector3(0f, minU, v),
                "right" => new Vector3(minU, 0f, v),
                _ => new Vector3(minU, v, 0f)
            };
            var wb = plane.Kind.Trim().ToLowerInvariant() switch
            {
                "front" => new Vector3(0f, maxU, v),
                "right" => new Vector3(maxU, 0f, v),
                _ => new Vector3(maxU, v, 0f)
            };
            var pa2 = Project(wa);
            var pb2 = Project(wb);
            if (pa2.IsValid && pb2.IsValid)
            {
                graphics.DrawLine(gridPen, pa2.Point, pb2.Point);
            }
        }
    }

    private void DrawBackdrop(Graphics graphics)
    {
        var top = _themeMode == StudioThemeMode.Dark
            ? Color.FromArgb(40, 47, 58)
            : Color.FromArgb(244, 247, 251);
        var bottom = _themeMode == StudioThemeMode.Dark
            ? Color.FromArgb(30, 36, 45)
            : Color.FromArgb(232, 237, 244);

        using var brush = new LinearGradientBrush(ClientRectangle, top, bottom, LinearGradientMode.Vertical);
        graphics.FillRectangle(brush, ClientRectangle);
    }

    private void DrawPlanes(Graphics graphics)
    {
        if (SceneState is null)
        {
            return;
        }

        const float planeSize = 12f;

        foreach (var plane in SceneState.Planes.Where(item => item.Visible))
        {
            var corners = GetPlaneCorners(plane.Kind, planeSize);
            var projected = new ProjectedVertex[4];
            var canDraw = true;

            for (var i = 0; i < corners.Length; i++)
            {
                projected[i] = Project(corners[i]);
                if (!projected[i].IsValid)
                {
                    canDraw = false;
                    break;
                }
            }

            if (!canDraw)
            {
                continue;
            }

            var selected = SceneState.SelectedPlaneId == plane.PlaneId;
            var baseColor = PlaneColor(plane.Kind);
            var fillAlpha = selected ? 88 : 62;
            var edgeAlpha = selected ? 160 : 108;

            using var fill = new SolidBrush(Color.FromArgb(fillAlpha, baseColor));
            using var pen = new Pen(Color.FromArgb(edgeAlpha, baseColor), selected ? 1.8f : 1.1f);

            var polygon = new[] { projected[0].Point, projected[1].Point, projected[2].Point, projected[3].Point };
            graphics.FillPolygon(fill, polygon);
            graphics.DrawPolygon(pen, polygon);

            using var font = new Font("Segoe UI Semibold", 8f);
            using var textBrush = new SolidBrush(Color.FromArgb(selected ? 188 : 140, baseColor));
            var center = new PointF(
                (projected[0].Point.X + projected[2].Point.X) * 0.5f + 6f,
                (projected[0].Point.Y + projected[2].Point.Y) * 0.5f - 6f);
            graphics.DrawString(plane.Name, font, textBrush, center);

            AddHitTriangle(CadEntityKind.ReferencePlane, plane.PlaneId, projected[0], projected[1], projected[2]);
            AddHitTriangle(CadEntityKind.ReferencePlane, plane.PlaneId, projected[0], projected[2], projected[3]);
        }
    }

    private void DrawBodies(Graphics graphics)
    {
        if (SceneState is null)
        {
            return;
        }

        var triangles = new List<BodyTriangle>();
        var lightDirection = Vector3.Normalize(new Vector3(-0.42f, -0.58f, 0.69f));

        foreach (var body in SceneState.Bodies)
        {
            var baseColor = BodyColor(body.Kind);
            if (SceneState.SelectedBodyId == body.BodyId)
            {
                baseColor = Color.FromArgb(
                    Math.Min(255, baseColor.R + 16),
                    Math.Min(255, baseColor.G + 22),
                    Math.Min(255, baseColor.B + 34));
            }

            var translation = new Vector3((float)body.X, (float)body.Y, (float)body.Z);
            for (var index = 0; index + 2 < body.Indices.Length; index += 3)
            {
                var ia = body.Indices[index] * 3;
                var ib = body.Indices[index + 1] * 3;
                var ic = body.Indices[index + 2] * 3;
                if (ic + 2 >= body.Positions.Length)
                {
                    continue;
                }

                var a = translation + new Vector3((float)body.Positions[ia], (float)body.Positions[ia + 1], (float)body.Positions[ia + 2]);
                var b = translation + new Vector3((float)body.Positions[ib], (float)body.Positions[ib + 1], (float)body.Positions[ib + 2]);
                var c = translation + new Vector3((float)body.Positions[ic], (float)body.Positions[ic + 1], (float)body.Positions[ic + 2]);

                var pa = Project(a);
                var pb = Project(b);
                var pc = Project(c);
                if (!pa.IsValid || !pb.IsValid || !pc.IsValid)
                {
                    continue;
                }

                var normal = Vector3.Cross(b - a, c - a);
                if (normal.LengthSquared() < 1e-6f)
                {
                    continue;
                }

                normal = Vector3.Normalize(normal);
                var diffuse = MathF.Max(0f, Vector3.Dot(normal, lightDirection));
                var shade = 0.34f + diffuse * 0.66f;
                var shadedColor = Color.FromArgb(
                    255,
                    (int)Math.Clamp(baseColor.R * shade, 0f, 255f),
                    (int)Math.Clamp(baseColor.G * shade, 0f, 255f),
                    (int)Math.Clamp(baseColor.B * shade, 0f, 255f));

                triangles.Add(new BodyTriangle(
                    body.BodyId,
                    pa.Point,
                    pb.Point,
                    pc.Point,
                    (pa.Camera.Depth + pb.Camera.Depth + pc.Camera.Depth) / 3f,
                    shadedColor));
            }
        }

        foreach (var triangle in triangles.OrderByDescending(item => item.Depth))
        {
            using var brush = new SolidBrush(triangle.Color);
            graphics.FillPolygon(brush, [triangle.A, triangle.B, triangle.C]);
            AddHitTriangle(CadEntityKind.Body, triangle.BodyId, triangle.A, triangle.B, triangle.C, triangle.Depth);
        }
    }

    private void DrawSketches(Graphics graphics)
    {
        if (SceneState is null || SceneState.Sketches.Count == 0)
        {
            return;
        }

        foreach (var sketch in SceneState.Sketches)
        {
            var isSelected = SceneState.SelectedSketchId == sketch.SketchId;
            var lineColor = SketchColor(sketch, isSelected);
            var lineWidth = sketch.IsPreview ? 1.45f : (sketch.IsDraft || isSelected ? 2.2f : 1.6f);

            foreach (var curve in sketch.Curves)
            {
                var curveColor = ResolveSketchCurveColor(sketch, curve, lineColor);
                if (string.Equals(curve.Kind, "point", StringComparison.OrdinalIgnoreCase))
                {
                    DrawSketchPoint(graphics, sketch, curve, curveColor);
                    continue;
                }

                var points = ProjectPolyline(curve.Points);
                if (points.Length < 2)
                {
                    continue;
                }

                using var pen = new Pen(curveColor, lineWidth)
                {
                    LineJoin = LineJoin.Round,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };

                if (!sketch.IsPreview && curve.IsConstruction)
                {
                    pen.DashStyle = DashStyle.Dash;
                    pen.DashPattern = [5f, 4f];
                }

                graphics.DrawLines(pen, points);
            }
        }
    }

    private Color ResolveSketchCurveColor(ViewportRenderSketch sketch, ViewportRenderSketchCurve curve, Color baseColor)
    {
        if (sketch.IsPreview)
        {
            return Color.FromArgb(
                150,
                Math.Min(255, baseColor.R + 22),
                Math.Min(255, baseColor.G + 22),
                Math.Min(255, baseColor.B + 22));
        }

        if (curve.IsConstruction)
        {
            return _themeMode == StudioThemeMode.Dark
                ? Color.FromArgb(172, 122, 184, 224)
                : Color.FromArgb(170, 58, 123, 191);
        }

        return baseColor;
    }

    private void DrawOriginMarker(Graphics graphics)
    {
        var origin = Project(Vector3.Zero);
        if (!origin.IsValid)
        {
            return;
        }

        var center = origin.Point;
        var ring = _themeMode == StudioThemeMode.Dark ? Color.FromArgb(238, 244, 255) : Color.FromArgb(34, 50, 68);
        var fill = _themeMode == StudioThemeMode.Dark ? Color.FromArgb(35, 48, 61) : Color.White;
        var dot = _themeMode == StudioThemeMode.Dark ? Color.FromArgb(238, 244, 255) : Color.FromArgb(34, 50, 68);
        var shadow = _themeMode == StudioThemeMode.Dark ? Color.FromArgb(40, 9, 13, 18) : Color.FromArgb(32, 34, 50, 68);

        using var shadowBrush = new SolidBrush(shadow);
        using var ringBrush = new SolidBrush(ring);
        using var fillBrush = new SolidBrush(fill);
        using var dotBrush = new SolidBrush(dot);

        graphics.FillEllipse(shadowBrush, center.X - 10f, center.Y - 10f, 20f, 20f);
        graphics.FillEllipse(ringBrush, center.X - 8f, center.Y - 8f, 16f, 16f);
        graphics.FillEllipse(fillBrush, center.X - 5.2f, center.Y - 5.2f, 10.4f, 10.4f);
        graphics.FillEllipse(dotBrush, center.X - 2.1f, center.Y - 2.1f, 4.2f, 4.2f);
    }

    private void DrawHud(Graphics graphics)
    {
        if (SceneState is null)
        {
            return;
        }

        using var titleFont = new Font("Segoe UI Semibold", 8.5f);
        using var bodyFont = new Font("Segoe UI", 8f);
        using var brush = new SolidBrush(_themeMode == StudioThemeMode.Dark
            ? Color.FromArgb(190, 215, 223, 236)
            : Color.FromArgb(172, 74, 82, 95));

        var title = $"Mode: {SceneState.Mode}";
        var subtitle = $"Active plane: {SceneState.ActivePlaneName}   Orbit: Right drag   Pan: Middle drag   Zoom: Mouse wheel";
        graphics.DrawString(title, titleFont, brush, 12, 10);
        graphics.DrawString(subtitle, bodyFont, brush, 12, 28);
    }

    private void PanByPixels(float dx, float dy)
    {
        var panScale = _distance / Math.Max(220f, Math.Min(ClientSize.Width, ClientSize.Height));
        var right = GetRightAxis();
        var up = GetScreenUpAxis();

        _pivot -= right * (dx * panScale);
        _pivot += up * (dy * panScale);
    }

    private void NormalizeAngles()
    {
        const float tau = MathF.PI * 2f;
        if (_yaw > tau || _yaw < -tau)
        {
            _yaw = MathF.IEEERemainder(_yaw, tau);
        }

        if (_pitch > tau || _pitch < -tau)
        {
            _pitch = MathF.IEEERemainder(_pitch, tau);
        }
    }

    private HitProxy? HitTest(Point location)
    {
        HitProxy? best = null;
        foreach (var proxy in _hitProxies)
        {
            if (!PointInTriangle(location, proxy.A, proxy.B, proxy.C))
            {
                continue;
            }

            if (best is null || proxy.Depth < best.Value.Depth)
            {
                best = proxy;
            }
        }

        return best;
    }

    private bool TryHandleSketchPlacement(Point location)
    {
        if (SceneState is null || !string.Equals(SceneState.Mode, "Sketch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var plane = ResolveSketchPlane();
        if (plane is null)
        {
            return false;
        }

        BuildRay(location, out var rayOrigin, out var rayDirection);
        var planeNormal = plane.Kind.Trim().ToLowerInvariant() switch
        {
            "front" => new Vector3(1f, 0f, 0f),
            "right" => new Vector3(0f, 1f, 0f),
            _ => new Vector3(0f, 0f, 1f)
        };

        if (!TryIntersectRayWithPlane(rayOrigin, rayDirection, Vector3.Zero, planeNormal, out var hitPoint))
        {
            return false;
        }

        var (u, v) = plane.Kind.Trim().ToLowerInvariant() switch
        {
            "front" => (hitPoint.Y, hitPoint.Z),
            "right" => (hitPoint.X, hitPoint.Z),
            _ => (hitPoint.X, hitPoint.Y)
        };

        SketchPlacementRequested?.Invoke(
            this,
            new ViewportSketchPlacementEventArgs(plane.PlaneId, plane.Kind, u, v));
        return true;
    }

    private bool TryHandleSketchPreview(Point location)
    {
        if (SceneState is null || !string.Equals(SceneState.Mode, "Sketch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var plane = ResolveSketchPlane();
        if (plane is null)
        {
            return false;
        }

        BuildRay(location, out var rayOrigin, out var rayDirection);
        var planeNormal = plane.Kind.Trim().ToLowerInvariant() switch
        {
            "front" => new Vector3(1f, 0f, 0f),
            "right" => new Vector3(0f, 1f, 0f),
            _ => new Vector3(0f, 0f, 1f)
        };

        if (!TryIntersectRayWithPlane(rayOrigin, rayDirection, Vector3.Zero, planeNormal, out var hitPoint))
        {
            return false;
        }

        var (u, v) = plane.Kind.Trim().ToLowerInvariant() switch
        {
            "front" => (hitPoint.Y, hitPoint.Z),
            "right" => (hitPoint.X, hitPoint.Z),
            _ => (hitPoint.X, hitPoint.Y)
        };

        SketchPreviewRequested?.Invoke(
            this,
            new ViewportSketchPreviewEventArgs(plane.PlaneId, plane.Kind, u, v));
        return true;
    }

    private ViewportRenderPlane? ResolveSketchPlane()
    {
        if (SceneState is null)
        {
            return null;
        }

        if (SceneState.SelectedPlaneId is Guid selectedPlaneId)
        {
            var selected = SceneState.Planes.FirstOrDefault(item => item.PlaneId == selectedPlaneId);
            if (selected is not null)
            {
                return selected;
            }
        }

        return SceneState.Planes.FirstOrDefault(item =>
            string.Equals(item.Name, SceneState.ActivePlaneName, StringComparison.OrdinalIgnoreCase));
    }

    private void AddHitTriangle(CadEntityKind kind, Guid entityId, ProjectedVertex a, ProjectedVertex b, ProjectedVertex c)
    {
        AddHitTriangle(kind, entityId, a.Point, b.Point, c.Point, (a.Camera.Depth + b.Camera.Depth + c.Camera.Depth) / 3f);
    }

    private void AddHitTriangle(CadEntityKind kind, Guid entityId, PointF a, PointF b, PointF c, float depth)
    {
        _hitProxies.Add(new HitProxy(kind, entityId, a, b, c, depth));
    }

    private void BuildRay(Point location, out Vector3 origin, out Vector3 direction)
    {
        var centerX = ClientSize.Width * 0.5f;
        var centerY = ClientSize.Height * 0.52f;
        var projectionScale = ProjectionScale;

        var sx = location.X - centerX;
        var sy = centerY - location.Y;

        origin = CameraToWorldPoint(new Vector3(0f, -_distance, 0f));
        direction = Vector3.Normalize(
            CameraVectorToWorld(new Vector3(sx / projectionScale, 1f, sy / projectionScale)));
    }

    private static bool TryIntersectRayWithPlane(Vector3 rayOrigin, Vector3 rayDirection, Vector3 planePoint, Vector3 planeNormal, out Vector3 hitPoint)
    {
        hitPoint = default;
        var denominator = Vector3.Dot(planeNormal, rayDirection);
        if (MathF.Abs(denominator) < 1e-6f)
        {
            return false;
        }

        var t = Vector3.Dot(planePoint - rayOrigin, planeNormal) / denominator;
        if (t <= 0f)
        {
            return false;
        }

        hitPoint = rayOrigin + rayDirection * t;
        return true;
    }

    private static bool TryGetBodyBounds(ViewportRenderBody body, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        if (body.Positions.Length < 3)
        {
            return false;
        }

        var translation = new Vector3((float)body.X, (float)body.Y, (float)body.Z);
        for (var i = 0; i + 2 < body.Positions.Length; i += 3)
        {
            var point = translation + new Vector3((float)body.Positions[i], (float)body.Positions[i + 1], (float)body.Positions[i + 2]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return true;
    }

    private static bool PointInTriangle(Point p, PointF a, PointF b, PointF c)
    {
        var area = Edge(a, b, c);
        if (Math.Abs(area) < 0.0001f)
        {
            return false;
        }

        var w1 = Edge(p, b, c);
        var w2 = Edge(a, p, c);
        var w3 = Edge(a, b, p);
        var hasNegative = (w1 < 0f) || (w2 < 0f) || (w3 < 0f);
        var hasPositive = (w1 > 0f) || (w2 > 0f) || (w3 > 0f);
        return !(hasNegative && hasPositive);
    }

    private static float Edge(PointF a, PointF b, PointF c) => (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);
    private static float Edge(Point p, PointF b, PointF c) => ((c.X - p.X) * (b.Y - p.Y)) - ((c.Y - p.Y) * (b.X - p.X));
    private static float Edge(PointF a, Point p, PointF c) => ((c.X - a.X) * (p.Y - a.Y)) - ((c.Y - a.Y) * (p.X - a.X));
    private static float Edge(PointF a, PointF b, Point p) => ((p.X - a.X) * (b.Y - a.Y)) - ((p.Y - a.Y) * (b.X - a.X));

    private ProjectedVertex Project(Vector3 world)
    {
        var camera = Transform(world);
        var denominator = _distance + camera.Depth;
        if (denominator <= 4f)
        {
            return ProjectedVertex.Invalid;
        }

        var scale = ProjectionScale / denominator;
        return new ProjectedVertex(
            new PointF(
                ClientSize.Width * 0.5f + camera.X * scale,
                ClientSize.Height * 0.52f - camera.Z * scale),
            camera,
            true);
    }

    private CameraVertex Transform(Vector3 world)
    {
        var local = world - _pivot;
        var cosYaw = MathF.Cos(_yaw);
        var sinYaw = MathF.Sin(_yaw);
        var cosPitch = MathF.Cos(_pitch);
        var sinPitch = MathF.Sin(_pitch);

        var x1 = cosYaw * local.X - sinYaw * local.Y;
        var y1 = sinYaw * local.X + cosYaw * local.Y;
        var z1 = local.Z;

        var depth = cosPitch * y1 - sinPitch * z1;
        var z2 = sinPitch * y1 + cosPitch * z1;
        return new CameraVertex(x1, depth, z2);
    }

    private Vector3 CameraToWorldPoint(Vector3 cameraPoint) => _pivot + CameraVectorToWorld(cameraPoint);

    private Vector3 CameraVectorToWorld(Vector3 cameraVector)
    {
        var cosYaw = MathF.Cos(_yaw);
        var sinYaw = MathF.Sin(_yaw);
        var cosPitch = MathF.Cos(_pitch);
        var sinPitch = MathF.Sin(_pitch);

        var y1 = cosPitch * cameraVector.Y + sinPitch * cameraVector.Z;
        var z = -sinPitch * cameraVector.Y + cosPitch * cameraVector.Z;

        var x = cosYaw * cameraVector.X + sinYaw * y1;
        var y = -sinYaw * cameraVector.X + cosYaw * y1;
        return new Vector3(x, y, z);
    }

    private float ProjectionScale => Math.Max(100f, Math.Min(ClientSize.Width, ClientSize.Height) * 1.18f);

    private Vector3 GetRightAxis()
    {
        var right = new Vector3(MathF.Cos(_yaw), -MathF.Sin(_yaw), 0f);
        return right.LengthSquared() < 0.0001f ? Vector3.UnitX : Vector3.Normalize(right);
    }

    private Vector3 GetScreenUpAxis()
    {
        var up = new Vector3(
            MathF.Sin(_pitch) * MathF.Sin(_yaw),
            MathF.Sin(_pitch) * MathF.Cos(_yaw),
            MathF.Cos(_pitch));
        return up.LengthSquared() < 0.0001f ? Vector3.UnitZ : Vector3.Normalize(up);
    }

    private static Vector3[] GetPlaneCorners(string planeKind, float size)
    {
        return planeKind.Trim().ToLowerInvariant() switch
        {
            "front" => [new Vector3(0, -size, -size), new Vector3(0, size, -size), new Vector3(0, size, size), new Vector3(0, -size, size)],
            "right" => [new Vector3(-size, 0, -size), new Vector3(size, 0, -size), new Vector3(size, 0, size), new Vector3(-size, 0, size)],
            _ => [new Vector3(-size, -size, 0), new Vector3(size, -size, 0), new Vector3(size, size, 0), new Vector3(-size, size, 0)]
        };
    }

    private static Color PlaneColor(string planeKind) => planeKind.Trim().ToLowerInvariant() switch
    {
        "top" => Color.FromArgb(143, 165, 207),
        "front" => Color.FromArgb(151, 187, 166),
        "right" => Color.FromArgb(201, 162, 162),
        _ => Color.FromArgb(183, 193, 210)
    };

    private static Color BodyColor(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "box" => Color.FromArgb(145, 164, 190),
        "sphere" => Color.FromArgb(147, 163, 193),
        "cylinder" => Color.FromArgb(143, 162, 189),
        "cone" => Color.FromArgb(145, 162, 187),
        "torus" => Color.FromArgb(143, 155, 183),
        "pyramid" => Color.FromArgb(154, 166, 191),
        "wedge" => Color.FromArgb(155, 170, 193),
        "ellipsoid" => Color.FromArgb(145, 167, 196),
        "capsule" => Color.FromArgb(150, 168, 191),
        "hemisphere" => Color.FromArgb(143, 167, 184),
        "prism" => Color.FromArgb(160, 170, 189),
        "arrow" => Color.FromArgb(147, 160, 176),
        "icosphere" => Color.FromArgb(142, 161, 184),
        "tetrahedron" => Color.FromArgb(153, 164, 181),
        "octahedron" => Color.FromArgb(143, 152, 173),
        "icosahedron" => Color.FromArgb(151, 161, 182),
        _ => Color.FromArgb(144, 164, 191)
    };

    private Color SketchColor(ViewportRenderSketch sketch, bool selected)
    {
        if (sketch.IsPreview)
        {
            return _themeMode == StudioThemeMode.Dark
                ? Color.FromArgb(150, 205, 236, 255)
                : Color.FromArgb(150, 98, 146, 214);
        }

        if (selected)
        {
            return _themeMode == StudioThemeMode.Dark
                ? Color.FromArgb(90, 170, 255)
                : Color.FromArgb(0, 120, 212);
        }

        if (sketch.IsDraft)
        {
            return _themeMode == StudioThemeMode.Dark
                ? Color.FromArgb(165, 205, 255)
                : Color.FromArgb(62, 122, 196);
        }

        return _themeMode == StudioThemeMode.Dark
            ? Color.FromArgb(184, 196, 214)
            : Color.FromArgb(95, 112, 139);
    }

    private void DrawSketchPoint(Graphics graphics, ViewportRenderSketch sketch, ViewportRenderSketchCurve curve, Color lineColor)
    {
        if (curve.Points.Length < 3)
        {
            return;
        }

        var projected = Project(new Vector3(
            (float)curve.Points[0],
            (float)curve.Points[1],
            (float)curve.Points[2]));
        if (!projected.IsValid)
        {
            return;
        }

        var center = projected.Point;
        var markerSize = sketch.IsPreview ? 6.0f : 7.0f;
        using var markerBrush = new SolidBrush(Color.FromArgb(
            sketch.IsPreview ? 150 : (curve.IsConstruction ? 178 : 235),
            lineColor.R,
            lineColor.G,
            lineColor.B));
        using var outlinePen = new Pen(_themeMode == StudioThemeMode.Dark
            ? Color.FromArgb(160, 238, 242, 247)
            : Color.FromArgb(120, 30, 40, 54), 0.75f);

        graphics.FillEllipse(markerBrush, center.X - (markerSize * 0.5f), center.Y - (markerSize * 0.5f), markerSize, markerSize);
        graphics.DrawEllipse(outlinePen, center.X - (markerSize * 0.5f), center.Y - (markerSize * 0.5f), markerSize, markerSize);
    }

    private PointF[] ProjectPolyline(double[] positions)
    {
        var points = new List<PointF>();
        for (var index = 0; index + 2 < positions.Length; index += 3)
        {
            var projected = Project(new Vector3(
                (float)positions[index],
                (float)positions[index + 1],
                (float)positions[index + 2]));
            if (projected.IsValid)
            {
                points.Add(projected.Point);
            }
        }

        return points.ToArray();
    }

    private readonly record struct CameraVertex(float X, float Depth, float Z);
    private readonly record struct ProjectedVertex(PointF Point, CameraVertex Camera, bool IsValid)
    {
        public static ProjectedVertex Invalid => new(default, default, false);
    }

    private readonly record struct HitProxy(CadEntityKind Kind, Guid EntityId, PointF A, PointF B, PointF C, float Depth);
    private readonly record struct BodyTriangle(Guid BodyId, PointF A, PointF B, PointF C, float Depth, Color Color);
}
