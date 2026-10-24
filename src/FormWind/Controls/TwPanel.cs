using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>
/// A panel that paints anti-aliased rounded corners and borders. With the <c>flex</c> or <c>grid</c> class it lays out
/// its children with the FormWind layout engine (Dock and Anchor are ignored). <c>animate-pulse</c> makes its background
/// pulse (skeleton placeholders). A translucent background (<c>bg-white/70</c>) shows the parent's painting (its gradient or
/// image) through, and <c>backdrop-blur-*</c> blurs it first: a frosted card or sidebar, the in-window counterpart of
/// <c>backdrop-acrylic</c>.
/// </summary>
[ToolboxItem(true)]
[Description("Panel with rounded corners and flex/grid layout.")]
public partial class TwPanel : Panel, ITwChrome, ITwLayoutHost, ITwAnimated
{
    private TwChrome _chrome = TwChrome.None;
    private TwAnimation _animation;
    private int _backdropBlur;
    private Bitmap? _backdrop;
    private Control? _watchedParent;

    public TwPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);
    }

    [Category("FormWind"), DefaultValue(0f), Description("Corner radius (pixels). The value from Classes wins when set.")]
    public float CornerRadius
    {
        get => _chrome.Radius;
        set => SetUniform(Math.Max(0f, value), _chrome.BorderTop, _chrome.BorderColor);
    }

    [Category("FormWind"), DefaultValue(0f), Description("Border width (pixels, all sides).")]
    public float BorderWidth
    {
        get => _chrome.BorderTop;
        set => SetUniform(_chrome.Radius, Math.Max(0f, value), _chrome.BorderColor);
    }

    [Category("FormWind"), Description("Border color.")]
    public Color BorderColor
    {
        get => _chrome.BorderColor;
        set => SetUniform(_chrome.Radius, _chrome.BorderTop, value);
    }

    private void SetUniform(float radius, float width, Color color)
    {
        _chrome = new TwChrome(radius, width, width, width, width, color, _chrome.Gradient);
        Invalidate();
    }

    private bool ShouldSerializeBorderColor() => !_chrome.BorderColor.IsEmpty;

    private void ResetBorderColor() => BorderColor = Color.Empty;

    /// <summary>Blur radius (96 DPI pixels) of what is behind the panel; 0 = none. Set with <c>backdrop-blur-*</c>.</summary>
    [Category("FormWind"), DefaultValue(0), Description("Blur radius of what is behind the panel (backdrop-blur-*).")]
    public int BackdropBlur
    {
        get => _backdropBlur;
        set
        {
            value = Math.Max(0, value);
            if (_backdropBlur == value) return;
            _backdropBlur = value;
            DropBackdrop();
            WatchParent();
            Invalidate();
        }
    }

    // Arkası görünen yüzey: yarı saydam zemin ya da buzlu cam.
    private bool SeesThrough => Parent != null && (_backdropBlur > 0 || BackColor.A is > 0 and < 255);

    private void DropBackdrop()
    {
        _backdrop?.Dispose();
        _backdrop = null;
    }

    // Üst kontrol yeniden çizilince arkadaki görüntü de yenilenir.
    private void WatchParent()
    {
        var parent = SeesThrough ? Parent : null;
        if (parent == _watchedParent) return;
        if (_watchedParent != null) _watchedParent.Invalidated -= OnParentInvalidated;
        _watchedParent = parent;
        if (parent != null) parent.Invalidated += OnParentInvalidated;
    }

    private void OnParentInvalidated(object? sender, InvalidateEventArgs e)
    {
        if (_backdrop is null || !e.InvalidRect.IntersectsWith(Bounds)) return;
        DropBackdrop();
        Invalidate();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        DropBackdrop();
        WatchParent();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        WatchParent();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        DropBackdrop();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        DropBackdrop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_watchedParent != null) _watchedParent.Invalidated -= OnParentInvalidated;
            _watchedParent = null;
            DropBackdrop();
        }
        base.Dispose(disposing);
    }

    // Üst kontrolün çizimi (zemini, geçişi, resmi; kardeşler hariç), gerekiyorsa bulanıklaştırılmış; boyut değişene kadar saklanır.
    private Bitmap Backdrop()
    {
        if (_backdrop != null && _backdrop.Size == ClientSize) return _backdrop;
        DropBackdrop();
        var bitmap = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Parent?.BackColor is Color { A: 255 } back ? back : SystemColors.Control);
            if (Parent is Control parent)
            {
                // Üst kontrol kendi koordinatlarında, bu panelin alanına kırpılarak çizilir (ButtonRenderer.DrawParentBackground
                // yalnızca görsel stiller açıkken çizer).
                g.TranslateTransform(-Left, -Top);
                using var args = new PaintEventArgs(g, Bounds);
                try
                {
                    InvokePaintBackground(parent, args);
                    InvokePaint(parent, args);
                }
                catch (Exception ex) when (!TwErrors.IsCritical(ex))
                {
                    // Üst kontrolün çizimi başarısızsa düz zemin kalır.
                }
            }
        }
        if (_backdropBlur > 0) TwBlur.Apply(bitmap, _backdropBlur * TwDpi.DeviceDpi(this) / 96f);
        return _backdrop = bitmap;
    }

    public override LayoutEngine LayoutEngine =>
        TwLayout.IsLayoutContainer(this) ? TwLayout.Engine : base.LayoutEngine;

    /// <summary>Computed from the content for a flex container; with <c>AutoSize = true</c> the panel sizes to its content.</summary>
    public override Size GetPreferredSize(Size proposedSize) =>
        TwLayout.TryMeasure(this, out var size) ? size : base.GetPreferredSize(proposedSize);

    void ITwChrome.SetChrome(TwChrome chrome)
    {
        if (_chrome.Equals(chrome)) return;
        bool borderChanged = BorderInsets() != Insets(chrome);
        _chrome = chrome;
        if (borderChanged) PerformLayout(this, nameof(BorderWidth));
        Invalidate();
    }

    /// <summary>
    /// As in the CSS box model the content is inside the border: children (Dock and flex/grid) are not placed over the border.
    /// </summary>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var rect = base.DisplayRectangle;
            var border = BorderInsets();
            return new Rectangle(
                rect.X + border.Left,
                rect.Y + border.Top,
                Math.Max(0, rect.Width - border.Horizontal),
                Math.Max(0, rect.Height - border.Vertical));
        }
    }

    /// <summary>Border widths (rounded to pixels); the content starts this far inside.</summary>
    public Padding BorderInsets() => Insets(_chrome);

    private static Padding Insets(TwChrome chrome) => new Padding(
        (int)Math.Ceiling(chrome.BorderLeft),
        (int)Math.Ceiling(chrome.BorderTop),
        (int)Math.Ceiling(chrome.BorderRight),
        (int)Math.Ceiling(chrome.BorderBottom));

    // Çocukların doğal boyutları da ölçeklenir (bkz. TwLayout.OnHostScaling); WinForms çocukları bu çağrıdan sonra ölçekler.
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        TwLayout.OnHostScaling(this, factor);
        base.ScaleControl(factor, specified);
    }

    // Sağdan sola değişince flex/grid yerleşimi aynalanır (bkz. TwLayoutEngine).
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        PerformLayout();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        TwPainter.PaintParentBackground(this, e.Graphics);
        if (SeesThrough)
        {
            // Köşelerin dışı üst kontrolün zemini kalır; içi arkadaki çizim (bulanık), üstüne yarı saydam zemin.
            using var path = TwPainter.CreateRoundedRect(ClientRectangle, TwPainter.EffectiveRadius(_chrome.Radius, ClientSize));
            var state = e.Graphics.Save();
            e.Graphics.SetClip(path, System.Drawing.Drawing2D.CombineMode.Intersect);
            e.Graphics.DrawImageUnscaled(Backdrop(), 0, 0);
            e.Graphics.Restore(state);
        }
        // animate-pulse: zemin ve kenarlık ebeveynin zeminine doğru solup geri gelir (iskelet yer tutucu); çocuklar etkilenmez.
        if (_animation == TwAnimation.Pulse)
        {
            float opacity = TwAnimations.PulseOpacity(this);
            TwPainter.FillSurface(e.Graphics, ClientRectangle, _chrome.Radius, TwAnimations.Fade(BackColor, opacity), _chrome.Gradient);
            TwPainter.StrokeChrome(e.Graphics, ClientRectangle, _chrome.WithBorderColor(TwAnimations.Fade(_chrome.BorderColor, opacity)));
            return;
        }
        TwPainter.FillSurface(e.Graphics, ClientRectangle, _chrome.Radius, BackColor, _chrome.Gradient);
        TwPainter.StrokeChrome(e.Graphics, ClientRectangle, _chrome);
    }

    void ITwAnimated.SetAnimation(TwAnimation animation)
    {
        // Döndürme panelde anlamsızdır; yalnızca nabız çizilir.
        if (animation == TwAnimation.Spin) animation = TwAnimation.None;
        if (_animation == animation) return;
        _animation = animation;
        TwAnimations.Track(this, animation);
        Invalidate();
    }
}
