using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Rounded rectangle drawing helpers. All drawing is anti-aliased with a half-pixel offset.</summary>
internal static class TwPainter
{
    /// <summary>Limits the radius to half of the control's shorter side (<c>rounded-full</c> = pill shape).</summary>
    public static float EffectiveRadius(float radius, Size size) =>
        Math.Max(0f, Math.Min(radius, Math.Min(size.Width, size.Height) / 2f));

    public static GraphicsPath CreateRoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0.5f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        float diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Fills the area outside the corners with the parent's background color.</summary>
    public static void PaintParentBackground(Control control, Graphics graphics)
    {
        var color = control.Parent?.BackColor ?? SystemColors.Control;
        if (color.A < 255) color = SystemColors.Control;
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, control.ClientRectangle);
    }

    public static void FillRounded(Graphics graphics, Rectangle bounds, float radius, Color color)
    {
        if (color.A == 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
        using var brush = new SolidBrush(color);
        FillRounded(graphics, bounds, radius, brush);
    }

    /// <summary>Fills the background with a solid color or (if any) a gradient.</summary>
    public static void FillSurface(Graphics graphics, Rectangle bounds, float radius, Color color, TwGradient? gradient)
    {
        if (gradient is TwGradient g && bounds.Width > 0 && bounds.Height > 0)
        {
            using var brush = g.CreateBrush(bounds);
            FillRounded(graphics, bounds, radius, brush);
            return;
        }
        FillRounded(graphics, bounds, radius, color);
    }

    /// <summary>Draws the border: a rounded frame when all sides are equal, otherwise only the defined sides (without corners).</summary>
    public static void StrokeChrome(Graphics graphics, Rectangle bounds, TwChrome chrome)
    {
        if (!chrome.HasBorder || chrome.BorderColor.A == 0) return;
        if (chrome.IsUniformBorder)
        {
            StrokeRounded(graphics, bounds, chrome.Radius, chrome.BorderTop, chrome.BorderColor);
            return;
        }

        using var brush = new SolidBrush(chrome.BorderColor);
        if (chrome.BorderTop > 0) graphics.FillRectangle(brush, new RectangleF(bounds.X, bounds.Y, bounds.Width, chrome.BorderTop));
        if (chrome.BorderBottom > 0) graphics.FillRectangle(brush, new RectangleF(bounds.X, bounds.Bottom - chrome.BorderBottom, bounds.Width, chrome.BorderBottom));
        if (chrome.BorderLeft > 0) graphics.FillRectangle(brush, new RectangleF(bounds.X, bounds.Y, chrome.BorderLeft, bounds.Height));
        if (chrome.BorderRight > 0) graphics.FillRectangle(brush, new RectangleF(bounds.Right - chrome.BorderRight, bounds.Y, chrome.BorderRight, bounds.Height));
    }

    private static void FillRounded(Graphics graphics, Rectangle bounds, float radius, Brush brush)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var r = EffectiveRadius(radius, bounds.Size);
        if (r <= 0.5f)
        {
            graphics.FillRectangle(brush, bounds);
            return;
        }

        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        using (var path = CreateRoundedRect(bounds, r))
            graphics.FillPath(brush, path);
        graphics.Restore(state);
    }

    public static void StrokeRounded(Graphics graphics, Rectangle bounds, float radius, float width, Color color)
    {
        if (width <= 0 || color.A == 0 || bounds.Width <= width || bounds.Height <= width) return;

        float inset = width / 2f;
        var rect = new RectangleF(bounds.X + inset, bounds.Y + inset, bounds.Width - width, bounds.Height - width);
        var r = Math.Max(0f, EffectiveRadius(radius, bounds.Size) - inset);

        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        using (var pen = new Pen(color, width))
        using (var path = CreateRoundedRect(rect, r))
            graphics.DrawPath(pen, path);
        graphics.Restore(state);
    }

    public static TextFormatFlags ToTextFormatFlags(ContentAlignment alignment)
    {
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        int value = (int)alignment;

        if ((value & 0x111) != 0) flags |= TextFormatFlags.Left;
        else if ((value & 0x222) != 0) flags |= TextFormatFlags.HorizontalCenter;
        else flags |= TextFormatFlags.Right;

        if ((value & 0x7) != 0) flags |= TextFormatFlags.Top;
        else if ((value & 0x70) != 0) flags |= TextFormatFlags.VerticalCenter;
        else flags |= TextFormatFlags.Bottom;

        return flags;
    }
}
