using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// <see cref="TwMotionOptions.Completed"/>: called once when a transition or window motion has finished, also when nothing
/// is animated; and <see cref="TwMotion.Timings.Scale"/>, the global multiplier of every duration.
/// </summary>
[Collection(UiCollection.Name)]
public class MotionCompletedTests
{
    private static void WaitUntil(Func<bool> condition, int milliseconds = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private static Form ShowForm()
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(120, 120), ClientSize = new Size(400, 300) };
        form.Show();
        form.Activate();
        Application.DoEvents();
        return form;
    }

    private static void WithoutMotion(Action action)
    {
        var previous = TwMotion.Preference;
        TwMotion.Enabled = false;
        try { action(); }
        finally { TwMotion.Preference = previous; TwMotion.Enabled = true; }
    }

    [Fact]
    public void Cross_fade_completes_once_after_the_fade() => Sta.Run(() =>
    {
        using var form = ShowForm();
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        form.Controls.Add(page);
        Application.DoEvents();
        int before = TwFadeOut.ActiveCount, completed = 0, activeAtCompletion = -1;
        bool changed = false;

        TwTransition.CrossFade(page, () => { page.BackColor = Color.Black; changed = true; },
            new TwMotionOptions { DurationMs = 100, Completed = () => { completed++; activeAtCompletion = TwFadeOut.ActiveCount; Assert.True(changed); } });
        Assert.True(TwTransition.LastCovered);
        Assert.Equal(0, completed);                                                // solma sürerken çağrılmaz
        WaitUntil(() => completed > 0);
        Assert.Equal(1, completed);
        Assert.Equal(before, activeAtCompletion);                                  // son resim kapanmış
        Application.DoEvents();
        Thread.Sleep(50);
        Application.DoEvents();
        Assert.Equal(1, completed);                                                // bir kez
        form.Close();
    });

    [Fact]
    public void Cross_fade_completes_at_once_when_nothing_is_animated() => Sta.Run(() =>
    {
        using var form = ShowForm();
        int completed = 0, changes = 0;
        var options = new TwMotionOptions { Completed = () => { completed++; Assert.Equal(1, changes); } };

        WithoutMotion(() =>
        {
            TwTransition.CrossFade(form, () => changes++, options);
            Assert.Equal(1, completed);                                            // değişiklikten sonra, dönmeden önce
        });

        TwTransition.CrossFade(form, () => changes++, new TwMotionOptions { DurationMs = 0, Completed = () => completed++ });
        Assert.Equal(2, changes);
        Assert.Equal(2, completed);                                                // süre 0

        using var hidden = new Panel();
        TwTransition.CrossFade(hidden, () => { }, new TwMotionOptions { Completed = () => completed++ });
        Assert.Equal(3, completed);                                                // görünmeyen ebeveyn
        form.Close();
    });

    [Fact]
    public void Completed_is_not_called_when_the_change_throws() => Sta.Run(() =>
    {
        using var form = ShowForm();
        int completed = 0;
        Assert.Throws<InvalidOperationException>(() => TwTransition.CrossFade(form, () => throw new InvalidOperationException(),
            new TwMotionOptions { DurationMs = 50, Completed = () => completed++ }));
        WaitUntil(() => TwFadeOut.ActiveCount == 0);
        Application.DoEvents();
        Assert.Equal(0, completed);
        form.Close();
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_panel_slide_completes_once(bool show) => Sta.Run(() =>
    {
        using var form = ShowForm();
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var inspector = new Panel { Dock = DockStyle.Right, Width = 150, BackColor = Color.SteelBlue, Visible = !show };
        form.Controls.Add(content);
        form.Controls.Add(inspector);
        Application.DoEvents();
        int completed = 0;
        var options = new TwMotionOptions { DurationMs = 100, Completed = () => { completed++; Assert.Equal(show, inspector.Visible); } };

        if (show) TwTransition.SlideIn(inspector, TwEdge.Right, options);
        else TwTransition.SlideOut(inspector, TwEdge.Right, options);
        Assert.True(TwTransition.LastCovered);
        Assert.Equal(0, completed);
        WaitUntil(() => completed > 0);
        Assert.Equal(1, completed);
        WaitUntil(() => false, 150);
        Assert.Equal(1, completed);

        // Zaten istenen durumdaki panel: hareket yok, hemen tamam.
        if (show) TwTransition.SlideIn(inspector, TwEdge.Right, options);
        else TwTransition.SlideOut(inspector, TwEdge.Right, options);
        Assert.Equal(2, completed);
        form.Close();
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_panel_slide_without_fade_of_the_rest_completes_once(bool fadeRest) => Sta.Run(() =>
    {
        using var form = ShowForm();
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var inspector = new Panel { Dock = DockStyle.Right, Width = 150, BackColor = Color.SteelBlue, Visible = false };
        form.Controls.Add(content);
        form.Controls.Add(inspector);
        Application.DoEvents();
        int completed = 0;
        TwTransition.SlideIn(inspector, TwEdge.Right, new TwMotionOptions { DurationMs = 100, FadeRest = fadeRest, Completed = () => completed++ });
        WaitUntil(() => completed > 0);
        WaitUntil(() => false, 150);
        Assert.Equal(1, completed);
        form.Close();
    });

    [Fact]
    public void Resize_completes_once_animated_unchanged_and_without_motion() => Sta.Run(() =>
    {
        using var form = ShowForm();
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = Color.SteelBlue };
        form.Controls.Add(content);
        form.Controls.Add(sidebar);
        Application.DoEvents();
        int completed = 0;
        var options = new TwMotionOptions { DurationMs = 100, Completed = () => completed++ };

        TwTransition.Resize(sidebar, 56, options);
        Assert.True(TwTransition.LastCovered);
        Assert.Equal(0, completed);
        WaitUntil(() => completed > 0);
        WaitUntil(() => false, 150);
        Assert.Equal(1, completed);

        TwTransition.Resize(sidebar, 56, options);                                 // aynı boy: yapılacak bir şey yok
        Assert.Equal(2, completed);

        // Boyu değiştirmeyen değişiklik yalnızca çapraz solar; yine bir kez.
        TwTransition.Resize(sidebar, () => content.BackColor = Color.Gray, options);
        WaitUntil(() => completed > 2);
        WaitUntil(() => false, 150);
        Assert.Equal(3, completed);

        WithoutMotion(() =>
        {
            TwTransition.Resize(sidebar, 120, options);
            Assert.Equal(120, sidebar.Width);
            Assert.Equal(4, completed);
        });
        form.Close();
    });

    [Fact]
    public void Window_motion_completes_once() => Sta.Run(() =>
    {
        using var owner = ShowForm();
        using var sheet = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(300, 200), Size = new Size(200, 150) };
        sheet.Show(owner);
        Application.DoEvents();
        int entered = 0, exited = 0;

        TwWindowMotion.SlideIn(sheet, TwEdge.Right, new TwMotionOptions { DurationMs = 80, Completed = () => entered++ });
        Assert.Equal(0, entered);
        WaitUntil(() => entered > 0);
        WaitUntil(() => false, 100);
        Assert.Equal(1, entered);
        Assert.Equal(new Point(300, 200), sheet.Location);

        TwWindowMotion.FadeOut(sheet, TwEdge.Right, new TwMotionOptions { DurationMs = 80, Completed = () => exited++ });
        WaitUntil(() => exited > 0);
        WaitUntil(() => false, 100);
        Assert.Equal(1, exited);

        WithoutMotion(() =>
        {
            TwWindowMotion.SlideIn(sheet, TwEdge.Right, new TwMotionOptions { Completed = () => entered++ });
            TwWindowMotion.FadeOut(sheet, TwEdge.Right, new TwMotionOptions { Completed = () => exited++ });
            Assert.Equal(2, entered);
            Assert.Equal(2, exited);
        });
        sheet.Close();
        owner.Close();
    });

    [Fact]
    public void Scale_validates_and_resets() => Sta.Run(() =>
    {
        try
        {
            Assert.Equal(1.0, TwMotion.Timings.Scale);
            TwMotion.Timings.Scale = 5;
            Assert.Equal(5.0, TwMotion.Timings.Scale);
            Assert.Throws<ArgumentOutOfRangeException>(() => TwMotion.Timings.Scale = -0.1);
            Assert.Throws<ArgumentOutOfRangeException>(() => TwMotion.Timings.Scale = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => TwMotion.Timings.Scale = double.PositiveInfinity);
            Assert.Equal(5.0, TwMotion.Timings.Scale);
            TwMotion.Timings.Reset();
            Assert.Equal(1.0, TwMotion.Timings.Scale);
        }
        finally { TwMotion.Timings.Reset(); }
    });

    [Fact]
    public void Scale_multiplies_every_timing() => Sta.Run(() =>
    {
        try
        {
            TwMotion.Timings.Scale = 5;
            var timing = new TwTiming(250, 20, TwEasing.Linear);
            Assert.Equal(1250, timing.DurationMs);
            Assert.Equal(100, timing.DelayMs);
            Assert.Equal(1350, timing.TotalMs);
            Assert.Equal(1250, new TwTiming(250, 0, new TwCubicBezier(0.2, 0, 0, 1)).DurationMs);
            Assert.Equal(250, TwTiming.Exact(250, 0, TwEasing.Out).DurationMs);    // önceden ölçeklenmiş süre bir daha çarpılmaz

            // TwMotionOptions.Timing ve Timings değerleri aynı yerden geçer.
            Assert.Equal(2000, new TwMotionOptions { DurationMs = 400 }.Timing(100, TwEasing.Out).DurationMs);
            Assert.Equal(TwMotion.Timings.CrossFade * 5, TwMotionOptions.Timing(null, TwMotion.Timings.CrossFade, TwEasing.Out).DurationMs);

            // duration-[120ms] sınıfının geçişi.
            var style = new TwTheme().Parse("transition duration-[120ms] delay-[10ms]").Resolve(TwState.None);
            Assert.Equal(120, style.DurationMs);                                   // sınıfın değeri değişmez; çalıştırılırken ölçeklenir
            Assert.Equal(600, new TwTiming(style.DurationMs!.Value, style.DelayMs!.Value, TwEasing.Out).DurationMs);

            TwMotion.Timings.Scale = 0.1;
            Assert.Equal(25, new TwTiming(250).DurationMs);
            Assert.Equal(1, new TwTiming(1).DurationMs);                           // 0'dan büyük süre 1 ms'nin altına inmez
            Assert.Equal(0, new TwTiming(0).DurationMs);

            TwMotion.Timings.Scale = 0;
            var immediate = new TwTiming(250, 100);
            Assert.Equal(0, immediate.TotalMs);
            Assert.False(immediate.Animates(TwTransitionProperty.Colors));
        }
        finally { TwMotion.Timings.Reset(); }
    });

    [Fact]
    public void Scale_slows_a_transition_down_and_zero_makes_it_immediate() => Sta.Run(() =>
    {
        try
        {
            using var form = ShowForm();
            var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            form.Controls.Add(page);
            Application.DoEvents();
            int before = TwFadeOut.ActiveCount;

            TwMotion.Timings.Scale = 30;                                           // 100 ms -> 3 sn
            TwTransition.CrossFade(page, () => page.BackColor = Color.Black, 100);
            WaitUntil(() => false, 400);
            Assert.Equal(before + 1, TwFadeOut.ActiveCount);                       // 100 ms'de bitecekken hâlâ sürüyor
            TwMotion.Timings.Scale = 1;
            WaitUntil(() => TwFadeOut.ActiveCount == before, 6000);
            Assert.Equal(before, TwFadeOut.ActiveCount);

            TwMotion.Timings.Scale = 0;
            int completed = 0;
            TwTransition.CrossFade(page, () => page.BackColor = Color.White, new TwMotionOptions { Completed = () => completed++ });
            Assert.False(TwTransition.LastCovered);                                // hiç animasyon yok
            Assert.Equal(1, completed);
            var tween = new TwTween(page);
            tween.Start(500);
            Assert.False(tween.Running);
            Assert.Equal(1f, tween.Progress);
            form.Close();
        }
        finally { TwMotion.Timings.Reset(); }
    });
}
