using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    private readonly TwFrameTimer _timer = new TwFrameTimer();
    private IReadOnlyList<TwShadowLayer> _fromShadows = Array.Empty<TwShadowLayer>();
    private TwOutsetSpec? _fromRing;
    private int _fromExtent;
    private DateTime _start;
    private TwTiming _timing;
    // Geçişte belirerek çizilenler; değişmeyen tam opak kalır.
    private bool _shadowsFade, _ringFade;
    // Klavye odağıyla beliren halka dışarıdan yerine daralır.
    private DateTime _ringInStart;
    private bool _ringIn;

    /// <summary>Time (ms) a focus ring takes to close in onto the control.</summary>
    internal const int RingInDuration = 180;

    /// <summary>How far (96 DPI px) outside its place a focus ring starts.</summary>
    internal const int RingInDistance = 6;

    // Dikkat çekme: kontrolün çevresinden dışa yayılıp solan halkalar (TwAttention.Pulse).
    private DateTime _pingStart;
    private int _pingCount;
    private float _pingRadius;
    private Color _pingColor;

    /// <summary>Time (ms) of one attention pulse.</summary>
    internal const int PingDuration = 700;

    // Darbe süresi ilk darbede sabitlenir: süre ayarı darbeler sürerken değişirse halka sıçramaz.
    private int PingTime => Math.Max(1, _pingTime);

    private int _pingTime = PingDuration;

    /// <summary>How far (96 DPI px) an attention pulse spreads out.</summary>
    internal const int PingDistance = 10;

    private TwOutset(Control control)
    {
        _control = control;
        _timer.Source = "outset";
        _timer.Subject = control;
        _control.ParentChanged += (_, _) => Reattach();
        _control.LocationChanged += (_, _) => InvalidateParent();
        _control.SizeChanged += (_, _) => InvalidateParent();
        _control.VisibleChanged += (_, _) => InvalidateParent();
        // Çerçevenin zemini kontrolün o anki rengidir (geçiş animasyonları dahil).
        _control.BackColorChanged += (_, _) => { if (Spec?.Frame != null) InvalidateParent(); };
        _control.Disposed += (_, _) => _timer.Dispose();
        _timer.Tick += (_, _) =>
        {
            if (Progress >= 1f && RingInProgress >= 1f && !Pinging)
            {
                _timer.Stop();
                _ringIn = false;
                _pingCount = 0;
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

    internal bool Animating => Progress < 1f || RingInProgress < 1f || Pinging;

    /// <summary>Whether attention pulses are running.</summary>
    internal bool Pinging => _pingCount > 0 && (DateTime.UtcNow - _pingStart).TotalMilliseconds < _pingCount * PingTime;

    /// <summary>Starts <paramref name="count"/> attention pulses in <paramref name="color"/> around the control.</summary>
    public void Ping(int count, Color color, float radius)
    {
        _pingStart = DateTime.UtcNow;
        _pingTime = Math.Max(1, TwMotion.Timings.Scaled(TwMotion.Timings.AttentionPulse));
        _pingCount = Math.Max(1, count);
        _pingColor = color;
        _pingRadius = radius;
        _timer.Start();
        InvalidateParent();
    }

    private int PingExtent => _pingCount > 0 ? (int)Math.Ceiling(PingDistance * TwDpi.DeviceDpi(_control) / 96f) + 2 : 0;

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

    public void Update(TwOutsetSpec spec) => Update(spec, TwTiming.None);

    /// <param name="spec">New shadow, ring and frame.</param>
    /// <param name="timing">Transition of the shadow and ring change (<c>transition</c>); none = immediate.</param>
    public void Update(TwOutsetSpec spec, TwTiming timing)
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
            _timing = TwTiming.None;
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
        bool pinging = Pinging;
        if (!_control.Visible || spec is null && !pinging && (progress >= 1f || _fromShadows.Count == 0 && _fromRing is null)) return;

        var bounds = _control.Bounds;
        int spread = RingInSpread;
        int extent = Math.Max(Math.Max((spec?.Extent ?? 0) + spread, progress < 1f ? _fromExtent : 0), PingExtent);
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
                TwShadowRenderer.Draw(graphics, bounds, shadowRadius, _fromShadows[i], 1f - progress, underBox: true);
        if (fading && _fromRing != null) DrawRing(graphics, bounds, _fromRing, 1f - progress);
        if (pinging) DrawPing(graphics, _control.Bounds);
        if (spec is null) return;
        float shadowOpacity = fading && _shadowsFade ? progress : 1f;
        for (int i = spec.Shadows.Count - 1; i >= 0; i--)
            TwShadowRenderer.Draw(graphics, bounds, spec.Radius, spec.Shadows[i], shadowOpacity, underBox: true);

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

    // Her darbe kontrolün kenarından başlar, PingDistance kadar yayılırken solar.
    private void DrawPing(Graphics graphics, Rectangle bounds)
    {
        double elapsed = (DateTime.UtcNow - _pingStart).TotalMilliseconds % PingTime;
        float t = TwMotion.Ease(TwEasing.Out, (float)(elapsed / PingTime));
        float scale = TwDpi.DeviceDpi(_control) / 96f;
        int grow = (int)Math.Round(PingDistance * scale * t);
        int alpha = (int)Math.Round(_pingColor.A * 0.9f * (1 - t));
        if (alpha <= 0) return;
        var ring = Rectangle.Inflate(bounds, grow, grow);
        float radius = TwPainter.EffectiveRadius(_pingRadius, bounds.Size) + grow;
        TwPainter.StrokeRounded(graphics, ring, radius, Math.Max(2f, 2.5f * scale), Color.FromArgb(alpha, _pingColor));
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
        var extent = Math.Max(Math.Max((Spec?.Extent ?? 0) + ringIn, _fromExtent), PingExtent);
        var area = Rectangle.Inflate(_control.Bounds, extent, extent);
        _parent.Invalidate(Rectangle.Union(area, _paintedBounds.IsEmpty ? area : _paintedBounds));
        _paintedBounds = area;
    }

    // Paint'in çizeceği alan (kontrol + gölge/halka/darbe taşması); çizecek bir şey yoksa boş.
    private Rectangle PaintArea()
    {
        var spec = Spec;
        float progress = Progress;
        bool pinging = Pinging;
        if (!_control.Visible || spec is null && !pinging && (progress >= 1f || _fromShadows.Count == 0 && _fromRing is null)) return Rectangle.Empty;
        int extent = Math.Max(Math.Max((spec?.Extent ?? 0) + RingInSpread, progress < 1f ? _fromExtent : 0), PingExtent);
        return Rectangle.Inflate(_control.Bounds, extent, extent);
    }

    /// <summary>
    /// Paints the shadows, rings and frames of a parent's children in its Paint event. They are drawn into an offscreen
    /// premultiplied picture first and blended onto the parent once: GDI+ reads the destination back for every
    /// semi-transparent image it draws on a window's device context, which made each shadow part cost as much as a
    /// whole page of drawing on a parent that is not double-buffered.
    /// </summary>
    private sealed class ParentPainter
    {
        private readonly List<WeakReference<TwOutset>> _children = new List<WeakReference<TwOutset>>();
        private readonly List<TwOutset> _painting = new List<TwOutset>();
        private readonly List<Rectangle> _parts = new List<Rectangle>();

        public ParentPainter(Control parent) => parent.Paint += OnPaint;

        public void Add(TwOutset outset) => _children.Add(new WeakReference<TwOutset>(outset));

        public void Remove(TwOutset outset) =>
            _children.RemoveAll(r => !r.TryGetTarget(out var target) || ReferenceEquals(target, outset));

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            var clip = e.ClipRectangle;
            var area = Rectangle.Empty;
            long partsArea = 0;
            _painting.Clear();
            _parts.Clear();
            // Controls sırasının tersi: alttaki (sonra eklenen) kontrolün gölgesi önce çizilir.
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                if (!_children[i].TryGetTarget(out var outset))
                {
                    _children.RemoveAt(i);
                    continue;
                }
                var part = Rectangle.Intersect(outset.PaintArea(), clip);
                if (part.Width <= 0 || part.Height <= 0) continue;
                _painting.Add(outset);
                _parts.Add(part);
                partsArea += (long)part.Width * part.Height;
                area = area.IsEmpty ? part : Rectangle.Union(area, part);
            }
            if (_painting.Count == 0) return;
            try
            {
                // Birbirinden uzak kartlarda ortak alan çoğunlukla boştur: her biri kendi alanında harmanlanır.
                if ((long)area.Width * area.Height > partsArea * 2)
                    for (int i = 0; i < _painting.Count; i++) Blend(e.Graphics, _parts[i], _painting[i]);
                else Blend(e.Graphics, area, _painting.ToArray());
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
            {
                // Resim ayrılamadıysa (çok büyük alan, GDI+ belleği) doğrudan çizilir.
                TwErrors.Trace("shadow/ring buffer", ex);
                foreach (var outset in _painting) PaintOne(outset, e.Graphics, clip);
            }
            finally
            {
                _painting.Clear();
                _parts.Clear();
            }
        }

        // Saydam bir resme çizip hedefe tek seferde harmanlar (sırayla üst üste bindirme birleşmelidir: sonuç aynıdır).
        private static void Blend(Graphics target, Rectangle area, params TwOutset[] outsets)
        {
            using var picture = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(picture))
            {
                g.TranslateTransform(-area.X, -area.Y);
                g.SetClip(area);
                foreach (var outset in outsets) PaintOne(outset, g, area);
            }
            target.DrawImage(picture, area, new Rectangle(Point.Empty, area.Size), GraphicsUnit.Pixel);
        }

        private static void PaintOne(TwOutset outset, Graphics graphics, Rectangle clip)
        {
            // Çizim hatası ebeveynin Paint'ini (ve uygulamayı) düşürmemeli.
            try
            {
                outset.Paint(graphics, clip);
            }
            catch (Exception ex) when (!TwErrors.IsCritical(ex))
            {
                TwErrors.Trace("shadow/ring painting", ex);
            }
        }
    }
}

/// <summary>
/// Produces and caches blurred shadow bitmaps (3-pass box blur ≈ Gaussian). A shadow wider (or taller) than its corners is
/// drawn nine-slice: the blurred picture of the smallest such shape is cut into corners, edges and middle, and the edges and
/// middle are stretched. Along a straight side the blur is the same at every point, so the result is the same as blurring
/// the full size, while the cache holds one small picture per radius, blur and color instead of one per control size.
/// </summary>
internal static class TwShadowRenderer
{
    /// <summary>Default of <see cref="CacheBudgetBytes"/>.</summary>
    internal const long DefaultCacheBudgetBytes = 8L * 1024 * 1024;

    private static long _budget = DefaultCacheBudgetBytes;

    /// <summary>Memory the cached pictures may take (bytes); the least recently used ones are disposed beyond it.</summary>
    internal static long CacheBudgetBytes
    {
        get
        {
            lock (Cache) return _budget;
        }
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The shadow cache budget must be positive.");
            lock (Cache)
            {
                _budget = value;
                Trim(null);
            }
        }
    }

    private static readonly Dictionary<(int W, int H, int Radius, int Blur, int Color), LinkedListNode<Entry>> Cache =
        new Dictionary<(int W, int H, int Radius, int Blur, int Color), LinkedListNode<Entry>>();
    private static readonly LinkedList<Entry> Recent = new LinkedList<Entry>();
    private static long _bytes, _hits, _misses;

    internal static TwShadowCacheStats Stats
    {
        get
        {
            lock (Cache) return new TwShadowCacheStats(Cache.Count, _bytes, _hits, _misses);
        }
    }

    /// <summary>Empties the cache (the pictures are made again when next drawn).</summary>
    internal static void Clear()
    {
        lock (Cache)
        {
            foreach (var entry in Recent) entry.Dispose();
            Recent.Clear();
            Cache.Clear();
            _bytes = 0;
        }
    }

    /// <summary>false = draw the whole blurred picture without slicing (tests compare the sliced drawing with it).</summary>
    [ThreadStatic]
    internal static bool NoSliceForTests;

    /// <param name="graphics">Where to draw (the parent's surface).</param>
    /// <param name="bounds">The box casting the shadow.</param>
    /// <param name="radius">Corner radius of the box.</param>
    /// <param name="layer">The shadow layer.</param>
    /// <param name="opacity">Opacity of the whole layer (transitions).</param>
    /// <param name="underBox">
    /// The box itself is drawn over the shadow (a control on its parent): the parts of the sliced picture lying inside the
    /// box, away from its rounded corners, are not drawn. As in CSS, an outer shadow is not seen under its box; the middle
    /// part, stretched over the whole box, is most of the pixels to blend.
    /// </param>
    public static void Draw(Graphics graphics, Rectangle bounds, float radius, TwShadowLayer layer, float opacity = 1f, bool underBox = false)
    {
        if (layer.Color.A == 0 || opacity <= 0f) return;
        var spread = layer.Spread;
        var rect = RectangleF.Inflate(bounds, spread, spread);
        rect.Offset(0, layer.OffsetY);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        int blur = (int)Math.Ceiling(layer.Blur);
        int margin = blur + 2;
        int effectiveRadius = (int)Math.Round(Math.Max(0, TwPainter.EffectiveRadius(radius, bounds.Size) + spread));
        int width = (int)Math.Ceiling(rect.Width), height = (int)Math.Ceiling(rect.Height);
        var target = new Rectangle((int)Math.Round(rect.X) - margin, (int)Math.Round(rect.Y) - margin, width + margin * 2, height + margin * 2);
        // A shadow outside the area being painted is neither drawn nor put in the cache.
        if (!graphics.IsVisible(target)) return;

        int corner = CornerSize(effectiveRadius, blur);
        int tile = corner * 2 + 1;
        bool sliceX = !NoSliceForTests && target.Width > tile, sliceY = !NoSliceForTests && target.Height > tile;
        int pictureWidth = sliceX ? tile - margin * 2 : width, pictureHeight = sliceY ? tile - margin * 2 : height;
        var hidden = underBox ? HiddenUnder(bounds, radius) : Rectangle.Empty;
        // Picture and drawing stay under the lock: an entry evicted by another UI thread is disposed at once.
        lock (Cache)
        {
            var entry = Get(pictureWidth, pictureHeight, effectiveRadius, blur, layer.Color);
            entry.Draw(graphics, target, sliceX ? corner : -1, sliceY ? corner : -1, opacity, hidden);
        }
    }

    // The box's area away from its rounded corners: whatever is drawn there is painted over by the box.
    private static Rectangle HiddenUnder(Rectangle bounds, float radius)
    {
        int inset = (int)Math.Ceiling(TwPainter.EffectiveRadius(radius, bounds.Size));
        var hidden = Rectangle.Inflate(bounds, -inset, -inset);
        return hidden.Width > 0 && hidden.Height > 0 ? hidden : Rectangle.Empty;
    }

    /// <summary>
    /// Distance from the picture's edge within which the blurred corner differs from the straight side: the margin, the
    /// radius, the antialiased edge pixel (half covered) and the reach of the three box passes (3 × box radius).
    /// </summary>
    internal static int CornerSize(int radius, int blur) => blur + 2 + radius + 1 + 3 * BoxRadius(blur);

    private static int BoxRadius(int blur) => blur > 0 ? Math.Max(1, (int)Math.Round(blur / 2.0)) : 0;

    private static Entry Get(int width, int height, int radius, int blur, Color color)
    {
        var key = (width, height, radius, blur, color.ToArgb());
        if (Cache.TryGetValue(key, out var node))
        {
            _hits++;
            Recent.Remove(node);
            Recent.AddFirst(node);
            return node.Value;
        }
        _misses++;
        var entry = new Entry(key, Create(width, height, radius, blur, color));
        Cache[key] = Recent.AddFirst(entry);
        _bytes += entry.Bytes;
        Trim(entry);
        return entry;
    }

    // Disposes the least recently used pictures beyond the budget. The newest one (keep) stays even when it alone is over
    // the budget: it is the one about to be drawn. Called under the lock.
    private static void Trim(Entry? keep)
    {
        while (_bytes > _budget && Recent.Last is { } oldest && oldest.Value != keep)
        {
            Recent.RemoveLast();
            Cache.Remove(oldest.Value.Key);
            _bytes -= oldest.Value.Bytes;
            oldest.Value.Dispose();
        }
    }

    /// <summary>A cached blurred picture.</summary>
    private sealed class Entry : IDisposable
    {
        private readonly Bitmap _picture;

        public Entry((int, int, int, int, int) key, Bitmap picture)
        {
            Key = key;
            _picture = picture;
        }

        public (int W, int H, int Radius, int Blur, int Color) Key { get; }

        public long Bytes => (long)_picture.Width * _picture.Height * 4;

        /// <summary>Draws the picture over <paramref name="target"/>; a corner size of -1 = that axis is not sliced.</summary>
        public void Draw(Graphics graphics, Rectangle target, int cornerX, int cornerY, float opacity, Rectangle hidden)
        {
            using var attributes = opacity >= 1f ? null : new ImageAttributes();
            attributes?.SetColorMatrix(new ColorMatrix { Matrix33 = opacity });
            if (cornerX < 0 && cornerY < 0)
            {
                DrawPart(graphics, new Rectangle(Point.Empty, _picture.Size), target, attributes);
                return;
            }
            var columns = Spans(_picture.Width, target.X, target.Width, cornerX);
            var rows = Spans(_picture.Height, target.Y, target.Height, cornerY);
            // Picture pixels map one to one onto device pixels: filtering would blend in the pixels beside each part.
            var interpolation = graphics.InterpolationMode;
            var offset = graphics.PixelOffsetMode;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            try
            {
                foreach (var (srcY, dstY, dstH, srcH) in rows)
                foreach (var (srcX, dstX, dstW, srcW) in columns)
                {
                    var dest = new Rectangle(dstX, dstY, dstW, dstH);
                    if (dest.Width <= 0 || dest.Height <= 0 || hidden.Contains(dest) || !graphics.IsVisible(dest)) continue;
                    var source = new Rectangle(srcX, srcY, srcW, srcH);
                    if (source.Size == dest.Size) DrawPart(graphics, source, dest, attributes);
                    else Stretch(graphics, source, dest, attributes);
                }
            }
            finally
            {
                graphics.InterpolationMode = interpolation;
                graphics.PixelOffsetMode = offset;
            }
        }

        // (source position, target position, target length, source length): corner, stretched 1 px middle, far corner.
        private static (int Src, int Dst, int DstLength, int SrcLength)[] Spans(int pictureLength, int start, int length, int corner) =>
            corner < 0
                ? new[] { (0, start, length, pictureLength) }
                : new[] { (0, start, corner, corner), (corner, start + corner, length - corner * 2, 1), (corner + 1, start + length - corner, corner, corner) };

        private void DrawPart(Graphics graphics, Rectangle source, Rectangle dest, ImageAttributes? attributes)
        {
            if (attributes is null && source.Location == Point.Empty && source.Size == _picture.Size)
                graphics.DrawImageUnscaled(_picture, dest.X, dest.Y);
            else if (attributes is null)
                graphics.DrawImage(_picture, dest, source, GraphicsUnit.Pixel);
            else
                graphics.DrawImage(_picture, dest, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        }

        // A stretched part is a one-pixel row or column scaled with nearest-neighbor sampling (set by Draw): every
        // target pixel takes that row or column exactly, blended the same way as the corners.
        private void Stretch(Graphics graphics, Rectangle source, Rectangle dest, ImageAttributes? attributes) =>
            DrawPart(graphics, source, dest, attributes);

        public void Dispose()
        {
            _picture.Dispose();
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
            int boxRadius = BoxRadius(blur);
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
        // GDI+ blends premultiplied pixels: converted once here, not on every draw (where whole and partial draws would
        // round the conversion differently).
        using (bitmap) return bitmap.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format32bppPArgb);
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
