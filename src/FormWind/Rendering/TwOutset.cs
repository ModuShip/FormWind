using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>A shadow layer (CSS box-shadow): vertical offset, blur, spread and color. Values are in pixels.</summary>
internal readonly struct TwShadowLayer : IEquatable<TwShadowLayer>
{
    public TwShadowLayer(float offsetY, float blur, float spread, Color color)
    {
        OffsetY = offsetY;
        Blur = blur;
        Spread = spread;
        Color = color;
    }

    public float OffsetY { get; }
    public float Blur { get; }
    public float Spread { get; }
    public Color Color { get; }

    public TwShadowLayer Scale(float scale) => new TwShadowLayer(OffsetY * scale, Blur * scale, Spread * scale, Color);

    /// <summary>Tailwind v3 shadow presets; <paramref name="color"/> replaces the layer colors when given.</summary>
    public static TwShadowLayer[] Preset(TwShadowSize size, Color? color)
    {
        Color C(double alpha) => color ?? Color.FromArgb((int)Math.Round(alpha * 255), 0, 0, 0);
        return size switch
        {
            TwShadowSize.Sm => new[] { new TwShadowLayer(1, 2, 0, C(0.05)) },
            TwShadowSize.Base => new[] { new TwShadowLayer(1, 3, 0, C(0.1)), new TwShadowLayer(1, 2, -1, C(0.1)) },
            TwShadowSize.Md => new[] { new TwShadowLayer(4, 6, -1, C(0.1)), new TwShadowLayer(2, 4, -2, C(0.1)) },
            TwShadowSize.Lg => new[] { new TwShadowLayer(10, 15, -3, C(0.1)), new TwShadowLayer(4, 6, -4, C(0.1)) },
            TwShadowSize.Xl => new[] { new TwShadowLayer(20, 25, -5, C(0.1)), new TwShadowLayer(8, 10, -6, C(0.1)) },
            TwShadowSize.Xl2 => new[] { new TwShadowLayer(25, 50, -12, C(0.25)) },
            _ => Array.Empty<TwShadowLayer>(),
        };
    }

    public bool Equals(TwShadowLayer other) =>
        OffsetY == other.OffsetY && Blur == other.Blur && Spread == other.Spread && Color.ToArgb() == other.Color.ToArgb();

    public override bool Equals(object? obj) => obj is TwShadowLayer other && Equals(other);

    public override int GetHashCode() => OffsetY.GetHashCode() ^ (Blur.GetHashCode() * 7) ^ Color.ToArgb();
}

/// <summary>
/// Decorations that extend outside the control: shadow layers, focus ring and the frame of native controls
/// (<see cref="Frame"/>: border and corners of TextBoxes that cannot draw their own border).
/// </summary>
internal sealed class TwOutsetSpec : IEquatable<TwOutsetSpec>
{
    public static readonly Color DefaultRingColor = Color.FromArgb(128, 59, 130, 246); // Tailwind: blue-500 / %50

    public TwOutsetSpec(float radius, IReadOnlyList<TwShadowLayer> shadows, float ringWidth, Color ringColor, float ringOffset, Color ringOffsetColor)
    {
        Radius = radius;
        Shadows = shadows;
        RingWidth = ringWidth;
        RingColor = ringColor;
        RingOffset = ringOffset;
        RingOffsetColor = ringOffsetColor;
    }

    public float Radius { get; }
    public IReadOnlyList<TwShadowLayer> Shadows { get; }
    public float RingWidth { get; }
    public Color RingColor { get; }
    public float RingOffset { get; }
    public Color RingOffsetColor { get; }

    /// <summary>A border drawn around the control and filled with its background color; <c>null</c> = no frame.</summary>
    public TwChrome? Frame { get; private set; }

    /// <summary>Space between the frame and the control (pixels).</summary>
    public float FrameGap { get; private set; }

    public bool IsEmpty => Shadows.Count == 0 && RingWidth <= 0 && Frame is null;

    /// <summary>How far the frame extends outside the control's bounds (per side).</summary>
    public Padding FrameInset => Frame is TwChrome frame
        ? new Padding(
            (int)Math.Ceiling(FrameGap + frame.BorderLeft), (int)Math.Ceiling(FrameGap + frame.BorderTop),
            (int)Math.Ceiling(FrameGap + frame.BorderRight), (int)Math.Ceiling(FrameGap + frame.BorderBottom))
        : Padding.Empty;

    /// <summary>A copy with the same shadow and ring that also draws the given frame.</summary>
    public TwOutsetSpec WithFrame(TwChrome frame, float gap) =>
        new TwOutsetSpec(Radius, Shadows, RingWidth, RingColor, RingOffset, RingOffsetColor) { Frame = frame, FrameGap = gap };

    /// <summary>The largest distance a shadow or ring extends outside the control's bounds.</summary>
    public int Extent
    {
        get
        {
            var inset = FrameInset;
            float frame = Math.Max(Math.Max(inset.Left, inset.Right), Math.Max(inset.Top, inset.Bottom));
            float extent = frame + RingWidth + RingOffset;
            foreach (var layer in Shadows)
                extent = Math.Max(extent, frame + layer.Blur + Math.Max(0, layer.Spread) + Math.Abs(layer.OffsetY));
            return (int)Math.Ceiling(extent) + 2;
        }
    }

    public static TwOutsetSpec FromStyle(TwStyle style, float scale)
    {
        var layers = TwShadowLayer.Preset(style.Shadow ?? TwShadowSize.None, style.ShadowColor);
        for (int i = 0; i < layers.Length; i++) layers[i] = layers[i].Scale(scale);
        return new TwOutsetSpec(
            (style.Radius ?? 0) * scale,
            layers,
            (style.RingWidth ?? 0) * scale,
            style.RingColor ?? DefaultRingColor,
            (style.RingOffset ?? 0) * scale,
            style.RingOffsetColor ?? Color.White);
    }

    public bool Equals(TwOutsetSpec? other)
    {
        if (other is null) return false;
        if (Radius != other.Radius || RingWidth != other.RingWidth || RingOffset != other.RingOffset
            || RingColor.ToArgb() != other.RingColor.ToArgb() || RingOffsetColor.ToArgb() != other.RingOffsetColor.ToArgb()
            || Shadows.Count != other.Shadows.Count
            || !Nullable.Equals(Frame, other.Frame) || FrameGap != other.FrameGap)
            return false;
        for (int i = 0; i < Shadows.Count; i++)
            if (!Shadows[i].Equals(other.Shadows[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as TwOutsetSpec);

    public override int GetHashCode() => Radius.GetHashCode() ^ RingWidth.GetHashCode() ^ Shadows.Count;
}

/// <summary>
/// Draws shadows and rings on the control's <b>parent</b>: controls cannot paint outside their own bounds.
/// The parent's Paint event runs before the child windows paint, so the shadow naturally stays below the control.
/// Works on every parent that raises Paint, such as Form, Panel and TwPanel. With <c>transition</c> (or
/// <c>transition-shadow</c>), a shadow or ring change (e.g. <c>shadow-sm hover:shadow-lg</c>, <c>focus:ring-2</c>) is animated
/// by cross-fading the old and new ones; the blurred shadow bitmaps come from the cache and are not recomputed per frame.
/// </summary>
internal sealed class TwOutset
{
    private static readonly ConditionalWeakTable<Control, TwOutset> Table = new ConditionalWeakTable<Control, TwOutset>();
    private static readonly ConditionalWeakTable<Control, ParentPainter> Painters = new ConditionalWeakTable<Control, ParentPainter>();

    private readonly Control _control;
    private Control? _parent;
    private Rectangle _paintedBounds;
    private readonly Timer _timer = new Timer { Interval = 16 };
    private IReadOnlyList<TwShadowLayer> _fromShadows = Array.Empty<TwShadowLayer>();
    private TwOutsetSpec? _fromRing;
    private int _fromExtent;
    private DateTime _start;
    private TwTransition _timing;
    // Geçişte belirerek çizilenler; değişmeyen tam opak kalır.
    private bool _shadowsFade, _ringFade;
    // Klavye odağıyla beliren halka dışarıdan yerine daralır.
    private DateTime _ringInStart;
    private bool _ringIn;

    /// <summary>Time (ms) a focus ring takes to close in onto the control.</summary>
    internal const int RingInDuration = 180;

    /// <summary>How far (96 DPI px) outside its place a focus ring starts.</summary>
    internal const int RingInDistance = 6;

    private TwOutset(Control control)
    {
        _control = control;
        _control.ParentChanged += (_, _) => Reattach();
        _control.LocationChanged += (_, _) => InvalidateParent();
        _control.SizeChanged += (_, _) => InvalidateParent();
        _control.VisibleChanged += (_, _) => InvalidateParent();
        // Çerçevenin zemini kontrolün o anki rengidir (geçiş animasyonları dahil).
        _control.BackColorChanged += (_, _) => { if (Spec?.Frame != null) InvalidateParent(); };
        _control.Disposed += (_, _) => _timer.Dispose();
        _timer.Tick += (_, _) =>
        {
            if (Progress >= 1f && RingInProgress >= 1f)
            {
                _timer.Stop();
                _ringIn = false;
            }
            InvalidateParent();
        };
        Reattach();
    }

    // Gölge ve halka geçişinin (eğrisi uygulanmış) ilerlemesi; geçiş yoksa 1.
    internal float Progress
    {
        get
        {
            double elapsed = (DateTime.UtcNow - _start).TotalMilliseconds;
            return _timing.TotalMs > 0 && elapsed < _timing.TotalMs ? _timing.Progress(elapsed) : 1f;
        }
    }

    internal bool Animating => Progress < 1f || RingInProgress < 1f;

    /// <summary>Progress of a focus ring closing in (eased); 1 when none runs.</summary>
    internal float RingInProgress
    {
        get
        {
            if (!_ringIn) return 1f;
            double elapsed = (DateTime.UtcNow - _ringInStart).TotalMilliseconds;
            return elapsed >= RingInDuration ? 1f : TwMotion.Ease(TwEasing.Out, (float)(elapsed / RingInDuration));
        }
    }

    /// <summary>How far (device px) outside its place the ring is drawn now.</summary>
    internal int RingInSpread => (int)Math.Round(RingInDistance * TwDpi.DeviceDpi(_control) / 96f * (1 - RingInProgress));

    public TwOutsetSpec? Spec { get; private set; }

    public static bool IsAttached(Control control) => Table.TryGetValue(control, out _);

    public static TwOutset For(Control control) => Table.GetValue(control, c => new TwOutset(c));

    public void Update(TwOutsetSpec spec) => Update(spec, TwTransition.None);

    /// <param name="spec">New shadow, ring and frame.</param>
    /// <param name="timing">Transition of the shadow and ring change (<c>transition</c>); none = immediate.</param>
    public void Update(TwOutsetSpec spec, TwTransition timing)
    {
        if (Spec != null && Spec.Equals(spec)) return;
        var old = Spec;
        var oldShadows = old?.Shadows ?? Array.Empty<TwShadowLayer>();
        int oldExtent = old?.Extent ?? 0;
        Spec = spec.IsEmpty ? null : spec;

        bool shadowsChanged = !SameShadows(oldShadows, spec.Shadows);
        bool ringChanged = !SameRing(old, spec);
        // Odak klavyeyle geldiyse beliren halka dıştan içe daralır (transition sınıfı olmasa da).
        if ((old?.RingWidth ?? 0) <= 0 && spec.RingWidth > 0 && _control.Focused && TwFocusVisible.LastInputWasKeyboard
            && TwMotion.CanAnimate(_control))
        {
            _ringIn = true;
            _ringInStart = DateTime.UtcNow;
            _timer.Start();
        }
        else if (spec.RingWidth <= 0) _ringIn = false;
        if (timing.TotalMs > 0 && TwMotion.CanAnimate(_control) && (shadowsChanged || ringChanged))
        {
            // Geçiş sürerken yeniden değişirse o anki görüntü yerine eski hedeften başlanır (kısa geçişte fark edilmez).
            _fromShadows = shadowsChanged ? oldShadows : Array.Empty<TwShadowLayer>();
            _fromRing = ringChanged && old is { RingWidth: > 0 } ? old : null;
            _fromExtent = Math.Max(oldExtent, _fromExtent);
            _start = DateTime.UtcNow;
            _timing = timing;
            _shadowsFade = shadowsChanged;
            _ringFade = ringChanged;
            _timer.Start();
        }
        else
        {
            _timing = TwTransition.None;
            if (!_ringIn) _timer.Stop();
            _fromShadows = Array.Empty<TwShadowLayer>();
            _fromRing = null;
            _fromExtent = 0;
        }
        InvalidateParent();
    }

    private static bool SameRing(TwOutsetSpec? a, TwOutsetSpec b)
    {
        float width = a?.RingWidth ?? 0;
        if (width <= 0 && b.RingWidth <= 0) return true;
        return a != null && width == b.RingWidth && a.RingOffset == b.RingOffset
               && a.RingColor.ToArgb() == b.RingColor.ToArgb() && a.RingOffsetColor.ToArgb() == b.RingOffsetColor.ToArgb();
    }

    private static bool SameShadows(IReadOnlyList<TwShadowLayer> a, IReadOnlyList<TwShadowLayer> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!a[i].Equals(b[i])) return false;
        return true;
    }

    internal void Paint(Graphics graphics) => Paint(graphics, null);

    // clip: ebeveynin geçersiz bölgesi; dışında kalan gölge/çerçeve çizilmez (null = çiz).
    internal void Paint(Graphics graphics, Rectangle? clip)
    {
        var spec = Spec;
        float progress = Progress;
        if (!_control.Visible || spec is null && (progress >= 1f || _fromShadows.Count == 0 && _fromRing is null)) return;

        var bounds = _control.Bounds;
        int spread = RingInSpread;
        int extent = Math.Max((spec?.Extent ?? 0) + spread, progress < 1f ? _fromExtent : 0);
        _paintedBounds = Rectangle.Inflate(bounds, extent, extent);
        // Bir kontrolün geçersiz kılması ebeveyni yalnızca o bölgede yeniden çizer; diğer gölge ve çerçeveler atlanır.
        if (clip is Rectangle area && !area.IntersectsWith(_paintedBounds)) return;

        // Çerçeve varsa gölge ve halka onun dışına çizilir.
        var inset = spec?.FrameInset ?? Padding.Empty;
        bounds = Rectangle.FromLTRB(bounds.Left - inset.Left, bounds.Top - inset.Top, bounds.Right + inset.Right, bounds.Bottom + inset.Bottom);
        float shadowRadius = spec?.Radius ?? 0;

        // Gölgeler, en geniş katman önce; geçişte eski gölge solarken yenisi belirir.
        bool fading = progress < 1f;
        if (fading)
            for (int i = _fromShadows.Count - 1; i >= 0; i--)
                TwShadowRenderer.Draw(graphics, bounds, shadowRadius, _fromShadows[i], 1f - progress);
        if (fading && _fromRing != null) DrawRing(graphics, bounds, _fromRing, 1f - progress);
        if (spec is null) return;
        float shadowOpacity = fading && _shadowsFade ? progress : 1f;
        for (int i = spec.Shadows.Count - 1; i >= 0; i--)
            TwShadowRenderer.Draw(graphics, bounds, spec.Radius, spec.Shadows[i], shadowOpacity);

        if (spec.Frame is TwChrome frame)
        {
            TwPainter.FillRounded(graphics, bounds, frame.Radius, _control.BackColor);
            TwPainter.StrokeChrome(graphics, bounds, frame);
        }

        if (spec.RingWidth > 0)
        {
            float opacity = fading && _ringFade ? progress : 1f;
            // Daralan halka belirerek gelir.
            if (spread > 0) opacity *= 0.35f + 0.65f * RingInProgress;
            DrawRing(graphics, Rectangle.Inflate(bounds, spread, spread), spec, opacity);
        }
    }

    private static void DrawRing(Graphics graphics, Rectangle bounds, TwOutsetSpec spec, float opacity)
    {
        if (opacity <= 0f) return;
        Color Fade(Color color) => opacity >= 1f ? color : Color.FromArgb((int)Math.Round(color.A * opacity), color);
        var radius = TwPainter.EffectiveRadius(spec.Radius, bounds.Size);
        if (spec.RingOffset > 0)
        {
            var offsetBounds = Rectangle.Inflate(bounds, (int)Math.Ceiling(spec.RingOffset), (int)Math.Ceiling(spec.RingOffset));
            TwPainter.StrokeRounded(graphics, offsetBounds, radius + spec.RingOffset, spec.RingOffset, Fade(spec.RingOffsetColor));
        }
        var grow = (int)Math.Ceiling(spec.RingOffset + spec.RingWidth);
        var ringBounds = Rectangle.Inflate(bounds, grow, grow);
        TwPainter.StrokeRounded(graphics, ringBounds, radius + grow, spec.RingWidth, Fade(spec.RingColor));
    }

    private void Reattach()
    {
        var parent = _control.Parent;
        if (ReferenceEquals(parent, _parent)) return;
        if (_parent != null && Painters.TryGetValue(_parent, out var oldPainter))
        {
            oldPainter.Remove(this);
            _parent.Invalidate(_paintedBounds);
        }
        _parent = parent;
        if (parent != null) Painters.GetValue(parent, p => new ParentPainter(p)).Add(this);
        InvalidateParent();
    }

    private void InvalidateParent()
    {
        if (_parent is null) return;
        // Daralan halka için en dış konumu da kapsar.
        var ringIn = _ringIn ? (int)Math.Ceiling(RingInDistance * TwDpi.DeviceDpi(_control) / 96f) : 0;
        var extent = Math.Max((Spec?.Extent ?? 0) + ringIn, _fromExtent);
        var area = Rectangle.Inflate(_control.Bounds, extent, extent);
        _parent.Invalidate(Rectangle.Union(area, _paintedBounds.IsEmpty ? area : _paintedBounds));
        _paintedBounds = area;
    }

    private sealed class ParentPainter
    {
        private readonly List<WeakReference<TwOutset>> _children = new List<WeakReference<TwOutset>>();

        public ParentPainter(Control parent) => parent.Paint += OnPaint;

        public void Add(TwOutset outset) => _children.Add(new WeakReference<TwOutset>(outset));

        public void Remove(TwOutset outset) =>
            _children.RemoveAll(r => !r.TryGetTarget(out var target) || ReferenceEquals(target, outset));

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            // Controls sırasının tersi: alttaki (sonra eklenen) kontrolün gölgesi önce çizilir.
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                if (!_children[i].TryGetTarget(out var outset))
                {
                    _children.RemoveAt(i);
                    continue;
                }
                // Çizim hatası ebeveynin Paint'ini (ve uygulamayı) düşürmemeli.
                try
                {
                    outset.Paint(e.Graphics, e.ClipRectangle);
                }
                catch (Exception ex) when (!TwErrors.IsCritical(ex))
                {
                    TwErrors.Trace("shadow/ring painting", ex);
                }
            }
        }
    }
}

/// <summary>Produces and caches blurred shadow bitmaps (3-pass box blur ≈ Gaussian).</summary>
internal static class TwShadowRenderer
{
    private const int CacheLimit = 128;
    private static readonly Dictionary<(int W, int H, int Radius, int Blur, int Color), Bitmap> Cache =
        new Dictionary<(int W, int H, int Radius, int Blur, int Color), Bitmap>();

    public static void Draw(Graphics graphics, Rectangle bounds, float radius, TwShadowLayer layer, float opacity = 1f)
    {
        if (layer.Color.A == 0 || opacity <= 0f) return;
        var spread = layer.Spread;
        var rect = RectangleF.Inflate(bounds, spread, spread);
        rect.Offset(0, layer.OffsetY);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        int blur = (int)Math.Ceiling(layer.Blur);
        int margin = blur + 2;
        var effectiveRadius = Math.Max(0, TwPainter.EffectiveRadius(radius, bounds.Size) + spread);
        var bitmap = Get((int)Math.Ceiling(rect.Width), (int)Math.Ceiling(rect.Height), (int)Math.Round(effectiveRadius), blur, layer.Color);
        int x = (int)Math.Round(rect.X) - margin, y = (int)Math.Round(rect.Y) - margin;
        if (opacity >= 1f)
        {
            graphics.DrawImageUnscaled(bitmap, x, y);
            return;
        }
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
        graphics.DrawImage(bitmap, new Rectangle(x, y, bitmap.Width, bitmap.Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
    }

    private static Bitmap Get(int width, int height, int radius, int blur, Color color)
    {
        var key = (width, height, radius, blur, color.ToArgb());
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            if (Cache.Count >= CacheLimit)
            {
                foreach (var bitmap in Cache.Values) bitmap.Dispose();
                Cache.Clear();
            }
            var created = Create(width, height, radius, blur, color);
            Cache[key] = created;
            return created;
        }
    }

    private static Bitmap Create(int width, int height, int radius, int blur, Color color)
    {
        int margin = blur + 2;
        int w = width + margin * 2, h = height + margin * 2;
        var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.Black);
            using var path = TwPainter.CreateRoundedRect(new RectangleF(margin, margin, width, height), radius);
            g.FillPath(brush, path);
        }

        var data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[data.Stride * h];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            var alpha = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    alpha[y * w + x] = pixels[y * data.Stride + x * 4 + 3];

            // CSS blur yarıçapı B ≈ Gauss σ = B/2; üç kutu geçişi için kutu yarıçapı ≈ B/2.
            int boxRadius = blur > 0 ? Math.Max(1, (int)Math.Round(blur / 2.0)) : 0;
            for (int pass = 0; pass < 3 && boxRadius > 0; pass++)
            {
                BoxBlurHorizontal(alpha, w, h, boxRadius);
                BoxBlurVertical(alpha, w, h, boxRadius);
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * data.Stride + x * 4;
                    pixels[i] = color.B;
                    pixels[i + 1] = color.G;
                    pixels[i + 2] = color.R;
                    pixels[i + 3] = (byte)(alpha[y * w + x] * color.A / 255);
                }
            }
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }

    private static void BoxBlurHorizontal(byte[] values, int w, int h, int r)
    {
        var row = new byte[w];
        int size = r * 2 + 1;
        for (int y = 0; y < h; y++)
        {
            int offset = y * w, sum = 0;
            for (int x = -r; x <= r; x++) sum += values[offset + Clamp(x, w)];
            for (int x = 0; x < w; x++)
            {
                row[x] = (byte)(sum / size);
                sum += values[offset + Clamp(x + r + 1, w)] - values[offset + Clamp(x - r, w)];
            }
            Buffer.BlockCopy(row, 0, values, offset, w);
        }
    }

    private static void BoxBlurVertical(byte[] values, int w, int h, int r)
    {
        var column = new byte[h];
        int size = r * 2 + 1;
        for (int x = 0; x < w; x++)
        {
            int sum = 0;
            for (int y = -r; y <= r; y++) sum += values[Clamp(y, h) * w + x];
            for (int y = 0; y < h; y++)
            {
                column[y] = (byte)(sum / size);
                sum += values[Clamp(y + r + 1, h) * w + x] - values[Clamp(y - r, h) * w + x];
            }
            for (int y = 0; y < h; y++) values[y * w + x] = column[y];
        }
    }

    private static int Clamp(int value, int length) => value < 0 ? 0 : value >= length ? length - 1 : value;
}
