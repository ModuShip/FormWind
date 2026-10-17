using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FormWind;

public enum TwImageFit
{
    /// <summary>Fills the box and clips the overflow (keeps the aspect ratio).</summary>
    Cover,

    /// <summary>The whole image is visible and space may remain (keeps the aspect ratio).</summary>
    Contain,

    /// <summary>Kutuya gerilir (oran korunmaz).</summary>
    Fill,
}

/// <summary>
/// Picture box with smooth rounded corners (<c>rounded-lg</c>, <c>rounded-full</c> avatar). Placement is set with
/// <c>object-cover</c> / <c>object-contain</c> / <c>object-fill</c>; without an image the <c>bg-*</c> color shows.
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Image))]
[Description("Picture box with rounded corners.")]
public class TwPictureBox : Control, ITwChrome
{
    private TwChrome _chrome = TwChrome.None;
    private Image? _image;
    private TwImageFit _fit = TwImageFit.Cover;
    private Bitmap? _scaled;

    protected override AccessibleObject CreateAccessibilityInstance() => new TwRoleAccessibleObject(this, AccessibleRole.Graphic);

    public TwPictureBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    [Category("Appearance"), DefaultValue(null)]
    public Image? Image
    {
        get => _image;
        set
        {
            _image = value;
            InvalidateScaled();
        }
    }

    [Category("FormWind"), DefaultValue(TwImageFit.Cover)]
    public TwImageFit Fit
    {
        get => _fit;
        set
        {
            _fit = value;
            InvalidateScaled();
        }
    }

    protected override Size DefaultSize => new Size(64, 64);

    void ITwChrome.SetChrome(TwChrome chrome)
    {
        if (_chrome.Equals(chrome)) return;
        _chrome = chrome;
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateScaled();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _scaled?.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        var bounds = ClientRectangle;
        TwPainter.FillSurface(g, bounds, _chrome.Radius, BackColor, _chrome.Gradient);

        var scaled = GetScaled();
        if (scaled != null)
        {
            // Yuvarlak kırpma: Region yerine doku fırçasıyla antialias dolgu.
            using var brush = new TextureBrush(scaled, WrapMode.Clamp);
            var radius = TwPainter.EffectiveRadius(_chrome.Radius, bounds.Size);
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using (var path = TwPainter.CreateRoundedRect(bounds, radius))
                g.FillPath(brush, path);
            g.Restore(state);
        }

        TwPainter.StrokeChrome(g, bounds, _chrome);
    }

    private void InvalidateScaled()
    {
        _scaled?.Dispose();
        _scaled = null;
        Invalidate();
    }

    // Resim kutu boyutunda bir kez ölçeklenip saklanır; her boyamada yeniden ölçeklenmez.
    private Bitmap? GetScaled()
    {
        if (_image is null || Width <= 0 || Height <= 0) return null;
        if (_scaled != null) return _scaled;

        var target = new Bitmap(Width, Height);
        using (var g = Graphics.FromImage(target))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(_image, Destination(_image.Size, new Size(Width, Height), _fit));
        }
        return _scaled = target;
    }

    internal static Rectangle Destination(Size image, Size box, TwImageFit fit)
    {
        if (fit == TwImageFit.Fill || image.Width <= 0 || image.Height <= 0) return new Rectangle(Point.Empty, box);

        float scaleX = box.Width / (float)image.Width, scaleY = box.Height / (float)image.Height;
        float scale = fit == TwImageFit.Cover ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
        int w = (int)Math.Round(image.Width * scale), h = (int)Math.Round(image.Height * scale);
        return new Rectangle((box.Width - w) / 2, (box.Height - h) / 2, w, h);
    }
}
