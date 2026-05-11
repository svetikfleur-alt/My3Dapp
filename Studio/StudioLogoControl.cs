using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace My3DApp.Studio;

internal sealed class StudioLogoControl : Control
{
    internal StudioLogoControl()
    {
        DoubleBuffered = true;
        Size = new Size(58, 40);
        MinimumSize = new Size(58, 40);
        MaximumSize = new Size(58, 40);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var cardRect = new RectangleF(2, 2, Width - 4, Height - 4);
        using var cardPath = CreateRoundedRect(cardRect, 8f);
        using var cardBrush = new SolidBrush(Color.FromArgb(33, 56, 122));
        using var cardBorder = new Pen(Color.FromArgb(92, 122, 201), 1f);

        e.Graphics.FillPath(cardBrush, cardPath);
        e.Graphics.DrawPath(cardBorder, cardPath);

        using var markFont = new Font("Segoe UI Black", 15f, FontStyle.Bold);
        using var markBrush = new SolidBrush(Color.White);
        e.Graphics.DrawString("M", markFont, markBrush, 8.5f, 8f);

        using var accentFont = new Font("Segoe UI Black", 12f, FontStyle.Bold);
        using var accentBrush = new SolidBrush(Color.FromArgb(71, 214, 192));
        e.Graphics.DrawString("3", accentFont, accentBrush, 32.2f, 11.4f);

        using var accentBar = new SolidBrush(Color.FromArgb(71, 214, 192));
        e.Graphics.FillRectangle(accentBar, 30f, 28f, 16f, 2f);
    }

    private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
