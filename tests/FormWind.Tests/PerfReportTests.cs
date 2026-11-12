using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Findings of the performance report of 0.1.7-preview.1: frame timers that outlive their control, shadow cache,
/// cross-fade, motion policy and per-control costs.
/// </summary>
[Collection(UiCollection.Name)]
public class PerfReportTests
{
    private static Form HostForm(out Panel page)
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(120, 120), ClientSize = new Size(420, 260) };
        page = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        return form;
    }

    private static void WaitUntil(Func<bool> condition, int milliseconds = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(5);
        }
    }

    private static DataGridView LoadingGrid(Panel page, TailwindProvider provider)
    {
        var grid = new DataGridView { AllowUserToAddRows = false, Dock = DockStyle.Fill };
        grid.Columns.Add("Name", "Name");
        page.Controls.Add(grid);
        provider.SetClasses(grid, "table");
        return grid;
    }

    [Fact]
    public void Grid_pulse_stops_when_loading_ends_while_the_grid_is_out_of_view() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm(out var page);
        var grid = LoadingGrid(page, provider);
        form.Show();
        try
        {
            int before = TwFrameClock.ActiveCount;
            TwGrid.SetLoading(grid, true);
            grid.Refresh();                                   // the skeleton paint starts the pulse
            Assert.Equal(before + 1, TwFrameClock.ActiveCount);

            page.Visible = false;                             // the user went to another page
            TwGrid.SetLoading(grid, false);                   // data arrived: no paint comes to the hidden grid
            Assert.Equal(before, TwFrameClock.ActiveCount);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Grid_pulse_leaves_the_clock_while_its_page_is_hidden_and_comes_back() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm(out var page);
        var grid = LoadingGrid(page, provider);
        form.Show();
        try
        {
            int before = TwFrameClock.ActiveCount;
            TwGrid.SetLoading(grid, true);
            grid.Refresh();
            Assert.Equal(before + 1, TwFrameClock.ActiveCount);

            page.Visible = false;
            TwFrameClock.Frame();                             // the next frame parks the timer of the hidden grid
            Assert.Equal(before, TwFrameClock.ActiveCount);

            page.Visible = true;                              // shown again: the pulse goes on
            Assert.Equal(before + 1, TwFrameClock.ActiveCount);
            TwGrid.SetLoading(grid, false);
            Assert.Equal(before, TwFrameClock.ActiveCount);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Frame_timer_with_a_hidden_owner_waits_off_the_clock() => Sta.Run(() =>
    {
        using var form = HostForm(out var page);
        var owner = new Panel();
        page.Controls.Add(owner);
        form.Show();
        page.Visible = false;
        try
        {
            int before = TwFrameClock.ActiveCount;
            using var timer = new TwFrameTimer { Owner = owner };
            timer.Start();
            Assert.True(timer.Enabled);
            Assert.True(timer.Parked);
            Assert.Equal(before, TwFrameClock.ActiveCount);

            page.Visible = true;
            Assert.False(timer.Parked);
            Assert.Equal(before + 1, TwFrameClock.ActiveCount);

            timer.Stop();
            Assert.Equal(before, TwFrameClock.ActiveCount);
            page.Visible = false;
            page.Visible = true;                              // a stopped timer does not come back
            Assert.Equal(before, TwFrameClock.ActiveCount);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Frame_timer_stops_when_its_owner_is_disposed() => Sta.Run(() =>
    {
        using var form = HostForm(out var page);
        var owner = new Panel();
        page.Controls.Add(owner);
        form.Show();
        page.Visible = false;
        int before = TwFrameClock.ActiveCount;
        using var timer = new TwFrameTimer { Owner = owner };
        timer.Start();
        owner.Dispose();
        Assert.False(timer.Enabled);
        Assert.Equal(before, TwFrameClock.ActiveCount);
        form.Close();
    });

    [Fact]
    public void Repeating_animations_stop_the_clock_while_no_control_is_shown() => Sta.Run(() =>
    {
        using var form = HostForm(out var page);
        var spinner = new Panel { Size = new Size(16, 16) };
        page.Controls.Add(spinner);
        form.Show();
        try
        {
            TwAnimations.Track(spinner, TwAnimation.Spin);
            Assert.True(TwAnimations.Running);

            page.Visible = false;
            WaitUntil(() => !TwAnimations.Running);
            Assert.False(TwAnimations.Running);

            page.Visible = true;
            Assert.True(TwAnimations.Running);
        }
        finally
        {
            TwAnimations.Track(spinner, TwAnimation.None);
            form.Close();
        }
    });

    // ---- F2: nine-slice shadows ----

    private static Bitmap DrawShadow(Size size, float radius, TwShadowSize preset, float opacity, bool slice)
    {
        var bitmap = new Bitmap(size.Width + 200, size.Height + 200);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);
        TwShadowRenderer.NoSliceForTests = !slice;
        try
        {
            foreach (var layer in TwShadowLayer.Preset(preset, null))
                TwShadowRenderer.Draw(g, new Rectangle(new Point(100, 100), size), radius, layer, opacity);
        }
        finally
        {
            TwShadowRenderer.NoSliceForTests = false;
        }
        return bitmap;
    }

    private static string LastDiff = "";

    private static int MaxChannelDifference(Bitmap a, Bitmap b)
    {
        int max = 0;
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
            {
                Color p = a.GetPixel(x, y), q = b.GetPixel(x, y);
                int d = Math.Max(Math.Max(Math.Abs(p.R - q.R), Math.Abs(p.G - q.G)), Math.Max(Math.Abs(p.B - q.B), Math.Abs(p.A - q.A)));
                if (d > max) { max = d; LastDiff = $"at {x},{y}: {p} vs {q}"; }
            }
        return max;
    }

    [Theory]
    [InlineData(TwShadowSize.Sm, 8f, 1f)]
    [InlineData(TwShadowSize.Base, 4f, 1f)]
    [InlineData(TwShadowSize.Md, 12f, 1f)]
    [InlineData(TwShadowSize.Lg, 0f, 1f)]
    [InlineData(TwShadowSize.Xl, 16f, 1f)]
    [InlineData(TwShadowSize.Xl2, 24f, 1f)]
    [InlineData(TwShadowSize.Lg, 8f, 0.5f)]
    public void Sliced_shadow_matches_the_shadow_blurred_at_full_size(TwShadowSize preset, float radius, float opacity)
    {
        foreach (var size in new[] { new Size(40, 30), new Size(160, 90), new Size(420, 130), new Size(37, 300) })
        {
            using var whole = DrawShadow(size, radius, preset, opacity, slice: false);
            using var sliced = DrawShadow(size, radius, preset, opacity, slice: true);
            int difference = MaxChannelDifference(whole, sliced);
            Assert.True(difference <= (opacity < 1f ? 1 : 0), $"{preset} r{radius} {size.Width}×{size.Height} opacity {opacity}: max difference {difference} {LastDiff}");
        }
    }

    [Fact]
    public void Resizing_cards_does_not_fill_the_shadow_cache()
    {
        TwShadowRenderer.Clear();
        var before = TwShadowRenderer.Stats;
        using var bitmap = new Bitmap(2000, 300);
        using var g = Graphics.FromImage(bitmap);
        foreach (var layer in TwShadowLayer.Preset(TwShadowSize.Sm, null))
            for (int width = 1100; width <= 1900; width += 20)          // a window resized in 40 steps
                TwShadowRenderer.Draw(g, new Rectangle(20, 20, width / 4, 200), 8, layer);

        var stats = TwShadowRenderer.Stats;
        Assert.Equal(1, stats.Count);                                     // one blurred picture for every width
        Assert.True(stats.Bytes < 64 * 1024, stats.ToString());
        Assert.Equal(before.Misses + 1, stats.Misses);
        Assert.Equal(TwDiagnostics.ShadowCache.Count, stats.Count);
    }

    [Fact]
    public void Shadow_cache_keeps_to_its_memory_budget()
    {
        TwShadowRenderer.Clear();
        using var bitmap = new Bitmap(10, 10);
        using var g = Graphics.FromImage(bitmap);
        g.SetClip(new Rectangle(-10000, -10000, 30000, 30000));
        // Narrow and tall: not sliced across, so every height is its own picture.
        var layer = new TwShadowLayer(0, 20, 0, Color.FromArgb(40, 0, 0, 0));
        for (int height = 200; height < 2000; height += 7)
            TwShadowRenderer.Draw(g, new Rectangle(0, 0, 3000, height), 0, layer);
        Assert.True(TwShadowRenderer.Stats.Bytes <= TwShadowRenderer.CacheBudgetBytes, TwShadowRenderer.Stats.ToString());
    }

    // ---- F3 and F5: cross-fade and diagnostics ----

    private static Form ShowForm(out Panel page)
    {
        var form = HostForm(out page);
        page.BackColor = Color.White;
        form.Show();
        form.Activate();
        Application.DoEvents();
        return form;
    }

    [Fact]
    public void A_new_cross_fade_ends_the_one_still_running_on_the_same_host() => Sta.Run(() =>
    {
        using var form = ShowForm(out var page);
        try
        {
            int before = TwFadeOut.ActiveCount;
            TwTransition.CrossFade(page, () => page.BackColor = Color.Black, 2000);
            Assert.True(TwTransition.IsFading(page));
            Assert.Equal(before + 1, TwFadeOut.ActiveCount);

            TwTransition.CrossFade(page, () => page.BackColor = Color.White, 2000);
            Application.DoEvents();
            Assert.Equal(before + 1, TwFadeOut.ActiveCount);              // the first picture is gone, not fading under the second
            Assert.True(TwTransition.IsFading(page));
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Cross_fade_reports_its_steps_to_diagnostics() => Sta.Run(() =>
    {
        using var form = ShowForm(out var page);
        TwTransitionEventArgs? reported = null;
        EventHandler<TwTransitionEventArgs> handler = (_, e) => reported = e;
        TwDiagnostics.TransitionCompleted += handler;
        try
        {
            TwTransition.CrossFade(page, () => page.BackColor = Color.Black, 150);
            Assert.NotNull(reported);
            Assert.Same(page, reported!.Host);
            Assert.Equal("CrossFade", reported.Kind);
            Assert.NotEqual("None", reported.CaptureSource);
            Assert.Equal(150, reported.DurationMs);
            Assert.True(reported.TotalMs >= reported.CaptureMs + reported.ChangeMs);
        }
        finally
        {
            TwDiagnostics.TransitionCompleted -= handler;
            form.Close();
        }
    });

    [Fact]
    public void Diagnostics_list_the_running_frame_timers_and_their_controls() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm(out var page);
        var grid = LoadingGrid(page, provider);
        grid.Name = "orders";
        form.Show();
        try
        {
            TwGrid.SetLoading(grid, true);
            grid.Refresh();
            var timer = Assert.Single(TwDiagnostics.ActiveFrameTimers, t => t.Source == "grid-pulse");
            Assert.Same(grid, timer.Control);
            Assert.True(timer.PausesWhenHidden);
            Assert.True(TwDiagnostics.IsAnimating);
            Assert.Contains("orders", timer.ToString());

            TwGrid.SetLoading(grid, false);
            Assert.DoesNotContain(TwDiagnostics.ActiveFrameTimers, t => t.Source == "grid-pulse");
        }
        finally
        {
            form.Close();
        }
    });

    // ---- F6: motion in a remote desktop session ----

    [Fact]
    public void Animations_are_off_in_a_remote_session_while_following_windows()
    {
        var previous = TwMotion.Preference;
        try
        {
            TwMotion.RemoteSessionForTests = () => true;
            TwMotion.Preference = TwMotionPreference.System;
            Assert.False(TwMotion.Enabled);

            TwMotion.ReduceInRemoteSession = false;
            TwMotion.RemoteSessionForTests = () => false;
            bool console = TwMotion.Enabled;                              // the Windows setting alone
            TwMotion.RemoteSessionForTests = () => true;
            Assert.Equal(console, TwMotion.Enabled);

            TwMotion.ReduceInRemoteSession = true;
            TwMotion.Preference = TwMotionPreference.On;                  // set in code: wins over the session
            Assert.True(TwMotion.Enabled);
        }
        finally
        {
            TwMotion.RemoteSessionForTests = null;
            TwMotion.ReduceInRemoteSession = true;
            TwMotion.Preference = previous;
        }
    }

    // ---- F4 and F7: per-control costs ----

    [Fact]
    public void Shared_resolution_is_cached_and_matches_a_fresh_one()
    {
        var parsed = TwTheme.Current.Parse("text-gray-700 disabled:text-gray-400 group");
        var shared = parsed.ResolveShared(TwState.Disabled);
        Assert.Same(shared, parsed.ResolveShared(TwState.Disabled));
        Assert.Equal(parsed.Resolve(TwState.Disabled).ForeColor, shared.ForeColor);
        Assert.Equal(parsed.Resolve(TwState.None).IsGroup, parsed.ResolveShared(TwState.None).IsGroup);
        Assert.Equal(parsed.ResolveExact(TwState.Disabled, TwState.Disabled).ForeColor, parsed.ResolveExactShared(TwState.Disabled, TwState.Disabled).ForeColor);
        Assert.NotSame(parsed.Resolve(TwState.None), parsed.Resolve(TwState.None));   // the public method still returns a new style
    }

    [Fact]
    public void Chip_preferred_size_follows_text_and_font_changes() => Sta.Run(() =>
    {
        using var chip = new TwChip { Text = "Açık" };
        var small = chip.GetPreferredSize(Size.Empty);
        Assert.Equal(small, chip.GetPreferredSize(Size.Empty));
        chip.Text = "Açık iş emirleri";
        var wide = chip.GetPreferredSize(Size.Empty);
        Assert.True(wide.Width > small.Width);
        using var big = new Font(chip.Font.FontFamily, chip.Font.SizeInPoints * 2);
        chip.Font = big;
        Assert.True(chip.GetPreferredSize(Size.Empty).Height > wide.Height);
    });

    [Fact]
    public void Data_list_pulse_stops_when_loading_ends_while_the_list_is_hidden() => Sta.Run(() =>
    {
        using var form = HostForm(out var page);
        var list = new TwDataList { Dock = DockStyle.Fill };
        page.Controls.Add(list);
        form.Show();
        try
        {
            int before = TwFrameClock.ActiveCount;
            list.IsLoading = true;
            list.Refresh();
            Assert.Equal(before + 1, TwFrameClock.ActiveCount);

            page.Visible = false;
            list.IsLoading = false;
            Assert.Equal(before, TwFrameClock.ActiveCount);
        }
        finally
        {
            form.Close();
        }
    });
}
