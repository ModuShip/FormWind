using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

// Animasyon: ilk görünüşte çubuklar tabandan sırayla büyür, çizgiler soldan sağa açılır; sonra veri değişince ekranda
// görünen çubuklar ve noktalar yeni yerlerine kayar. Geometri (BarBounds, ComputeLayout) hep hedefi verir.
public partial class TwChart
{
    private TwChartMotion? _motion;
    private Dictionary<(int Series, int Category), RectangleF> _shownBars = new Dictionary<(int, int), RectangleF>();
    private Dictionary<(int Series, int Category), PointF> _shownPoints = new Dictionary<(int, int), PointF>();
    private Dictionary<(int Series, int Category), RectangleF> _fromBars = new Dictionary<(int, int), RectangleF>();
    private Dictionary<(int Series, int Category), PointF> _fromPoints = new Dictionary<(int, int), PointF>();

    private TwChartMotion Motion => _motion ??= new TwChartMotion(this);

    /// <summary>
    /// The first time the chart is shown with data, bars grow from the baseline one after another (35 ms apart) and lines
    /// open from left to right (600 ms each). Not when animations are off. <see cref="AnimateIn"/> plays it again.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateOnShow { get; set; } = true;

    /// <summary>
    /// When the data changes after the chart was shown, bars and points move from where they are to their new place
    /// (350 ms) instead of being redrawn at once. Not when animations are off.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateChanges { get; set; } = true;

    /// <summary>Plays the opening animation now (e.g. after loading new data); nothing happens when animations are off.</summary>
    public void AnimateIn()
    {
        Motion.PlayIntro(CategoryCount);
        Invalidate();
    }

    /// <summary>Share (0–1) of the opening animation that has passed; 1 when none runs.</summary>
    internal float IntroProgress => Motion.IntroOverall;

    /// <summary>Progress of the move to new data; 1 when none runs.</summary>
    internal float ChangeProgress => Motion.ChangeProgress;

    /// <summary>A bar as the last paint drew it (empty when not drawn).</summary>
    internal RectangleF PaintedBarBounds(int series, int category) => _shownBars.TryGetValue((series, category), out var bar) ? bar : RectangleF.Empty;

    /// <summary>A line point as the last paint drew it.</summary>
    internal PointF? PaintedLinePoint(int series, int category) => _shownPoints.TryGetValue((series, category), out var point) ? point : null;

    // Görünmeden önce (Load, tasarımcı) veri atanmışsa ilk görünüşte; veri sonradan gelirse ilk veriyle oynatılır.
    private void AnimateDataChange() =>
        Motion.DataChanged(AnimateOnShow, AnimateChanges, CategoryCount > 0, CategoryCount, () =>
        {
            // Ekranda en son çizilen hâl başlangıç olur (art arda gelen değişikliklerde de, çizim olmadığı için aynı kalır).
            _fromBars = new Dictionary<(int, int), RectangleF>(_shownBars);
            _fromPoints = new Dictionary<(int, int), PointF>(_shownPoints);
        });

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        Motion.TryIntro(AnimateOnShow, CategoryCount > 0, CategoryCount);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        // Eski kutular başka bir boyuta aittir; kayma yerine yeni hâl hemen çizilir.
        if (Motion.ChangeRunning) Motion.Stop();
    }

    /// <summary>Starts a paint: what this paint draws becomes the start of the next data change.</summary>
    private void BeginShownFrame()
    {
        _shownBars.Clear();
        _shownPoints.Clear();
        Motion.MarkPainted(CategoryCount > 0);
    }

    /// <summary>The bar as painted now: grown from <paramref name="zero"/> while opening, moving while the data changes.</summary>
    private RectangleF ShownBar(RectangleF target, int series, int category, float zero, bool horizontal)
    {
        if (target.IsEmpty) return target;
        var shown = target;
        if (Motion.IntroRunning)
        {
            float p = Motion.IntroProgress(category);
            shown = horizontal
                ? RectangleF.FromLTRB(Grow(target.Left, zero, p), target.Top, Grow(target.Right, zero, p), target.Bottom)
                : RectangleF.FromLTRB(target.Left, Grow(target.Top, zero, p), target.Right, Grow(target.Bottom, zero, p));
        }
        else if (Motion.ChangeRunning)
        {
            // Yeni çubuk tabandan çıkar.
            var from = _fromBars.TryGetValue((series, category), out var old) ? old
                : horizontal ? new RectangleF(zero, target.Y, 0, target.Height) : new RectangleF(target.X, zero, target.Width, 0);
            shown = TwChartMotion.Lerp(from, target, Motion.ChangeProgress);
        }
        _shownBars[(series, category)] = shown;
        return shown;
    }

    private static float Grow(float edge, float zero, float p) => zero + (edge - zero) * p;

    /// <summary>The bar of a series at a category as painted in this frame (its target when not painted).</summary>
    private RectangleF PaintedBar(RectangleF target, int series, int category) =>
        _shownBars.TryGetValue((series, category), out var shown) ? shown : target;

    /// <summary>A line point as painted now: moving from its old place while the data changes; new points appear in place.</summary>
    private PointF ShownPoint(PointF target, int series, int category)
    {
        var shown = Motion.ChangeRunning && _fromPoints.TryGetValue((series, category), out var from)
            ? TwChartMotion.Lerp(from, target, Motion.ChangeProgress)
            : target;
        _shownPoints[(series, category)] = shown;
        return shown;
    }

    /// <summary>Limits drawing to the opened part of <paramref name="area"/> while the opening animation runs.</summary>
    private void ClipIntro(Graphics g, RectangleF area)
    {
        float progress = IntroProgress;
        if (progress >= 1f) return;
        bool mirror = RightToLeft == RightToLeft.Yes;
        float width = area.Width * progress;
        g.IntersectClip(new RectangleF(mirror ? area.Right - width : area.Left, area.Top, width, area.Height));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _motion?.Dispose();
        base.Dispose(disposing);
    }
}
