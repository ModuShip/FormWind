using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Per-call settings of transitions and window motion (<see cref="TwMotionOptions"/>), the shared durations of them in
/// <see cref="TwMotion.Timings"/>, windows that are shown again or not shown yet, docked panel resizing and the helpers
/// around them (prewarming, double buffering).
/// </summary>
[Collection(UiCollection.Name)]
public class MotionOptionsTests
{
    private static void WaitUntil(Func<bool> condition, int milliseconds = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private static Form ShowForm(Size clientSize)
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(120, 120), ClientSize = clientSize };
        form.Show();
        form.Activate();
        Application.DoEvents();
        return form;
    }

    private static Form Sheet() =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(300, 200), Size = new Size(200, 150) };

    private static void WithMotion(Action action)
    {
        var previous = TwMotion.Preference;
        TwMotion.Enabled = true;
        try { action(); }
        finally { TwMotion.Preference = previous; }
    }

    [Fact]
    public void Options_pick_the_duration_and_curve() => Sta.Run(() =>
    {
        var spring = new TwMotionOptions { Easing = TwEasing.Spring, DurationMs = 400 }.Timing(100, TwEasing.Out);
        Assert.Equal(400, spring.DurationMs);
        Assert.Equal(TwEasing.Spring, spring.Easing);
        var curve = new TwCubicBezier(0.2, 0, 0, 1);
        var custom = new TwMotionOptions { Easing = TwEasing.In, Curve = curve }.Timing(100, TwEasing.Out);
        Assert.Equal(curve, custom.Curve);                                         // eğri, Easing'den önce gelir
        Assert.Equal(100, custom.DurationMs);
        var defaults = TwMotionOptions.Timing(null, 180, TwEasing.In);
        Assert.Equal(180, defaults.DurationMs);
        Assert.Equal(TwEasing.In, defaults.Easing);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TwMotionOptions { DurationMs = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TwMotionOptions { Distance = -1 });
        Assert.True(new TwMotionOptions().FadeRest);
    });

    [Fact]
    public void Shared_transition_timings_can_be_set_and_reset() => Sta.Run(() =>
    {
        try
        {
            Assert.Equal(250, TwMotion.Timings.CrossFade);
            Assert.Equal(250, TwMotion.Timings.Slide);
            Assert.Equal(250, TwMotion.Timings.WindowIn);
            Assert.Equal(180, TwMotion.Timings.WindowOut);
            TwMotion.Timings.WindowIn = 320;
            Assert.Equal(320, TwMotion.Timings.WindowIn);
            Assert.Throws<ArgumentOutOfRangeException>(() => TwMotion.Timings.Slide = -1);
        }
        finally
        {
            TwMotion.Timings.Reset();
        }
        Assert.Equal(250, TwMotion.Timings.WindowIn);
    });

    [Fact]
    public void A_window_slides_the_distance_that_is_set() => Sta.Run(() => WithMotion(() =>
    {
        using var owner = ShowForm(new Size(400, 300));
        try
        {
            TwWindowMotion.Distance = 12;
            Assert.Throws<ArgumentOutOfRangeException>(() => TwWindowMotion.Distance = -1);
            using var sheet = Sheet();
            sheet.Show(owner);
            Application.DoEvents();
            float dpi = TwDpi.DeviceDpi(sheet) / 96f;
            // Yalnız bu çağrı için 40 px; uzun süre, ilk karelerde neredeyse yerinden oynamaz.
            TwWindowMotion.SlideIn(sheet, TwEdge.Right, new TwMotionOptions { DurationMs = 5000, Distance = 40, Easing = TwEasing.Linear });
            Assert.InRange(sheet.Left - 300, (int)Math.Round(40 * dpi) - 2, (int)Math.Round(40 * dpi));
            TwAnimator.Step(DateTime.UtcNow.AddSeconds(10));
            Assert.Equal(new Point(300, 200), sheet.Location);

            TwWindowMotion.SlideIn(sheet, TwEdge.Bottom, new TwMotionOptions { DurationMs = 5000, Easing = TwEasing.Linear });
            Assert.InRange(sheet.Top - 200, (int)Math.Round(12 * dpi) - 1, (int)Math.Round(12 * dpi));
            TwAnimator.Step(DateTime.UtcNow.AddSeconds(10));
            sheet.Close();
        }
        finally
        {
            TwWindowMotion.Distance = TwWindowMotion.DefaultDistance;
            owner.Close();
        }
    }));

    [Fact]
    public void An_attached_window_slides_in_every_time_it_is_shown() => Sta.Run(() => WithMotion(() =>
    {
        using var owner = ShowForm(new Size(400, 300));
        using var sheet = Sheet();
        TwWindowMotion.Attach(sheet, TwEdge.Right, new TwMotionOptions { Easing = TwEasing.Spring, Distance = 12 }, new TwMotionOptions { Easing = TwEasing.In });
        sheet.Show(owner);
        Application.DoEvents();
        Assert.True(TwAnimator.IsAnimating(sheet, "tw-window-slide"));
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(1.0, sheet.Opacity, 2);

        sheet.Hide();
        Assert.Equal(0.0, sheet.Opacity, 2);                                       // bir sonraki gösterişe saydam hazır
        sheet.Show(owner);
        Application.DoEvents();
        Assert.True(TwAnimator.IsAnimating(sheet, "tw-window-slide"));            // ikinci gösterişte de kayar
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(new Point(300, 200), sheet.Location);
        Assert.Equal(1.0, sheet.Opacity, 2);

        int before = TwFadeOut.ActiveCount;
        sheet.Close();
        WaitUntil(() => TwFadeOut.ActiveCount == before);
        owner.Close();
    }));

    [Fact]
    public void A_window_that_is_not_shown_yet_slides_in_once_shown() => Sta.Run(() => WithMotion(() =>
    {
        using var owner = ShowForm(new Size(400, 300));
        using var sheet = Sheet();
        TwWindowMotion.SlideIn(sheet, TwEdge.Right);
        Assert.Equal(0.0, sheet.Opacity, 2);                                       // gösterilene kadar saydam
        Assert.False(TwAnimator.IsAnimating(sheet, "tw-window-slide"));
        sheet.Show(owner);
        Application.DoEvents();
        Assert.True(TwAnimator.IsAnimating(sheet, "tw-window-slide"));
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(new Point(300, 200), sheet.Location);
        Assert.Equal(1.0, sheet.Opacity, 2);

        using var shown = Sheet();
        shown.Load += (_, _) => TwWindowMotion.SlideIn(shown, TwEdge.Left);       // gösterilirken (Load) çağrılan
        shown.Show(owner);
        Application.DoEvents();
        Assert.True(TwAnimator.IsAnimating(shown, "tw-window-slide"));
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(1.0, shown.Opacity, 2);
        shown.Close();
        sheet.Close();
        owner.Close();
    }));

    [Fact]
    public void A_panel_can_slide_without_fading_the_rest() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(400, 240));
        var grid = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var inspector = new Panel { Dock = DockStyle.Right, Width = 120, BackColor = Color.SteelBlue, Visible = false, Margin = new Padding(14, 0, 0, 0) };
        form.Controls.Add(grid);
        form.Controls.Add(inspector);
        Application.DoEvents();
        int before = TwFadeOut.ActiveCount;
        int open = Application.OpenForms.Count;

        TwTransition.SlideIn(inspector, TwEdge.Right, new TwMotionOptions { FadeRest = false, IncludeMargin = true, Easing = TwEasing.Spring });
        Assert.True(inspector.Visible);
        Assert.True(TwTransition.LastCovered);
        Assert.Equal(before, TwFadeOut.ActiveCount);                               // kalan alan hemen açıldı, yalnız panel kayıyor
        WaitUntil(() => Application.OpenForms.Count == open);
        Assert.Equal(open, Application.OpenForms.Count);

        TwTransition.SlideOut(inspector, TwEdge.Right, new TwMotionOptions { FadeRest = false, DurationMs = 120 });
        Assert.False(inspector.Visible);
        Assert.Equal(before, TwFadeOut.ActiveCount);
        WaitUntil(() => Application.OpenForms.Count == open);
        Assert.Equal(open, Application.OpenForms.Count);
        form.Close();
    }));

    [Theory]
    [InlineData(DockStyle.Left)]
    [InlineData(DockStyle.Right)]
    [InlineData(DockStyle.Top)]
    public void A_docked_panel_resizes_with_one_layout(DockStyle dock) => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(400, 300));
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var sidebar = new Panel { Dock = dock, Width = 200, Height = 120, BackColor = Color.SteelBlue };
        form.Controls.Add(content);
        form.Controls.Add(sidebar);
        Application.DoEvents();
        int layouts = 0;
        content.Resize += (_, _) => layouts++;
        int before = TwFadeOut.ActiveCount;
        int open = Application.OpenForms.Count;
        bool vertical = dock == DockStyle.Top;

        TwTransition.Resize(sidebar, 56, new TwMotionOptions { DurationMs = 120 });
        Assert.True(TwTransition.LastCovered);
        Assert.Equal(56, vertical ? sidebar.Height : sidebar.Width);
        WaitUntil(() => TwFadeOut.ActiveCount == before && Application.OpenForms.Count == open);
        Assert.Equal(1, layouts);                                                  // içerik bir kez yerleşti
        Assert.Equal(open, Application.OpenForms.Count);

        TwTransition.Resize(sidebar, 240, new TwMotionOptions { DurationMs = 120 });
        Assert.Equal(240, vertical ? sidebar.Height : sidebar.Width);
        WaitUntil(() => TwFadeOut.ActiveCount == before && Application.OpenForms.Count == open);
        Assert.Equal(2, layouts);

        var previous = TwMotion.Enabled;
        TwMotion.Enabled = false;
        TwTransition.Resize(sidebar, 100);
        Assert.False(TwTransition.LastCovered);
        Assert.Equal(100, vertical ? sidebar.Height : sidebar.Width);
        TwMotion.Enabled = previous;
        Assert.Throws<ArgumentOutOfRangeException>(() => TwTransition.Resize(sidebar, -1));
        form.Close();
    }));

    [Fact]
    public void A_resize_can_change_more_than_the_size() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(400, 300));
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var label = new Label { Text = "Kayıtlar", Dock = DockStyle.Top };
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = Color.SteelBlue };
        sidebar.Controls.Add(label);
        form.Controls.Add(content);
        form.Controls.Add(sidebar);
        Application.DoEvents();
        int before = TwFadeOut.ActiveCount;
        int open = Application.OpenForms.Count;

        TwTransition.Resize(sidebar, () =>
        {
            label.Visible = false;                                                 // metinler eski görüntünün altında gizlenir
            sidebar.Width = 56;
        }, new TwMotionOptions { DurationMs = 120, FadeRest = false });
        Assert.True(TwTransition.LastCovered);
        Assert.False(label.Visible);
        Assert.Equal(56, sidebar.Width);
        Assert.Equal(before, TwFadeOut.ActiveCount);                               // kalan alan hemen açıldı
        WaitUntil(() => Application.OpenForms.Count == open);

        TwTransition.Resize(sidebar, () =>
        {
            label.Visible = true;
            sidebar.Width = 200;
        }, new TwMotionOptions { DurationMs = 120, FadeRest = false });
        Assert.Equal(200, sidebar.Width);
        WaitUntil(() => Application.OpenForms.Count == open);
        Assert.Equal(open, Application.OpenForms.Count);
        form.Close();
    }));

    [Fact]
    public void Prewarm_creates_the_handles_of_a_hidden_page() => Sta.Run(() =>
    {
        using var form = ShowForm(new Size(400, 300));
        var page = new Panel { Dock = DockStyle.Fill, Visible = false };
        var inner = new Panel { Dock = DockStyle.Top, Height = 40 };
        var box = new TextBox { Dock = DockStyle.Fill };
        inner.Controls.Add(box);
        page.Controls.Add(inner);
        form.Controls.Add(page);
        Assert.False(box.IsHandleCreated);

        TwMotion.Prewarm(page);
        Assert.True(new Control[] { page, inner, box }.All(c => c.IsHandleCreated));
        Assert.False(page.Visible);
        Assert.Equal(40, inner.Height);
        form.Close();
    });

    [Fact]
    public void Double_buffering_can_be_set_on_any_control() => Sta.Run(() =>
    {
        using var grid = new DataGridView();
        var doubleBuffered = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.False((bool)doubleBuffered.GetValue(grid)!);
        TwMotion.SetDoubleBuffered(grid, true);
        Assert.True((bool)doubleBuffered.GetValue(grid)!);
        TwMotion.SetDoubleBuffered(grid, false);
        Assert.False((bool)doubleBuffered.GetValue(grid)!);
    });
}
