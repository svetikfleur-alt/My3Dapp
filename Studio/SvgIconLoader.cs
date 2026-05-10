using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Xml.Linq;

namespace My3DApp.Studio;

internal static class SvgIconLoader
{
    internal static Bitmap? Load(string svgPath, int size = 18)
    {
        if (!File.Exists(svgPath)) return null;
        try
        {
            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            var doc = XDocument.Load(svgPath);
            var root = doc.Root!;
            float scale = ParseViewBoxScale(root, size);

            foreach (var el in root.Descendants())
                RenderElement(g, el, scale);

            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static float ParseViewBoxScale(XElement root, int size)
    {
        var vb = root.Attribute("viewBox")?.Value?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (vb is { Length: >= 4 } && float.TryParse(vb[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w > 0)
            return size / w;
        return 1f;
    }

    private static void RenderElement(Graphics g, XElement el, float scale)
    {
        var fill = GetColor(el, "fill");
        var stroke = GetColor(el, "stroke");
        float sw = GetFloat(el, "stroke-width", 1f) * scale;

        using var brush = fill.HasValue ? new SolidBrush(fill.Value) : null;
        Pen? pen = null;
        if (stroke.HasValue)
        {
            pen = new Pen(stroke.Value, Math.Max(sw, 1f));
            pen.LineJoin = el.Attribute("stroke-linejoin")?.Value == "round" ? LineJoin.Round : LineJoin.Miter;
            pen.StartCap = el.Attribute("stroke-linecap")?.Value == "round" ? LineCap.Round : LineCap.Flat;
            pen.EndCap = pen.StartCap;
        }

        try
        {
            switch (el.Name.LocalName)
            {
                case "rect":
                {
                    float x = GetFloat(el, "x", 0) * scale, y = GetFloat(el, "y", 0) * scale;
                    float w = GetFloat(el, "width", 0) * scale, h = GetFloat(el, "height", 0) * scale;
                    if (brush != null) g.FillRectangle(brush, x, y, w, h);
                    if (pen != null) g.DrawRectangle(pen, x, y, w, h);
                    break;
                }
                case "circle":
                {
                    float cx = GetFloat(el, "cx", 0) * scale, cy = GetFloat(el, "cy", 0) * scale;
                    float r = GetFloat(el, "r", 0) * scale;
                    if (brush != null) g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
                    if (pen != null) g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                    break;
                }
                case "ellipse":
                {
                    float cx = GetFloat(el, "cx", 0) * scale, cy = GetFloat(el, "cy", 0) * scale;
                    float rx = GetFloat(el, "rx", 0) * scale, ry = GetFloat(el, "ry", 0) * scale;
                    if (brush != null) g.FillEllipse(brush, cx - rx, cy - ry, rx * 2, ry * 2);
                    if (pen != null) g.DrawEllipse(pen, cx - rx, cy - ry, rx * 2, ry * 2);
                    break;
                }
                case "line":
                {
                    float x1 = GetFloat(el, "x1", 0) * scale, y1 = GetFloat(el, "y1", 0) * scale;
                    float x2 = GetFloat(el, "x2", 0) * scale, y2 = GetFloat(el, "y2", 0) * scale;
                    if (pen != null)
                    {
                        ApplyDashArray(pen, el, scale);
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                    break;
                }
                case "path":
                {
                    var d = el.Attribute("d")?.Value;
                    if (d == null) break;
                    using var path = ParsePath(d, scale);
                    if (brush != null) g.FillPath(brush, path);
                    if (pen != null)
                    {
                        ApplyDashArray(pen, el, scale);
                        g.DrawPath(pen, path);
                    }
                    break;
                }
            }
        }
        finally
        {
            pen?.Dispose();
        }
    }

    private static void ApplyDashArray(Pen pen, XElement el, float scale)
    {
        var da = el.Attribute("stroke-dasharray")?.Value;
        if (da == null) return;
        var parts = da.Split(' ', ',');
        var dashes = parts
            .Select(p => float.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f)
            .Where(v => v > 0)
            .ToArray();
        if (dashes.Length > 0)
        {
            pen.DashStyle = DashStyle.Custom;
            // GDI+ dash values are multiples of pen width
            pen.DashPattern = dashes.Select(v => v / pen.Width).ToArray();
        }
    }

    private static GraphicsPath ParsePath(string d, float scale)
    {
        var path = new GraphicsPath();
        var tokens = TokenizePath(d);
        int i = 0;
        float cx = 0, cy = 0;
        float scx = 0, scy = 0;
        char lastCmd = 'M';

        while (i < tokens.Count)
        {
            char cmd;
            if (char.IsLetter(tokens[i][0]))
            {
                cmd = tokens[i][0];
                i++;
            }
            else
            {
                cmd = lastCmd;
            }

            bool rel = char.IsLower(cmd);

            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                {
                    float x = F(tokens, i) * scale + (rel ? cx : 0);
                    float y = F(tokens, i + 1) * scale + (rel ? cy : 0);
                    i += 2;
                    path.StartFigure();
                    cx = x; cy = y;
                    lastCmd = rel ? 'l' : 'L';
                    continue;
                }
                case 'L':
                {
                    float x = F(tokens, i) * scale + (rel ? cx : 0);
                    float y = F(tokens, i + 1) * scale + (rel ? cy : 0);
                    i += 2;
                    path.AddLine(cx, cy, x, y);
                    cx = x; cy = y;
                    break;
                }
                case 'H':
                {
                    float x = F(tokens, i) * scale + (rel ? cx : 0);
                    i++;
                    path.AddLine(cx, cy, x, cy);
                    cx = x;
                    break;
                }
                case 'V':
                {
                    float y = F(tokens, i) * scale + (rel ? cy : 0);
                    i++;
                    path.AddLine(cx, cy, cx, y);
                    cy = y;
                    break;
                }
                case 'Z':
                {
                    path.CloseFigure();
                    break;
                }
                case 'C':
                {
                    float x1 = F(tokens, i) * scale + (rel ? cx : 0);
                    float y1 = F(tokens, i + 1) * scale + (rel ? cy : 0);
                    float x2 = F(tokens, i + 2) * scale + (rel ? cx : 0);
                    float y2 = F(tokens, i + 3) * scale + (rel ? cy : 0);
                    float x = F(tokens, i + 4) * scale + (rel ? cx : 0);
                    float y = F(tokens, i + 5) * scale + (rel ? cy : 0);
                    i += 6;
                    path.AddBezier(cx, cy, x1, y1, x2, y2, x, y);
                    scx = x2; scy = y2;
                    cx = x; cy = y;
                    break;
                }
                case 'S':
                {
                    char uc = char.ToUpperInvariant(lastCmd);
                    float rx1 = uc == 'C' || uc == 'S' ? 2 * cx - scx : cx;
                    float ry1 = uc == 'C' || uc == 'S' ? 2 * cy - scy : cy;
                    float x2 = F(tokens, i) * scale + (rel ? cx : 0);
                    float y2 = F(tokens, i + 1) * scale + (rel ? cy : 0);
                    float x = F(tokens, i + 2) * scale + (rel ? cx : 0);
                    float y = F(tokens, i + 3) * scale + (rel ? cy : 0);
                    i += 4;
                    path.AddBezier(cx, cy, rx1, ry1, x2, y2, x, y);
                    scx = x2; scy = y2;
                    cx = x; cy = y;
                    break;
                }
                case 'A':
                {
                    float rx = F(tokens, i) * scale;
                    float ry = F(tokens, i + 1) * scale;
                    float xRot = F(tokens, i + 2);
                    bool largeArc = F(tokens, i + 3) != 0;
                    bool sweep = F(tokens, i + 4) != 0;
                    float x = F(tokens, i + 5) * scale + (rel ? cx : 0);
                    float y = F(tokens, i + 6) * scale + (rel ? cy : 0);
                    i += 7;
                    AddArcToPath(path, cx, cy, x, y, rx, ry, xRot, largeArc, sweep);
                    cx = x; cy = y;
                    break;
                }
                default:
                    i++;
                    break;
            }

            lastCmd = cmd;
        }

        return path;
    }

    private static void AddArcToPath(GraphicsPath path, float x1, float y1, float x2, float y2,
        float rx, float ry, float xRot, bool largeArc, bool sweep)
    {
        double phi = xRot * Math.PI / 180.0;
        double cosPhi = Math.Cos(phi), sinPhi = Math.Sin(phi);

        double dx2 = (x1 - x2) / 2.0, dy2 = (y1 - y2) / 2.0;
        double x1p = cosPhi * dx2 + sinPhi * dy2;
        double y1p = -sinPhi * dx2 + cosPhi * dy2;

        double x1p2 = x1p * x1p, y1p2 = y1p * y1p;
        double rx2 = rx * rx, ry2 = ry * ry;

        double lambda = x1p2 / rx2 + y1p2 / ry2;
        if (lambda > 1) { double s = Math.Sqrt(lambda); rx *= (float)s; ry *= (float)s; rx2 = rx * rx; ry2 = ry * ry; }

        double num = Math.Max(0, rx2 * ry2 - rx2 * y1p2 - ry2 * x1p2);
        double den = rx2 * y1p2 + ry2 * x1p2;
        double sq = den == 0 ? 0 : Math.Sqrt(num / den);
        if (largeArc == sweep) sq = -sq;

        double cxp = sq * rx * y1p / ry;
        double cyp = -sq * ry * x1p / rx;

        double cx = cosPhi * cxp - sinPhi * cyp + (x1 + x2) / 2.0;
        double cy = sinPhi * cxp + cosPhi * cyp + (y1 + y2) / 2.0;

        double ux = (x1p - cxp) / rx, uy = (y1p - cyp) / ry;
        double vx = (-x1p - cxp) / rx, vy = (-y1p - cyp) / ry;

        double startAngle = Math.Atan2(uy, ux) * 180.0 / Math.PI;
        double dtheta = Math.Atan2(vy, vx) * 180.0 / Math.PI - startAngle;

        if (!sweep && dtheta > 0) dtheta -= 360;
        if (sweep && dtheta < 0) dtheta += 360;

        path.AddArc((float)(cx - rx), (float)(cy - ry), (float)(rx * 2), (float)(ry * 2),
                    (float)startAngle, (float)dtheta);
    }

    private static List<string> TokenizePath(string d)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < d.Length)
        {
            char c = d[i];
            if (char.IsWhiteSpace(c) || c == ',') { i++; continue; }
            if (char.IsLetter(c)) { tokens.Add(c.ToString()); i++; continue; }
            int start = i;
            if (c == '-' || c == '+') i++;
            while (i < d.Length && (char.IsDigit(d[i]) || d[i] == '.')) i++;
            if (i < d.Length && (d[i] == 'e' || d[i] == 'E'))
            {
                i++;
                if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
                while (i < d.Length && char.IsDigit(d[i])) i++;
            }
            if (i > start) tokens.Add(d[start..i]);
            else i++;
        }
        return tokens;
    }

    private static float F(List<string> tokens, int i) =>
        i < tokens.Count && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static Color? GetColor(XElement el, string attr)
    {
        var val = el.Attribute(attr)?.Value;
        if (val == null || val == "none") return null;
        if (val.StartsWith('#') && val.Length == 7)
        {
            int r = Convert.ToInt32(val[1..3], 16);
            int g = Convert.ToInt32(val[3..5], 16);
            int b = Convert.ToInt32(val[5..7], 16);
            return Color.FromArgb(r, g, b);
        }
        return null;
    }

    private static float GetFloat(XElement el, string attr, float def)
    {
        var val = el.Attribute(attr)?.Value;
        return val != null && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : def;
    }
}
