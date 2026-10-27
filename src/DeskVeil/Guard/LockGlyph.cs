using System.Drawing.Drawing2D;

namespace DeskVeil.Guard;

/// <summary>Vector padlock used by the curtain and the tray icon, so no image assets are needed.</summary>
internal static class LockGlyph
{
    public static void Draw(Graphics graphics, RectangleF bounds, Color color, bool open)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var size = Math.Min(bounds.Width, bounds.Height);
        var left = bounds.X + (bounds.Width - size) / 2;
        var top = bounds.Y + (bounds.Height - size) / 2;

        var bodyWidth = size * 0.78f;
        var bodyHeight = size * 0.5f;
        var bodyLeft = left + (size - bodyWidth) / 2;
        var bodyTop = top + size - bodyHeight;

        var stroke = Math.Max(1.5f, size * 0.11f);
        var shackleWidth = bodyWidth * 0.62f;
        var shackleLeft = left + (size - shackleWidth) / 2 + (open ? shackleWidth * 0.45f : 0);
        var shackleTop = top + stroke / 2;
        var shackleHeight = (bodyTop - shackleTop) * 2;

        using (var pen = new Pen(color, stroke))
        {
            pen.StartCap = LineCap.Flat;
            pen.EndCap = LineCap.Flat;
            graphics.DrawArc(pen, shackleLeft, shackleTop, shackleWidth, shackleHeight, 180, 180);
            var legBottom = bodyTop + stroke;
            graphics.DrawLine(pen, shackleLeft + shackleWidth, shackleTop + shackleHeight / 2, shackleLeft + shackleWidth, legBottom);
            var leftLegBottom = open ? shackleTop + shackleHeight / 2 + size * 0.06f : legBottom;
            graphics.DrawLine(pen, shackleLeft, shackleTop + shackleHeight / 2, shackleLeft, leftLegBottom);
        }

        using var body = RoundedRectangle(new RectangleF(bodyLeft, bodyTop, bodyWidth, bodyHeight), size * 0.08f);
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, body);
    }

    private static GraphicsPath RoundedRectangle(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
