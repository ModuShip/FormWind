using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Opening and data-change animation shared by the chart controls. The opening plays once, the first time the chart is
/// shown with data: each item (bar, ring, cell, dot) starts <see cref="Stagger"/> ms after the previous one. After that a
/// data change moves what is on screen to the new data in <see cref="ChangeDuration"/> ms. The chart keeps its geometry
/// calculations on the target; only painting reads <see cref="IntroProgress"/> and <see cref="ChangeProgress"/>.
/// </summary>
internal sealed class TwChartMotion : IDisposable
{
    /// <summary>Time (ms) of one item's opening animation.</summary>
    internal const int IntroDuration = 600;

    /// <summary>Time (ms) of the move from the old data to the new.</summary>
    internal const int ChangeDuration = 350;

    /// <summary>Delay (ms) between the openings of neighbouring items.</summary>
    internal const int Stagger = 35;

    /// <summary>The delays together stay within this (ms), so charts with many items do not open slowly.</summary>
    internal const int MaxSpread = 400;

    private readonly Control _owner;
    private TwTween? _tween;
    private int _duration = 1, _stagger, _total = 1;
    private TwEasing _easing;

    public TwChartMotion(Control owner) => _owner = owner;

    /// <summary>Whether the chart has been shown with data (its opening played or was skipped); data changes then animate.</summary>
    public bool Shown { get; private set; }

    public bool Running => _tween is { Running: true };

    public bool IntroRunning => Running && IsIntro;

    public bool ChangeRunning => Running && !IsIntro;

    private bool IsIntro { get; set; }

    /// <summary>Opening progress of item <paramref name="index"/> (0 before it starts, 1 when no opening runs).</summary>
    public float IntroProgress(int index = 0) => IntroRunning ? Progress(index) : 1f;

    /// <summary>Progress of the move to the new data (1 when none runs).</summary>
    public float ChangeProgress => ChangeRunning ? Progress(0) : 1f;

    /// <summary>Share of the whole opening that has passed, eased (for parts drawn across all items, e.g. a joining line).</summary>
    public float IntroOverall => IntroRunning ? TwMotion.Ease(TwEasing.Out, _tween!.Progress) : 1f;

    private float Progress(int index)
    {
        double elapsed = _tween!.Progress * _total;
        return TwMotion.Ease(_easing, (float)((elapsed - index * _stagger) / _duration));
    }

    /// <summary>Plays the opening for <paramref name="count"/> items now (nothing happens when the chart cannot animate).</summary>
    public void PlayIntro(int count, TwEasing easing = TwEasing.Out)
    {
        Shown = true;
        IsIntro = true;
        Start(IntroDuration, count, easing);
    }

    /// <summary>Plays the opening the first time the chart can show its data.</summary>
    public void TryIntro(bool animateOnShow, bool hasData, int count, TwEasing easing = TwEasing.Out)
    {
        if (Shown || !animateOnShow || !hasData || !TwMotion.CanAnimate(_owner)) return;
        PlayIntro(count, easing);
    }

    /// <summary>
    /// After the data changed: before the chart was shown, the opening may play; afterwards, when
    /// <paramref name="animateChanges"/> and the chart can animate, <paramref name="captureFrom"/> keeps what is on screen
    /// and the move to the new data starts. Otherwise the new data is drawn at once.
    /// </summary>
    public void DataChanged(bool animateOnShow, bool animateChanges, bool hasData, int count, Action captureFrom, TwEasing introEasing = TwEasing.Out)
    {
        if (!Shown)
        {
            TryIntro(animateOnShow, hasData, count, introEasing);
            return;
        }
        if (!animateChanges || !TwMotion.CanAnimate(_owner))
        {
            Stop();
            return;
        }
        captureFrom();
        IsIntro = false;
        Start(ChangeDuration, 1, TwEasing.Out);
    }

    /// <summary>Called from painting: a chart drawn with data counts as shown (its later changes animate).</summary>
    public void MarkPainted(bool hasData)
    {
        if (hasData && _owner.IsHandleCreated && _owner.Visible) Shown = true;
    }

    private void Start(int durationMs, int count, TwEasing easing)
    {
        _tween ??= new TwTween(_owner);
        _duration = durationMs;
        _stagger = count > 1 ? Math.Min(Stagger, MaxSpread / (count - 1)) : 0;
        _total = durationMs + _stagger * Math.Max(0, count - 1);
        _easing = easing;
        _tween.Start(_total, TwEasing.Linear);
    }

    public void Stop() => _tween?.Stop();

    public void Dispose() => _tween?.Dispose();

    // ---- Ara değerler ----

    public static float Lerp(float from, float to, float t) => from + (to - from) * t;

    public static PointF Lerp(PointF from, PointF to, float t) => new PointF(Lerp(from.X, to.X, t), Lerp(from.Y, to.Y, t));

    public static RectangleF Lerp(RectangleF from, RectangleF to, float t) => TwMotion.Lerp(from, to, t);
}
