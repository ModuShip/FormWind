using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

// Giriş animasyonu: grafik ilk göründüğünde (verisi varsa) bir kez, soldan sağa açılarak çizilir.
public partial class TwChart
{
    private TwTween? _intro;
    private bool _introPlayed;

    /// <summary>Time (ms) of the opening animation.</summary>
    internal const int IntroDuration = 600;

    /// <summary>
    /// Draws the series opening from left to right (horizontal bars grow) the first time the chart is shown with data
    /// (600 ms); later data changes are drawn at once. Not when animations are off. <see cref="AnimateIn"/> plays it again.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateOnShow { get; set; } = true;

    /// <summary>Plays the opening animation now (e.g. after loading new data); nothing happens when animations are off.</summary>
    public void AnimateIn()
    {
        _introPlayed = true;
        _intro ??= new TwTween(this);
        _intro.Start(IntroDuration, TwEasing.Out);
        Invalidate();
    }

    /// <summary>Share (0–1) of the series drawn now; 1 when no opening animation runs.</summary>
    internal float IntroProgress => _intro is { Running: true } ? _intro.Progress : 1f;

    // Görünmeden önce (Load, tasarımcı) veri atanmışsa ilk görünüşte; veri sonradan gelirse ilk veriyle oynatılır.
    private void TryPlayIntro()
    {
        if (_introPlayed || !AnimateOnShow || CategoryCount == 0 || !TwMotion.CanAnimate(this)) return;
        AnimateIn();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        TryPlayIntro();
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
        if (disposing) _intro?.Dispose();
        base.Dispose(disposing);
    }
}
