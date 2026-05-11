using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using FormaCore.Core;

namespace My3DApp.Studio;

public sealed class StudioViewportControl : Control
{
    private readonly CadDesignCompiler _designCompiler = new();
    private readonly List<HitProxy> _hitProxies = new();
    private readonly List<AnalyticBody> _analyticBodies = new();
    private Bitmap? _bodyBitmap;
    private int[]? _bodyFrameBuffer;
    private Point _lastMousePoint;
    private Point _mouseDownPoint;
    private bool _orbiting;
    private bool _panning;
    private bool _pendingSelection;
    private float _yaw = -0.68f;
    private float _pitch = 0.96f;
    private float _distance = 230f;
    private Vector3 _pivot = Vector3.Zero;
    private object? _hoverNode;
    private object? _selectedNode;
    private int _viewCubeHoverFace = -1;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CadDocument? Document { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (!ReferenceEquals(_selectedNode, value))
            {
                _selectedNode = value;
                Invalidate();
            }
        }
    }

    public event Action<object?>? EntitySelected;

    public StudioViewportControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = StudioTheme.SurfacePrimary;
        Cursor = Cursors.Cross;
    }

    public object? PeekEntityAt(Point location) => HitTest(location);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bodyBitmap?.Dispose();
            _bodyBitmap = null;
            _bodyFrameBuffer = null;
        }

        base.Dispose(disposing);
    }

    public void FocusOnNode(object? node)
    {
        if (Document is null)
        {
            return;
        }

        if (node is CadReferencePlane)
        {
            _pivot = Vector3.Zero;
            _distance = 230f;
            Invalidate();
            return;
        }

        if (node is not CadBody body)
        {
            return;
        }

        try
        {
            var displayBody = _designCompiler.BuildDisplayBody(body);
            var hasBounds = false;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            TryGetSolidBounds(displayBody.Solid, Vector3.Zero, ref hasBounds, ref min, ref max);

            if (!hasBounds)
            {
                return;
            }

            _pivot = (min + max) * 0.5f;
            var extent = max - min;
            var radius = MathF.Max(8f, extent.Length() * 0.5f);
            _distance = Math.Clamp(radius * 4.2f, 40f, 1600f);
            Invalidate();
        }
        catch
        {
            // Focus is optional behavior. Ignore if body cannot be built yet.
        }
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
            _viewCubeHoverFace = -1;
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

        if (e.Button == MouseButtons.None)
        {
            var hover = HitTest(e.Location);
            if (!ReferenceEquals(_hoverNode, hover))
            {
                _hoverNode = hover;
                Invalidate();
            }

            var vcFace = HitTestViewCube(e.Location);
            if (vcFace != _viewCubeHoverFace)
            {
                _viewCubeHoverFace = vcFace;
                Invalidate();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left && _pendingSelection)
        {
            var vcFace = HitTestViewCube(e.Location);
            if (vcFace >= 0)
            {
                SnapToViewCubeFace(vcFace);
            }
            else
            {
                var hit = HitTest(e.Location);
                SelectedNode = hit;
                EntitySelected?.Invoke(hit);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        DrawBackdrop(e.Graphics);
        _hitProxies.Clear();

        if (Document is null)
        {
            DrawHud(e.Graphics);
            return;
        }

        DrawReferencePlanes(e.Graphics);
        DrawAxes(e.Graphics);
        DrawBodies(e.Graphics);
        DrawViewCube(e.Graphics);
        DrawScaleRuler(e.Graphics);
        DrawHud(e.Graphics);
    }

    private void DrawReferencePlanes(Graphics graphics)
    {
        if (Document is null)
        {
            return;
        }

        const float planeSize = 21f;

        foreach (var plane in Document.Scene.ReferencePlanes.Where(plane => plane.Visible))
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

            var isSelected = ReferenceEquals(plane, SelectedNode);
            var isHover = ReferenceEquals(plane, _hoverNode);

            var baseColor = plane.Kind switch
            {
                CadReferencePlaneKind.XY => Color.FromArgb(110, 145, 209),
                CadReferencePlaneKind.YZ => Color.FromArgb(122, 176, 133),
                _ => Color.FromArgb(198, 161, 106)
            };

            var fillAlpha = isSelected ? 52 : isHover ? 36 : 26;
            var lineAlpha = isSelected ? 132 : isHover ? 96 : 72;
            using var fill = new SolidBrush(Color.FromArgb(fillAlpha, baseColor));
            using var line = new Pen(Color.FromArgb(lineAlpha, baseColor), isSelected ? 1.8f : 1.2f);

            var polygon = new[] { projected[0].Point, projected[1].Point, projected[2].Point, projected[3].Point };
            graphics.FillPolygon(fill, polygon);
            graphics.DrawPolygon(line, polygon);

            var labelPoint = new PointF(
                (projected[0].Point.X + projected[2].Point.X) * 0.5f + 6f,
                (projected[0].Point.Y + projected[2].Point.Y) * 0.5f - 6f);
            var shortLabel = plane.Kind switch
            {
                CadReferencePlaneKind.XY => "XY",
                CadReferencePlaneKind.YZ => "YZ",
                _ => "ZX"
            };
            using var textBrush = new SolidBrush(Color.FromArgb(isSelected ? 180 : 120, baseColor));
            using var font = new Font("Segoe UI", 7.5f);
            graphics.DrawString(shortLabel, font, textBrush, labelPoint);

            AddHitTriangle(plane, projected[0], projected[1], projected[2]);
            AddHitTriangle(plane, projected[0], projected[2], projected[3]);
        }
    }

    private void DrawAxes(Graphics graphics)
    {
        const float axisLength = 110f;

        using var xPen = new Pen(Color.FromArgb(198, 184, 76, 86), 1.4f);
        using var yPen = new Pen(Color.FromArgb(198, 68, 153, 94), 1.4f);
        using var zPen = new Pen(Color.FromArgb(198, 72, 114, 194), 1.4f);

        DrawLine3D(graphics, xPen, new Vector3(-axisLength, 0, 0), new Vector3(axisLength, 0, 0));
        DrawLine3D(graphics, yPen, new Vector3(0, -axisLength, 0), new Vector3(0, axisLength, 0));
        DrawLine3D(graphics, zPen, new Vector3(0, 0, -axisLength * 0.3f), new Vector3(0, 0, axisLength));

        var xLabel = Project(new Vector3(axisLength + 6f, 0, 0));
        var yLabel = Project(new Vector3(0, axisLength + 6f, 0));
        var zLabel = Project(new Vector3(0, 0, axisLength + 6f));

        using var labelFont = new Font("Segoe UI Semibold", 8.5f);
        using var xBrush = new SolidBrush(Color.FromArgb(210, 184, 76, 86));
        using var yBrush = new SolidBrush(Color.FromArgb(210, 68, 153, 94));
        using var zBrush = new SolidBrush(Color.FromArgb(210, 72, 114, 194));

        if (xLabel.IsValid) graphics.DrawString("X", labelFont, xBrush, xLabel.Point);
        if (yLabel.IsValid) graphics.DrawString("Y", labelFont, yBrush, yLabel.Point);
        if (zLabel.IsValid) graphics.DrawString("Z", labelFont, zBrush, zLabel.Point);

        var origin = Project(Vector3.Zero);
        if (origin.IsValid)
        {
            using var originBrush = new SolidBrush(Color.FromArgb(200, 61, 70, 85));
            graphics.FillEllipse(originBrush, origin.Point.X - 3f, origin.Point.Y - 3f, 6f, 6f);
        }
    }

    private void DrawBodies(Graphics graphics)
    {
        if (Document is null || ClientSize.Width <= 2 || ClientSize.Height <= 2)
        {
            return;
        }

        RebuildAnalyticBodies();
        if (_analyticBodies.Count == 0)
        {
            return;
        }

        var pixelCount = ClientSize.Width * ClientSize.Height;
        if (_bodyFrameBuffer is null || _bodyFrameBuffer.Length != pixelCount)
        {
            _bodyFrameBuffer = new int[pixelCount];
        }

        if (_bodyBitmap is null || _bodyBitmap.Width != ClientSize.Width || _bodyBitmap.Height != ClientSize.Height)
        {
            _bodyBitmap?.Dispose();
            _bodyBitmap = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
        }

        Array.Fill(_bodyFrameBuffer, 0);

        var width = ClientSize.Width;
        var height = ClientSize.Height;
        var centerX = width * 0.5f;
        var centerY = height * 0.52f;
        var projectionScale = ProjectionScale;
        var cameraOrigin = CameraToWorldPoint(new Vector3(0f, -_distance, 0f));
        var worldAxisX = CameraVectorToWorld(new Vector3(1f, 0f, 0f));
        var worldAxisDepth = CameraVectorToWorld(new Vector3(0f, 1f, 0f));
        var worldAxisZ = CameraVectorToWorld(new Vector3(0f, 0f, 1f));
        var lightDirection = Vector3.Normalize(new Vector3(-0.42f, -0.58f, 0.69f));

        for (var y = 0; y < height; y++)
        {
            var sy = centerY - (y + 0.5f);
            var rowOffset = y * width;

            for (var x = 0; x < width; x++)
            {
                var sx = (x + 0.5f) - centerX;
                var direction = worldAxisX * (sx / projectionScale) + worldAxisDepth + worldAxisZ * (sy / projectionScale);
                var directionLengthSquared = direction.LengthSquared();
                if (directionLengthSquared < 1e-9f)
                {
                    continue;
                }

                direction /= MathF.Sqrt(directionLengthSquared);

                var hasHit = false;
                var bestDistance = float.MaxValue;
                var hitColor = Color.Empty;

                foreach (var body in _analyticBodies)
                {
                    if (!TryIntersectPrimitive(body.Primitive, cameraOrigin, direction, out var hitPoint, out var normal))
                    {
                        continue;
                    }

                    var camera = Transform(hitPoint);
                    var cameraDistance = _distance + camera.Depth;
                    if (cameraDistance <= 2f || cameraDistance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = cameraDistance;

                    var baseColor = body.IsSelected
                        ? Color.FromArgb(92, 122, 212)
                        : body.IsHover
                            ? Color.FromArgb(151, 166, 198)
                            : Color.FromArgb(182, 188, 200);

                    hitColor = ShadeBodyColor(baseColor, normal, lightDirection);
                    hasHit = true;
                }

                if (hasHit)
                {
                    _bodyFrameBuffer[rowOffset + x] = hitColor.ToArgb();
                }
            }
        }

        var rect = new Rectangle(0, 0, width, height);
        var data = _bodyBitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(_bodyFrameBuffer, 0, data.Scan0, _bodyFrameBuffer.Length);
        }
        finally
        {
            _bodyBitmap.UnlockBits(data);
        }

        graphics.DrawImageUnscaled(_bodyBitmap, 0, 0);
    }

    private void DrawBackdrop(Graphics graphics)
    {
        using var brush = new LinearGradientBrush(
            ClientRectangle,
            Color.FromArgb(252, 253, 255),
            Color.FromArgb(239, 243, 249),
            LinearGradientMode.Vertical);
        graphics.FillRectangle(brush, ClientRectangle);
    }

    private void DrawHud(Graphics graphics)
    {
        using var titleFont = new Font("Segoe UI Semibold", 9f);
        using var bodyFont = new Font("Segoe UI", 8.5f);
        using var brush = new SolidBrush(Color.FromArgb(174, 74, 82, 95));

        var title = "CAD Workspace";
        var subtitle = "Orbit: Right Drag   Pan: Middle Drag   Zoom: Mouse Wheel";
        graphics.DrawString(title, titleFont, brush, 12, 10);
        graphics.DrawString(subtitle, bodyFont, brush, 12, 28);
    }

    private void DrawLine3D(Graphics graphics, Pen pen, Vector3 start, Vector3 end)
    {
        var p1 = Project(start);
        var p2 = Project(end);
        if (p1.IsValid && p2.IsValid)
        {
            graphics.DrawLine(pen, p1.Point, p2.Point);
        }
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

    private object? HitTest(Point location)
    {
        object? bestNode = null;
        var bestDistance = float.MaxValue;

        foreach (var proxy in _hitProxies)
        {
            if (!PointInTriangle(location, proxy.A, proxy.B, proxy.C))
            {
                continue;
            }

            if (proxy.CameraDistance < bestDistance)
            {
                bestDistance = proxy.CameraDistance;
                bestNode = proxy.Node;
            }
        }

        var bodyHit = HitTestBodies(location);
        if (bodyHit is not null && bodyHit.Value.CameraDistance < bestDistance)
        {
            bestNode = bodyHit.Value.Node;
        }

        return bestNode;
    }

    private BodyHit? HitTestBodies(Point location)
    {
        if (Document is null)
        {
            return null;
        }

        if (_analyticBodies.Count == 0)
        {
            RebuildAnalyticBodies();
        }

        if (_analyticBodies.Count == 0)
        {
            return null;
        }

        BuildRay(location, out var origin, out var direction);

        object? bestNode = null;
        var bestDistance = float.MaxValue;

        foreach (var body in _analyticBodies)
        {
            if (!TryIntersectPrimitive(body.Primitive, origin, direction, out var hitPoint, out _))
            {
                continue;
            }

            var camera = Transform(hitPoint);
            var cameraDistance = _distance + camera.Depth;
            if (cameraDistance <= 2f || cameraDistance >= bestDistance)
            {
                continue;
            }

            bestDistance = cameraDistance;
            bestNode = body.Body;
        }

        return bestNode is null ? null : new BodyHit(bestNode, bestDistance);
    }

    private void RebuildAnalyticBodies()
    {
        _analyticBodies.Clear();
        if (Document is null)
        {
            return;
        }

        foreach (var displayBody in _designCompiler.BuildDisplayBodies(Document))
        {
            var isSelected = ReferenceEquals(displayBody.Body, SelectedNode);
            var isHover = ReferenceEquals(displayBody.Body, _hoverNode);
            CollectAnalyticBodies(displayBody.Body, displayBody.Solid, Vector3.Zero, isSelected, isHover);
        }
    }

    private void CollectAnalyticBodies(CadBody body, Solid solid, Vector3 offset, bool isSelected, bool isHover)
    {
        switch (solid)
        {
            case TransformedSolid transformed:
            {
                var translation = transformed.Transform.Translation;
                var translatedOffset = offset + new Vector3((float)translation.X, (float)translation.Y, (float)translation.Z);
                CollectAnalyticBodies(body, transformed.Child, translatedOffset, isSelected, isHover);
                break;
            }
            case BoxSolid box:
                _analyticBodies.Add(new AnalyticBody(
                    body,
                    new AnalyticPrimitive(
                        AnalyticPrimitiveKind.Box,
                        offset,
                        new Vector3((float)box.Width * 0.5f, (float)box.Depth * 0.5f, (float)box.Height * 0.5f),
                        0f,
                        0f,
                        0f,
                        0f,
                        0f,
                        0f),
                    isSelected,
                    isHover));
                break;
            case CylinderSolid cylinder:
                _analyticBodies.Add(new AnalyticBody(
                    body,
                    new AnalyticPrimitive(
                        AnalyticPrimitiveKind.Cylinder,
                        offset,
                        Vector3.Zero,
                        (float)cylinder.Radius,
                        (float)cylinder.Height * 0.5f,
                        0f,
                        0f,
                        0f,
                        0f),
                    isSelected,
                    isHover));
                break;
            case SphereSolid sphere:
                _analyticBodies.Add(new AnalyticBody(
                    body,
                    new AnalyticPrimitive(
                        AnalyticPrimitiveKind.Sphere,
                        offset,
                        Vector3.Zero,
                        (float)sphere.Radius,
                        0f,
                        0f,
                        0f,
                        0f,
                        0f),
                    isSelected,
                    isHover));
                break;
            case ConeSolid cone:
                _analyticBodies.Add(new AnalyticBody(
                    body,
                    new AnalyticPrimitive(
                        AnalyticPrimitiveKind.Cone,
                        offset,
                        Vector3.Zero,
                        0f,
                        (float)cone.Height * 0.5f,
                        (float)cone.RadiusTop,
                        (float)cone.RadiusBottom,
                        0f,
                        0f),
                    isSelected,
                    isHover));
                break;
            case TorusSolid torus:
                _analyticBodies.Add(new AnalyticBody(
                    body,
                    new AnalyticPrimitive(
                        AnalyticPrimitiveKind.Torus,
                        offset,
                        Vector3.Zero,
                        0f,
                        0f,
                        0f,
                        0f,
                        (float)torus.MajorRadius,
                        (float)torus.MinorRadius),
                    isSelected,
                    isHover));
                break;
            case PyramidSolid pyramid:
                _analyticBodies.Add(new AnalyticBody(
                    body,
                    new AnalyticPrimitive(
                        AnalyticPrimitiveKind.Pyramid,
                        offset,
                        new Vector3((float)pyramid.BaseWidth * 0.5f, (float)pyramid.BaseDepth * 0.5f, (float)pyramid.Height * 0.5f),
                        0f,
                        (float)pyramid.Height * 0.5f,
                        0f,
                        0f,
                        0f,
                        0f),
                    isSelected,
                    isHover));
                break;
            case BooleanSolid booleanSolid when booleanSolid.Operation == BooleanOperation.Union:
                CollectAnalyticBodies(body, booleanSolid.A, offset, isSelected, isHover);
                CollectAnalyticBodies(body, booleanSolid.B, offset, isSelected, isHover);
                break;
            case BooleanSolid booleanSolid:
                // Subtract is approximated by drawing the positive source for now.
                CollectAnalyticBodies(body, booleanSolid.A, offset, isSelected, isHover);
                break;
        }
    }

    private static bool TryGetSolidBounds(Solid solid, Vector3 offset, ref bool hasBounds, ref Vector3 min, ref Vector3 max)
    {
        switch (solid)
        {
            case TransformedSolid transformed:
            {
                var translation = transformed.Transform.Translation;
                var translatedOffset = offset + new Vector3((float)translation.X, (float)translation.Y, (float)translation.Z);
                TryGetSolidBounds(transformed.Child, translatedOffset, ref hasBounds, ref min, ref max);
                return hasBounds;
            }
            case BoxSolid box:
            {
                var half = new Vector3((float)box.Width * 0.5f, (float)box.Depth * 0.5f, (float)box.Height * 0.5f);
                UpdateBounds(offset - half, offset + half, ref hasBounds, ref min, ref max);
                return hasBounds;
            }
            case CylinderSolid cylinder:
            {
                var radius = (float)cylinder.Radius;
                var halfHeight = (float)cylinder.Height * 0.5f;
                var minPoint = new Vector3(offset.X - radius, offset.Y - radius, offset.Z - halfHeight);
                var maxPoint = new Vector3(offset.X + radius, offset.Y + radius, offset.Z + halfHeight);
                UpdateBounds(minPoint, maxPoint, ref hasBounds, ref min, ref max);
                return hasBounds;
            }
            case SphereSolid sphere:
            {
                var radius = (float)sphere.Radius;
                var radiusVector = new Vector3(radius, radius, radius);
                UpdateBounds(offset - radiusVector, offset + radiusVector, ref hasBounds, ref min, ref max);
                return hasBounds;
            }
            case ConeSolid cone:
            {
                var radius = MathF.Max((float)cone.RadiusTop, (float)cone.RadiusBottom);
                var halfHeight = (float)cone.Height * 0.5f;
                var minPoint = new Vector3(offset.X - radius, offset.Y - radius, offset.Z - halfHeight);
                var maxPoint = new Vector3(offset.X + radius, offset.Y + radius, offset.Z + halfHeight);
                UpdateBounds(minPoint, maxPoint, ref hasBounds, ref min, ref max);
                return hasBounds;
            }
            case TorusSolid torus:
            {
                var radial = (float)(torus.MajorRadius + torus.MinorRadius);
                var vertical = (float)torus.MinorRadius;
                var minPoint = new Vector3(offset.X - radial, offset.Y - radial, offset.Z - vertical);
                var maxPoint = new Vector3(offset.X + radial, offset.Y + radial, offset.Z + vertical);
                UpdateBounds(minPoint, maxPoint, ref hasBounds, ref min, ref max);
                return hasBounds;
            }
            case PyramidSolid pyramid:
            {
                var halfWidth = (float)pyramid.BaseWidth * 0.5f;
                var halfDepth = (float)pyramid.BaseDepth * 0.5f;
                var halfHeight = (float)pyramid.Height * 0.5f;
                var minPoint = new Vector3(offset.X - halfWidth, offset.Y - halfDepth, offset.Z - halfHeight);
                var maxPoint = new Vector3(offset.X + halfWidth, offset.Y + halfDepth, offset.Z + halfHeight);
                UpdateBounds(minPoint, maxPoint, ref hasBounds, ref min, ref max);
                return hasBounds;
            }
            case BooleanSolid booleanSolid when booleanSolid.Operation == BooleanOperation.Union:
                TryGetSolidBounds(booleanSolid.A, offset, ref hasBounds, ref min, ref max);
                TryGetSolidBounds(booleanSolid.B, offset, ref hasBounds, ref min, ref max);
                return hasBounds;
            case BooleanSolid booleanSolid:
                TryGetSolidBounds(booleanSolid.A, offset, ref hasBounds, ref min, ref max);
                return hasBounds;
            default:
                return hasBounds;
        }
    }

    private static void UpdateBounds(Vector3 candidateMin, Vector3 candidateMax, ref bool hasBounds, ref Vector3 min, ref Vector3 max)
    {
        if (!hasBounds)
        {
            min = candidateMin;
            max = candidateMax;
            hasBounds = true;
            return;
        }

        min = Vector3.Min(min, candidateMin);
        max = Vector3.Max(max, candidateMax);
    }

    private static bool TryIntersectRayWithPlane(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 planePoint,
        Vector3 planeNormal,
        out Vector3 intersection)
    {
        var denominator = Vector3.Dot(rayDirection, planeNormal);
        if (MathF.Abs(denominator) < 1e-6f)
        {
            intersection = default;
            return false;
        }

        var t = Vector3.Dot(planePoint - rayOrigin, planeNormal) / denominator;
        if (t <= 1e-4f)
        {
            intersection = default;
            return false;
        }

        intersection = rayOrigin + rayDirection * t;
        return true;
    }

    private static bool PointInTriangle(Point p, PointF a, PointF b, PointF c)
    {
        static float Sign(Point p1, PointF p2, PointF p3) =>
            (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);

        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);

        var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        var hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private void AddHitTriangle(object node, ProjectedVertex a, ProjectedVertex b, ProjectedVertex c)
    {
        var cameraDistance = _distance + (a.Camera.Depth + b.Camera.Depth + c.Camera.Depth) / 3f;
        if (cameraDistance <= 2f)
        {
            return;
        }

        _hitProxies.Add(new HitProxy(node, a.Point, b.Point, c.Point, cameraDistance));
    }

    private void BuildRay(Point point, out Vector3 origin, out Vector3 direction)
    {
        var sx = (point.X + 0.5f) - ClientSize.Width * 0.5f;
        var sy = ClientSize.Height * 0.52f - (point.Y + 0.5f);
        var directionCamera = Vector3.Normalize(new Vector3(sx / ProjectionScale, 1f, sy / ProjectionScale));
        origin = CameraToWorldPoint(new Vector3(0f, -_distance, 0f));
        direction = Vector3.Normalize(CameraVectorToWorld(directionCamera));
    }

    private bool TryIntersectPrimitive(
        AnalyticPrimitive primitive,
        Vector3 origin,
        Vector3 direction,
        out Vector3 hitPoint,
        out Vector3 normal)
    {
        switch (primitive.Kind)
        {
            case AnalyticPrimitiveKind.Box:
                return TryIntersectBox(primitive.Center, primitive.HalfExtents, origin, direction, out hitPoint, out normal);
            case AnalyticPrimitiveKind.Cylinder:
                return TryIntersectCylinder(primitive.Center, primitive.Radius, primitive.HalfHeight, origin, direction, out hitPoint, out normal);
            case AnalyticPrimitiveKind.Sphere:
                return TryIntersectSphere(primitive.Center, primitive.Radius, origin, direction, out hitPoint, out normal);
            case AnalyticPrimitiveKind.Cone:
                return TryIntersectCone(primitive.Center, primitive.RadiusTop, primitive.RadiusBottom, primitive.HalfHeight, origin, direction, out hitPoint, out normal);
            case AnalyticPrimitiveKind.Torus:
                return TryIntersectTorus(primitive.Center, primitive.MajorRadius, primitive.MinorRadius, origin, direction, out hitPoint, out normal);
            case AnalyticPrimitiveKind.Pyramid:
                return TryIntersectPyramid(primitive.Center, primitive.HalfExtents, primitive.HalfHeight, origin, direction, out hitPoint, out normal);
            default:
                hitPoint = default;
                normal = default;
                return false;
        }
    }

    private static bool TryIntersectSphere(
        Vector3 center,
        float radius,
        Vector3 origin,
        Vector3 direction,
        out Vector3 hitPoint,
        out Vector3 normal)
    {
        var oc = origin - center;
        var b = Vector3.Dot(oc, direction);
        var c = Vector3.Dot(oc, oc) - radius * radius;
        var discriminant = b * b - c;
        if (discriminant < 0f)
        {
            hitPoint = default;
            normal = default;
            return false;
        }

        var sqrt = MathF.Sqrt(discriminant);
        var t = -b - sqrt;
        if (t <= 1e-4f)
        {
            t = -b + sqrt;
            if (t <= 1e-4f)
            {
                hitPoint = default;
                normal = default;
                return false;
            }
        }

        hitPoint = origin + direction * t;
        normal = Vector3.Normalize(hitPoint - center);
        return true;
    }

    private static bool TryIntersectBox(
        Vector3 center,
        Vector3 halfExtents,
        Vector3 origin,
        Vector3 direction,
        out Vector3 hitPoint,
        out Vector3 normal)
    {
        var localOrigin = origin - center;
        var localDirection = direction;

        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;

        if (!IntersectSlab(localOrigin.X, localDirection.X, -halfExtents.X, halfExtents.X, ref tMin, ref tMax) ||
            !IntersectSlab(localOrigin.Y, localDirection.Y, -halfExtents.Y, halfExtents.Y, ref tMin, ref tMax) ||
            !IntersectSlab(localOrigin.Z, localDirection.Z, -halfExtents.Z, halfExtents.Z, ref tMin, ref tMax))
        {
            hitPoint = default;
            normal = default;
            return false;
        }

        var t = tMin > 1e-4f ? tMin : tMax;
        if (t <= 1e-4f)
        {
            hitPoint = default;
            normal = default;
            return false;
        }

        hitPoint = origin + direction * t;
        var localHit = hitPoint - center;
        var dx = MathF.Abs(MathF.Abs(localHit.X) - halfExtents.X);
        var dy = MathF.Abs(MathF.Abs(localHit.Y) - halfExtents.Y);
        var dz = MathF.Abs(MathF.Abs(localHit.Z) - halfExtents.Z);

        if (dx <= dy && dx <= dz)
        {
            normal = new Vector3(MathF.Sign(localHit.X), 0f, 0f);
        }
        else if (dy <= dz)
        {
            normal = new Vector3(0f, MathF.Sign(localHit.Y), 0f);
        }
        else
        {
            normal = new Vector3(0f, 0f, MathF.Sign(localHit.Z));
        }

        return true;
    }

    private static bool IntersectSlab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(direction) < 1e-6f)
        {
            return origin >= min && origin <= max;
        }

        var t1 = (min - origin) / direction;
        var t2 = (max - origin) / direction;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMin <= tMax;
    }

    private static bool TryIntersectCylinder(
        Vector3 center,
        float radius,
        float halfHeight,
        Vector3 origin,
        Vector3 direction,
        out Vector3 hitPoint,
        out Vector3 normal)
    {
        var localOrigin = origin - center;
        var localDirection = direction;
        var bestT = float.MaxValue;
        normal = default;

        var a = localDirection.X * localDirection.X + localDirection.Y * localDirection.Y;
        if (a > 1e-8f)
        {
            var b = 2f * (localOrigin.X * localDirection.X + localOrigin.Y * localDirection.Y);
            var c = localOrigin.X * localOrigin.X + localOrigin.Y * localOrigin.Y - radius * radius;
            var discriminant = b * b - 4f * a * c;
            if (discriminant >= 0f)
            {
                var sqrt = MathF.Sqrt(discriminant);
                var inv = 0.5f / a;
                var t0 = (-b - sqrt) * inv;
                var t1 = (-b + sqrt) * inv;

                if (t0 > 1e-4f)
                {
                    var z = localOrigin.Z + localDirection.Z * t0;
                    if (z >= -halfHeight && z <= halfHeight)
                    {
                        bestT = t0;
                        var x = localOrigin.X + localDirection.X * t0;
                        var y = localOrigin.Y + localDirection.Y * t0;
                        normal = Vector3.Normalize(new Vector3(x, y, 0f));
                    }
                }

                if (t1 > 1e-4f && t1 < bestT)
                {
                    var z = localOrigin.Z + localDirection.Z * t1;
                    if (z >= -halfHeight && z <= halfHeight)
                    {
                        bestT = t1;
                        var x = localOrigin.X + localDirection.X * t1;
                        var y = localOrigin.Y + localDirection.Y * t1;
                        normal = Vector3.Normalize(new Vector3(x, y, 0f));
                    }
                }
            }
        }

        if (MathF.Abs(localDirection.Z) > 1e-8f)
        {
            var topT = (halfHeight - localOrigin.Z) / localDirection.Z;
            if (topT > 1e-4f && topT < bestT)
            {
                var x = localOrigin.X + localDirection.X * topT;
                var y = localOrigin.Y + localDirection.Y * topT;
                if (x * x + y * y <= radius * radius)
                {
                    bestT = topT;
                    normal = Vector3.UnitZ;
                }
            }

            var bottomT = (-halfHeight - localOrigin.Z) / localDirection.Z;
            if (bottomT > 1e-4f && bottomT < bestT)
            {
                var x = localOrigin.X + localDirection.X * bottomT;
                var y = localOrigin.Y + localDirection.Y * bottomT;
                if (x * x + y * y <= radius * radius)
                {
                    bestT = bottomT;
                    normal = -Vector3.UnitZ;
                }
            }
        }

        if (bestT == float.MaxValue)
        {
            hitPoint = default;
            return false;
        }

        hitPoint = origin + direction * bestT;
        return true;
    }

    private static bool TryIntersectCone(
        Vector3 center,
        float radiusTop,
        float radiusBottom,
        float halfHeight,
        Vector3 origin,
        Vector3 direction,
        out Vector3 hitPoint,
        out Vector3 normal)
    {
        hitPoint = default;
        normal = default;

        if (halfHeight <= 1e-5f)
        {
            return false;
        }

        var localOrigin = origin - center;
        var localDirection = direction;
        var bestT = float.MaxValue;
        var bestNormal = Vector3.Zero;

        var k = (radiusTop - radiusBottom) / (2f * halfHeight);
        var b = (radiusTop + radiusBottom) * 0.5f;

        var kd = k * localDirection.Z;
        var kzPlusB = k * localOrigin.Z + b;
        var a = localDirection.X * localDirection.X + localDirection.Y * localDirection.Y - kd * kd;
        var q = localOrigin.X * localDirection.X + localOrigin.Y * localDirection.Y - kzPlusB * kd;
        var c = localOrigin.X * localOrigin.X + localOrigin.Y * localOrigin.Y - kzPlusB * kzPlusB;
        var quadraticB = 2f * q;

        if (TrySolveQuadratic(a, quadraticB, c, out var t0, out var t1))
        {
            CheckConeSideCandidate(t0);
            CheckConeSideCandidate(t1);
        }

        if (MathF.Abs(localDirection.Z) > 1e-8f)
        {
            CheckCapCandidate(halfHeight, radiusTop, Vector3.UnitZ);
            CheckCapCandidate(-halfHeight, radiusBottom, -Vector3.UnitZ);
        }

        if (bestT == float.MaxValue)
        {
            return false;
        }

        hitPoint = origin + direction * bestT;
        normal = bestNormal.LengthSquared() > 1e-8f ? Vector3.Normalize(bestNormal) : Vector3.UnitZ;
        return true;

        void CheckConeSideCandidate(float t)
        {
            if (t <= 1e-4f || t >= bestT)
            {
                return;
            }

            var z = localOrigin.Z + localDirection.Z * t;
            if (z < -halfHeight || z > halfHeight)
            {
                return;
            }

            var x = localOrigin.X + localDirection.X * t;
            var y = localOrigin.Y + localDirection.Y * t;
            var radiusAtZ = k * z + b;
            if (radiusAtZ < 0f)
            {
                return;
            }

            var radialSquared = x * x + y * y;
            var radiusSquared = radiusAtZ * radiusAtZ;
            if (MathF.Abs(radialSquared - radiusSquared) > MathF.Max(0.05f, radiusSquared * 0.08f))
            {
                return;
            }

            var sideNormal = new Vector3(x, y, -k * radiusAtZ);
            if (sideNormal.LengthSquared() < 1e-8f)
            {
                return;
            }

            bestT = t;
            bestNormal = sideNormal;
        }

        void CheckCapCandidate(float capZ, float capRadius, Vector3 capNormal)
        {
            var t = (capZ - localOrigin.Z) / localDirection.Z;
            if (t <= 1e-4f || t >= bestT)
            {
                return;
            }

            var x = localOrigin.X + localDirection.X * t;
            var y = localOrigin.Y + localDirection.Y * t;
            if (x * x + y * y > capRadius * capRadius)
            {
                return;
            }

            bestT = t;
            bestNormal = capNormal;
        }
    }

    private static bool TryIntersectTorus(
        Vector3 center,
        float majorRadius,
        float minorRadius,
        Vector3 origin,
        Vector3 direction,
        out Vector3 hitPoint,
        out Vector3 normal)
    {
        hitPoint = default;
        normal = default;

        if (majorRadius <= 0f || minorRadius <= 0f)
        {
            return false;
        }

        const int maxSteps = 110;
        const float maxDistance = 5000f;
        const float hitEpsilon = 0.015f;

        var t = 0f;
        for (var i = 0; i < maxSteps && t < maxDistance; i++)
        {
            var point = origin + direction * t;
            var local = point - center;
            var distance = TorusSignedDistance(local, majorRadius, minorRadius);
            if (distance < hitEpsilon)
            {
                hitPoint = point;
                normal = EstimateTorusNormal(local, majorRadius, minorRadius);
                return true;
            }

            t += Math.Clamp(distance, 0.02f, 26f);
        }

        return false;
    }

    private static bool TryIntersectPyramid(
        Vector3 center,
        Vector3 halfExtents,
        float halfHeight,
        Vector3 origin,
        Vector3 direction,
        out Vector3 hitPoint,
        out Vector3 normal)
    {
        hitPoint = default;
        normal = default;

        var halfWidth = halfExtents.X;
        var halfDepth = halfExtents.Y;
        if (halfWidth <= 1e-5f || halfDepth <= 1e-5f || halfHeight <= 1e-5f)
        {
            return false;
        }

        var localOrigin = origin - center;
        var localDirection = direction;

        Span<Vector3> planeNormals =
        [
            new Vector3(0f, 0f, -1f),
            new Vector3(2f * halfHeight, 0f, halfWidth),
            new Vector3(-2f * halfHeight, 0f, halfWidth),
            new Vector3(0f, 2f * halfHeight, halfDepth),
            new Vector3(0f, -2f * halfHeight, halfDepth)
        ];

        Span<float> planeOffsets =
        [
            -halfHeight,
            -halfWidth * halfHeight,
            -halfWidth * halfHeight,
            -halfDepth * halfHeight,
            -halfDepth * halfHeight
        ];

        var tEnter = float.NegativeInfinity;
        var tExit = float.PositiveInfinity;
        var enterNormal = Vector3.Zero;
        var exitNormal = Vector3.Zero;

        for (var i = 0; i < planeNormals.Length; i++)
        {
            var planeNormal = planeNormals[i];
            var planeOffset = planeOffsets[i];

            var distance = Vector3.Dot(planeNormal, localOrigin) + planeOffset;
            var denominator = Vector3.Dot(planeNormal, localDirection);
            if (MathF.Abs(denominator) < 1e-7f)
            {
                if (distance > 0f)
                {
                    return false;
                }

                continue;
            }

            var t = -distance / denominator;
            if (denominator < 0f)
            {
                if (t > tEnter)
                {
                    tEnter = t;
                    enterNormal = planeNormal;
                }
            }
            else
            {
                if (t < tExit)
                {
                    tExit = t;
                    exitNormal = planeNormal;
                }
            }

            if (tEnter > tExit)
            {
                return false;
            }
        }

        var useEnter = tEnter > 1e-4f;
        var tHit = useEnter ? tEnter : tExit;
        if (tHit <= 1e-4f || float.IsInfinity(tHit))
        {
            return false;
        }

        hitPoint = origin + direction * tHit;
        var rawNormal = useEnter ? enterNormal : exitNormal;
        normal = rawNormal.LengthSquared() > 1e-8f
            ? Vector3.Normalize(rawNormal)
            : Vector3.UnitZ;
        return true;
    }

    private static bool TrySolveQuadratic(float a, float b, float c, out float t0, out float t1)
    {
        t0 = 0f;
        t1 = 0f;

        if (MathF.Abs(a) < 1e-8f)
        {
            if (MathF.Abs(b) < 1e-8f)
            {
                return false;
            }

            var t = -c / b;
            t0 = t;
            t1 = t;
            return true;
        }

        var discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
        {
            return false;
        }

        var sqrt = MathF.Sqrt(discriminant);
        var inv = 0.5f / a;
        t0 = (-b - sqrt) * inv;
        t1 = (-b + sqrt) * inv;
        if (t0 > t1)
        {
            (t0, t1) = (t1, t0);
        }

        return true;
    }

    private static float TorusSignedDistance(Vector3 localPoint, float majorRadius, float minorRadius)
    {
        var radial = MathF.Sqrt(localPoint.X * localPoint.X + localPoint.Y * localPoint.Y);
        var qx = radial - majorRadius;
        var qy = localPoint.Z;
        return MathF.Sqrt(qx * qx + qy * qy) - minorRadius;
    }

    private static Vector3 EstimateTorusNormal(Vector3 localPoint, float majorRadius, float minorRadius)
    {
        const float h = 0.01f;
        var dx = TorusSignedDistance(localPoint + new Vector3(h, 0f, 0f), majorRadius, minorRadius) -
                 TorusSignedDistance(localPoint - new Vector3(h, 0f, 0f), majorRadius, minorRadius);
        var dy = TorusSignedDistance(localPoint + new Vector3(0f, h, 0f), majorRadius, minorRadius) -
                 TorusSignedDistance(localPoint - new Vector3(0f, h, 0f), majorRadius, minorRadius);
        var dz = TorusSignedDistance(localPoint + new Vector3(0f, 0f, h), majorRadius, minorRadius) -
                 TorusSignedDistance(localPoint - new Vector3(0f, 0f, h), majorRadius, minorRadius);

        var normal = new Vector3(dx, dy, dz);
        return normal.LengthSquared() > 1e-8f ? Vector3.Normalize(normal) : Vector3.UnitZ;
    }

    private static Color ShadeBodyColor(Color baseColor, Vector3 normal, Vector3 lightDirection)
    {
        var diffuse = MathF.Max(0f, Vector3.Dot(normal, lightDirection));
        var shade = 0.36f + diffuse * 0.64f;

        var r = (int)Math.Clamp(baseColor.R * shade, 0f, 255f);
        var g = (int)Math.Clamp(baseColor.G * shade, 0f, 255f);
        var b = (int)Math.Clamp(baseColor.B * shade, 0f, 255f);
        return Color.FromArgb(255, r, g, b);
    }

    private ProjectedVertex Project(Vertex world)
    {
        return Project(new Vector3((float)world.X, (float)world.Y, (float)world.Z));
    }

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

    private Vector3 CameraToWorldPoint(Vector3 cameraPoint)
    {
        return _pivot + CameraVectorToWorld(cameraPoint);
    }

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

    private float ProjectionScale => Math.Max(100f, Math.Min(ClientSize.Width, ClientSize.Height) * 1.25f);

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

    private static Vector3[] GetPlaneCorners(CadReferencePlaneKind kind, float size)
    {
        return kind switch
        {
            CadReferencePlaneKind.XY => new[]
            {
                new Vector3(-size, -size, 0),
                new Vector3(size, -size, 0),
                new Vector3(size, size, 0),
                new Vector3(-size, size, 0)
            },
            CadReferencePlaneKind.YZ => new[]
            {
                new Vector3(0, -size, -size),
                new Vector3(0, size, -size),
                new Vector3(0, size, size),
                new Vector3(0, -size, size)
            },
            _ => new[]
            {
                new Vector3(-size, 0, -size),
                new Vector3(size, 0, -size),
                new Vector3(size, 0, size),
                new Vector3(-size, 0, size)
            }
        };
    }

    // ── ViewCube ────────────────────────────────────────────────────────────

    // Each face: (corners, outward world-normal, short label, snap-yaw, snap-pitch)
    private static readonly (Vector3[] C, Vector3 N, string L, float SY, float SP)[] ViewCubeFaces =
    {
        (new Vector3[] { new(-1,-1,1), new(1,-1,1), new(1,1,1), new(-1,1,1) },
         new Vector3(0,0,1),   "TOP", 0f,               MathF.PI * 0.5f),
        (new Vector3[] { new(-1,-1,-1), new(-1,1,-1), new(1,1,-1), new(1,-1,-1) },
         new Vector3(0,0,-1),  "BTM", 0f,               -MathF.PI * 0.5f),
        (new Vector3[] { new(-1,-1,-1), new(1,-1,-1), new(1,-1,1), new(-1,-1,1) },
         new Vector3(0,-1,0),  "FRT", 0f,               0f),
        (new Vector3[] { new(1,1,-1), new(-1,1,-1), new(-1,1,1), new(1,1,1) },
         new Vector3(0,1,0),   "BCK", MathF.PI,         0f),
        (new Vector3[] { new(1,-1,-1), new(1,1,-1), new(1,1,1), new(1,-1,1) },
         new Vector3(1,0,0),   "RGT", -MathF.PI * 0.5f, 0f),
        (new Vector3[] { new(-1,1,-1), new(-1,-1,-1), new(-1,-1,1), new(-1,1,1) },
         new Vector3(-1,0,0),  "LFT", MathF.PI * 0.5f,  0f),
    };

    private static RectangleF GetViewCubeRect(Size clientSize)
    {
        const int vcSize   = 78;
        const int vcMargin = 12;
        return new RectangleF(clientSize.Width - vcSize - vcMargin, vcMargin, vcSize, vcSize);
    }

    private CameraVertex TransformRotationOnly(Vector3 world)
    {
        var cosYaw   = MathF.Cos(_yaw);
        var sinYaw   = MathF.Sin(_yaw);
        var cosPitch = MathF.Cos(_pitch);
        var sinPitch = MathF.Sin(_pitch);

        var x1    = cosYaw * world.X  - sinYaw * world.Y;
        var y1    = sinYaw * world.X  + cosYaw * world.Y;
        var depth = cosPitch * y1     - sinPitch * world.Z;
        var z2    = sinPitch * y1     + cosPitch * world.Z;
        return new CameraVertex(x1, depth, z2);
    }

    private PointF ProjectViewCubeVertex(Vector3 world, RectangleF vcRect)
    {
        var c = TransformRotationOnly(world);
        const float scale = 0.68f;
        return new PointF(
            vcRect.X + vcRect.Width  * 0.5f + c.X * vcRect.Width  * 0.5f * scale,
            vcRect.Y + vcRect.Height * 0.5f - c.Z * vcRect.Height * 0.5f * scale);
    }

    private static bool PointInQuad(Point p, PointF[] q) =>
        PointInTriangle(p, q[0], q[1], q[2]) || PointInTriangle(p, q[0], q[2], q[3]);

    private int HitTestViewCube(Point location)
    {
        var vcRect = GetViewCubeRect(ClientSize);
        if (!vcRect.Contains(location.X, location.Y))
            return -1;

        var bestFace  = -1;
        var bestDepth = float.MaxValue;

        for (var i = 0; i < ViewCubeFaces.Length; i++)
        {
            var (corners, normal, _, _, _) = ViewCubeFaces[i];
            if (TransformRotationOnly(normal).Depth >= 0f)
                continue;

            var poly = Array.ConvertAll(corners, c => ProjectViewCubeVertex(c, vcRect));
            if (!PointInQuad(location, poly))
                continue;

            var depth = (TransformRotationOnly(corners[0]).Depth +
                         TransformRotationOnly(corners[1]).Depth +
                         TransformRotationOnly(corners[2]).Depth +
                         TransformRotationOnly(corners[3]).Depth) * 0.25f;
            if (depth < bestDepth)
            {
                bestDepth = depth;
                bestFace  = i;
            }
        }

        return bestFace;
    }

    private void SnapToViewCubeFace(int faceIndex)
    {
        if ((uint)faceIndex >= (uint)ViewCubeFaces.Length)
            return;
        _yaw   = ViewCubeFaces[faceIndex].SY;
        _pitch = ViewCubeFaces[faceIndex].SP;
        Invalidate();
    }

    private void DrawViewCube(Graphics g)
    {
        var vcRect = GetViewCubeRect(ClientSize);

        using var bgBrush = new SolidBrush(Color.FromArgb(210, 246, 248, 252));
        g.FillRectangle(bgBrush, vcRect);
        using var bgPen = new Pen(Color.FromArgb(90, 158, 170, 186), 1f);
        g.DrawRectangle(bgPen, vcRect.X, vcRect.Y, vcRect.Width, vcRect.Height);

        var faces = new (PointF[] Poly, float AvgDepth, bool IsFront, int Fi)[ViewCubeFaces.Length];
        for (var i = 0; i < ViewCubeFaces.Length; i++)
        {
            var (corners, normal, _, _, _) = ViewCubeFaces[i];
            var poly     = Array.ConvertAll(corners, c => ProjectViewCubeVertex(c, vcRect));
            var avgDepth = (TransformRotationOnly(corners[0]).Depth +
                            TransformRotationOnly(corners[1]).Depth +
                            TransformRotationOnly(corners[2]).Depth +
                            TransformRotationOnly(corners[3]).Depth) * 0.25f;
            faces[i] = (poly, avgDepth, TransformRotationOnly(normal).Depth < 0f, i);
        }

        // Draw back-faces first
        Array.Sort(faces, static (a, b) => b.AvgDepth.CompareTo(a.AvgDepth));

        using var labelFont = new Font("Segoe UI Semibold", 6.5f);

        foreach (var (poly, _, isFront, fi) in faces)
        {
            var isHover = fi == _viewCubeHoverFace;

            Color fill;
            int   edgeAlpha;
            if (isHover && isFront) { fill = Color.FromArgb(210, 186, 214, 248); edgeAlpha = 200; }
            else if (isFront)        { fill = Color.FromArgb(195, 218, 227, 240); edgeAlpha = 165; }
            else                     { fill = Color.FromArgb(55,  200, 208, 218); edgeAlpha = 72;  }

            using var fillBrush = new SolidBrush(fill);
            g.FillPolygon(fillBrush, poly);
            using var edgePen = new Pen(Color.FromArgb(edgeAlpha, 126, 142, 162), 0.8f);
            g.DrawPolygon(edgePen, poly);

            if (!isFront)
                continue;

            var cx = (poly[0].X + poly[1].X + poly[2].X + poly[3].X) * 0.25f;
            var cy = (poly[0].Y + poly[1].Y + poly[2].Y + poly[3].Y) * 0.25f;
            var label = ViewCubeFaces[fi].L;
            using var textBrush = new SolidBrush(
                isHover ? Color.FromArgb(0, 70, 154) : Color.FromArgb(44, 54, 70));
            var sz = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, textBrush, cx - sz.Width * 0.5f, cy - sz.Height * 0.5f);
        }
    }

    // ── Scale ruler ─────────────────────────────────────────────────────────

    private void DrawScaleRuler(Graphics g)
    {
        const int rulerLeft   = 16;
        const int rulerBottom = 22;
        const int numSegments = 3;

        var pixelsPerUnit     = ProjectionScale / _distance;
        var targetWorldPerSeg = 40f / pixelsPerUnit;
        var step              = NiceStep(targetWorldPerSeg);
        var segPixels         = step * pixelsPerUnit;
        var totalPixels       = segPixels * numSegments;

        var y0 = ClientSize.Height - rulerBottom;
        var x0 = (float)rulerLeft;

        using var linePen   = new Pen(Color.FromArgb(140, 108, 116, 128), 1f);
        using var tickPen   = new Pen(Color.FromArgb(140, 108, 116, 128), 0.8f);
        using var textFont  = new Font("Consolas", 7.5f);
        using var textBrush = new SolidBrush(Color.FromArgb(130, 100, 108, 120));

        g.DrawLine(linePen, x0, y0, x0 + totalPixels, y0);

        for (var i = 0; i <= numSegments; i++)
        {
            var x = x0 + i * segPixels;
            var h = (i == 0 || i == numSegments) ? 9f : 5f;
            g.DrawLine(tickPen, x, y0, x, y0 - h);
        }

        var totalWorld = step * numSegments;
        var rulerLabel = totalWorld >= 1000f
            ? $"{totalWorld / 1000f:0.#} m"
            : totalWorld >= 1f
                ? $"{totalWorld:0} mm"
                : $"{totalWorld:0.##} mm";
        g.DrawString(rulerLabel, textFont, textBrush, x0 + totalPixels + 4f, y0 - 10f);
    }

    private static float NiceStep(float value)
    {
        if (value <= 0f) return 1f;
        var exp  = MathF.Floor(MathF.Log10(value));
        var mag  = MathF.Pow(10f, exp);
        var norm = value / mag;
        return norm switch
        {
            <= 1f => mag,
            <= 2f => 2f * mag,
            <= 5f => 5f * mag,
            _     => 10f * mag
        };
    }

    private readonly record struct CameraVertex(float X, float Depth, float Z);

    private readonly record struct ProjectedVertex(PointF Point, CameraVertex Camera, bool IsValid)
    {
        public static ProjectedVertex Invalid => new(default, default, false);
    }

    private readonly record struct HitProxy(object Node, PointF A, PointF B, PointF C, float CameraDistance);

    private readonly record struct BodyHit(object Node, float CameraDistance);

    private enum AnalyticPrimitiveKind
    {
        Box,
        Cylinder,
        Sphere,
        Cone,
        Torus,
        Pyramid
    }

    private readonly record struct AnalyticPrimitive(
        AnalyticPrimitiveKind Kind,
        Vector3 Center,
        Vector3 HalfExtents,
        float Radius,
        float HalfHeight,
        float RadiusTop,
        float RadiusBottom,
        float MajorRadius,
        float MinorRadius);

    private readonly record struct AnalyticBody(CadBody Body, AnalyticPrimitive Primitive, bool IsSelected, bool IsHover);
}
