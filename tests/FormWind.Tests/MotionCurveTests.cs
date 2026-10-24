using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>TwEasing.Spring, TwCubicBezier, TwMotion.Durations and TwAnimator's number animations.</summary>
[Collection(UiCollection.Name)]
public class MotionCurveTests
{
    private static Form Host() =>
        new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, Size = new Size(400, 300) };

    // Animasyonu kapatan/açan testler değeri geri alır (bkz. TestMotion).
    private static void WithMotion(bool enabled, Action body)
    {
        var previous = TwMotion.Enabled;
        TwMotion.Enabled = enabled;
        try
        {
            body();
        }
        finally
        {
            TwMotion.Enabled = previous;
        }
    }

    [Fact]
    public void Spring_overshoots_then_settles_on_the_target()
    {
        Assert.Equal(0f, TwMotion.Ease(TwEasing.Spring, 0f), 4);
        Assert.Equal(1f, TwMotion.Ease(TwEasing.Spring, 1f));
        Assert.Equal(1f, TwMotion.Ease(TwEasing.Spring, 2f));

        float peak = 0f;
        for (int i = 0; i <= 100; i++) peak = Math.Max(peak, TwMotion.Ease(TwEasing.Spring, i / 100f));
        Assert.InRange(peak, 1.05f, 1.12f);                                  // ~%10 taşma
        Assert.InRange(TwMotion.Ease(TwEasing.Spring, 0.9f), 0.99f, 1.01f);  // sonda oturmuş
    }

    [Theory]
    [InlineData(0.0, 0.0, 1.0, 1.0, 0.3f, 0.3f)]               // doğrusal
    [InlineData(0.25, 0.1, 0.25, 1.0, 0.5f, 0.8024f)]          // CSS ease
    [InlineData(0.42, 0.0, 1.0, 1.0, 0.5f, 0.3153f)]           // CSS ease-in
    [InlineData(0.0, 0.0, 0.58, 1.0, 0.5f, 0.6847f)]           // CSS ease-out
    public void Cubic_bezier_matches_css(double x1, double y1, double x2, double y2, float t, float expected)
    {
        var curve = new TwCubicBezier(x1, y1, x2, y2);
        Assert.Equal(expected, curve.Ease(t), 3);
        Assert.Equal(0f, curve.Ease(-1f));
        Assert.Equal(1f, curve.Ease(2f));
    }

    [Fact]
    public void Cubic_bezier_allows_overshoot_but_not_time_outside_0_to_1()
    {
        var back = new TwCubicBezier(0.34, 1.56, 0.64, 1);
        Assert.True(back.Ease(0.5f) > 1f);
        Assert.Equal(new TwCubicBezier(0.34, 1.56, 0.64, 1), back);
        Assert.Equal("cubic-bezier(0.34, 1.56, 0.64, 1)", back.ToString());

        Assert.Throws<ArgumentOutOfRangeException>(() => new TwCubicBezier(-0.1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TwCubicBezier(0, 0, 1.5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TwCubicBezier(0, double.NaN, 1, 1));
    }

    [Fact]
    public void Transition_uses_the_custom_curve_after_the_delay()
    {
        var timing = new TwTransition(100, 50, new TwCubicBezier(0.25, 0.1, 0.25, 1));
        Assert.Equal(0f, timing.Progress(30));
        Assert.Equal(0.8024f, timing.Progress(100), 3);
        Assert.Equal(1f, timing.Progress(150));
    }

    [Fact]
    public void Blend_clamps_channels_when_progress_overshoots()
    {
        var color = TwMotion.Blend(Color.FromArgb(255, 100, 100, 100), Color.FromArgb(255, 250, 0, 250), 1.2f);
        Assert.Equal(Color.FromArgb(255, 255, 0, 255).ToArgb(), color.ToArgb());
    }

    [Fact]
    public void Durations_drop_to_1ms_when_animations_are_off()
    {
        WithMotion(true, () =>
        {
            Assert.Equal(120, TwMotion.Durations.Xs);
            Assert.Equal(180, TwMotion.Durations.S);
            Assert.Equal(250, TwMotion.Durations.M);
            Assert.Equal(450, TwMotion.Durations.L);
        });
        WithMotion(false, () =>
        {
            Assert.Equal(1, TwMotion.Durations.Xs);
            Assert.Equal(1, TwMotion.Durations.L);
        });
    }

    [Fact]
    public void Animates_a_number_with_delay_curve_and_completion() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var panel = new Panel();
        form.Controls.Add(panel);
        form.Show();
        try
        {
            var values = new List<double>();
            int completed = 0;
            var start = DateTime.UtcNow;
            TwAnimator.Animate(panel, "width", 100, 300, 100, values.Add, TwEasing.Linear, 50, () => completed++);

            Assert.True(TwAnimator.IsAnimating(panel, "width"));
            TwAnimator.Step(start.AddMilliseconds(30));
            Assert.Equal(100, values[values.Count - 1]);                       // gecikme sürüyor
            TwAnimator.Step(start.AddMilliseconds(100));
            Assert.InRange(values[values.Count - 1], 190, 210);                 // doğrusal: yarı yolda
            Assert.Equal(0, completed);
            TwAnimator.Step(start.AddMilliseconds(400));
            Assert.Equal(300, values[values.Count - 1]);
            Assert.Equal(1, completed);
            Assert.False(TwAnimator.IsAnimating(panel, "width"));
        }
        finally
        {
            form.Close();
        }
    }));

    [Fact]
    public void New_number_animation_replaces_the_running_one_and_cancel_stops_it() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var panel = new Panel();
        form.Controls.Add(panel);
        form.Show();
        try
        {
            double last = 0;
            int firstCompleted = 0;
            var start = DateTime.UtcNow;
            TwAnimator.Animate(panel, "x", 0, 100, 100, v => last = v, TwEasing.Linear, 0, () => firstCompleted++);
            TwAnimator.Step(start.AddMilliseconds(50));
            TwAnimator.Animate(panel, "x", last, -100, 100, v => last = v, TwEasing.Linear);
            TwAnimator.Animate(panel, "y", 0, 10, 100, _ => { });

            TwAnimator.Step(start.AddMilliseconds(1000));
            Assert.Equal(-100, last);
            Assert.Equal(0, firstCompleted);                                    // yerine yenisi geçti

            TwAnimator.Animate(panel, "y", 0, 10, 100, _ => { });
            TwAnimator.Cancel(panel, "y");
            Assert.False(TwAnimator.IsAnimating(panel, "y"));
            Assert.False(TwAnimator.IsAnimating(panel, "x"));
        }
        finally
        {
            form.Close();
        }
    }));

    [Fact]
    public void Number_is_set_at_once_when_the_control_cannot_animate() => Sta.Run(() =>
    {
        WithMotion(false, () =>
        {
            using var form = Host();
            var panel = new Panel();
            form.Controls.Add(panel);
            form.Show();
            try
            {
                double value = 0;
                int completed = 0;
                TwAnimator.Animate(panel, "w", 0, 50, 300, v => value = v, TwEasing.Spring, 0, () => completed++);
                Assert.Equal(50, value);
                Assert.Equal(1, completed);
                Assert.False(TwAnimator.IsAnimating(panel, "w"));
            }
            finally
            {
                form.Close();
            }
        });

        // Görünmeyen kontrol de hemen bitişe atanır.
        double hidden = 0;
        using var orphan = new Panel();
        TwAnimator.Animate(orphan, "w", 0, 50, 300, v => hidden = v, new TwCubicBezier(0.34, 1.56, 0.64, 1));
        Assert.Equal(50, hidden);
    });

    [Fact]
    public void Tween_runs_with_a_custom_curve() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var panel = new Panel();
        form.Controls.Add(panel);
        form.Show();
        try
        {
            using var tween = new TwTween(panel);
            tween.Start(10_000, new TwCubicBezier(0.25, 0.1, 0.25, 1), 5_000);
            Assert.True(tween.Running);
            Assert.Equal(0f, tween.Progress);                                   // gecikme sürüyor
            tween.Stop();
            Assert.Equal(1f, tween.Progress);
        }
        finally
        {
            form.Close();
        }
    }));
}
