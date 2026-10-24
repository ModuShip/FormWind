using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>Closing fade, value animations (progress, stat, chart) and collapsing panels.</summary>
[Collection(UiCollection.Name)]
public class AnimationValueTests
{
    private static Form Host() =>
        new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, Size = new Size(400, 300) };

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

    private static void WaitUntil(Func<bool> condition, int milliseconds = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    [Theory]
    [InlineData("tr-TR", "₺12.400", 12400, 9731, "₺9.731")]
    [InlineData("tr-TR", "98,4%", 98.4, 50.26, "50,3%")]
    [InlineData("tr-TR", "-5 adet", -5, -2, "-2 adet")]
    [InlineData("en-US", "$1,234.50", 1234.5, 99, "$99.00")]
    [InlineData("en-US", "42 ms", 42, 7.6, "8 ms")]
    public void Number_text_keeps_its_form(string culture, string text, double value, double other, string formatted)
    {
        Assert.True(TwNumberText.TryParse(text, new CultureInfo(culture), out var number));
        Assert.Equal(value, number!.Value, 6);
        Assert.Equal(formatted, number.Format(other));
    }

    [Theory]
    [InlineData("278 h 52 min")]
    [InlineData("—")]
    [InlineData("")]
    public void Number_text_needs_exactly_one_number(string text) =>
        Assert.False(TwNumberText.TryParse(text, CultureInfo.InvariantCulture, out _));

    [Fact]
    public void Stat_counts_a_new_value_up_only_when_motion_is_enabled() => Sta.Run(() =>
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
        using var form = Host();
        var stat = new TwStat { Title = "Ciro" };
        form.Controls.Add(stat);
        form.Show();
        try
        {
            WithMotion(true, () =>
            {
                stat.Value = "₺12.400";
                Assert.NotEqual("₺12.400", stat.CountedText(stat.Value));            // sayıyor
                Assert.StartsWith("₺", stat.CountedText(stat.Value));
                WaitUntil(() => stat.CountedText(stat.Value) == "₺12.400");
                Assert.Equal("₺12.400", stat.CountedText(stat.Value));

                stat.Value = "278 sa 52 dk";                                           // iki sayı: sayılmaz
                Assert.Equal("278 sa 52 dk", stat.CountedText(stat.Value));

                stat.AnimateValue = false;
                stat.Value = "₺15.000";
                Assert.Equal("₺15.000", stat.CountedText(stat.Value));
            });
            WithMotion(false, () =>
            {
                stat.AnimateValue = true;
                stat.Value = "₺20.000";
                Assert.Equal("₺20.000", stat.CountedText(stat.Value));
            });
            Assert.Equal("₺20.000", stat.Value);
        }
        finally
        {
            form.Close();
            Thread.CurrentThread.CurrentCulture = culture;
        }
    });

    [Fact]
    public void Progress_fill_slides_to_a_new_value() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        var progress = new TwProgress { Size = new Size(200, 8) };
        form.Controls.Add(progress);
        form.Show();
        try
        {
            WithMotion(true, () =>
            {
                progress.Value = 80;
                Assert.True(progress.FillFraction < 0.8f);
                Assert.Equal(80, progress.Value);                                      // değer hemen yeni değer
                WaitUntil(() => progress.FillFraction >= 0.8f);
                Assert.Equal(0.8f, progress.FillFraction, 3);
                Assert.Equal(160, progress.BarBounds.Width);

                provider.SetClasses(progress, "transition-none");
                progress.Value = 20;
                Assert.Equal(0.2f, progress.FillFraction, 3);
            });
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Chart_opens_once_when_first_shown_with_data() => Sta.Run(() =>
    {
        using var form = Host();
        var chart = new TwChart { Size = new Size(300, 200) };
        chart.SetData(new[] { "Oca", "Şub", "Mar" }, new TwChartSeries("Satış", new double[] { 4, 7, 5 }));
        form.Controls.Add(chart);
        WithMotion(true, () =>
        {
            form.Show();
            Application.DoEvents();
            Assert.True(chart.IntroProgress < 1f);
            using (var bitmap = new Bitmap(chart.Width, chart.Height)) chart.DrawToBitmap(bitmap, new Rectangle(Point.Empty, chart.Size));
            WaitUntil(() => chart.IntroProgress >= 1f);
            Assert.Equal(1f, chart.IntroProgress);

            chart.SetData(new[] { "Oca", "Şub" }, new TwChartSeries("Satış", new double[] { 1, 2 }));
            Assert.Equal(1f, chart.IntroProgress);                                     // veri değişimi anında

            chart.AnimateIn();
            Assert.True(chart.IntroProgress < 1f);
        });
        WithMotion(false, () =>
        {
            chart.AnimateIn();
            Assert.Equal(1f, chart.IntroProgress);
        });
        form.Close();
    });

    [Fact]
    public void Chart_without_animate_on_show_draws_at_once() => Sta.Run(() =>
    {
        using var form = Host();
        var chart = new TwChart { Size = new Size(300, 200), AnimateOnShow = false };
        chart.SetData(new[] { "A", "B" }, new TwChartSeries("S", new double[] { 1, 2 }));
        form.Controls.Add(chart);
        WithMotion(true, () =>
        {
            form.Show();
            Assert.Equal(1f, chart.IntroProgress);
        });
        form.Close();
    });

    [Fact]
    public void Panel_collapses_and_expands_by_sliding_its_height() => Sta.Run(() =>
    {
        using var form = Host();
        var panel = new TwPanel { Dock = DockStyle.Top, Height = 100 };
        var child = new Button { Location = new Point(10, 10), Size = new Size(80, 24) };
        panel.Controls.Add(child);
        var below = new TwPanel { Dock = DockStyle.Fill };
        form.Controls.Add(below);
        form.Controls.Add(panel);
        form.Show();
        int changes = 0;
        panel.ExpandedChanged += (_, _) => changes++;
        try
        {
            WithMotion(true, () =>
            {
                panel.Collapse();
                Assert.False(panel.Expanded);
                Assert.True(panel.IsCollapsing);
                Assert.True(panel.Visible);
                WaitUntil(() => !panel.IsCollapsing);
                Assert.False(panel.Visible);
                Assert.Equal(100, panel.Height);                                        // gizli panel yüksekliğini korur
                Assert.Equal(new Point(10, 10), child.Location);                        // çocuk yerinde kaldı

                panel.Expand();
                Assert.True(panel.Visible);
                Assert.True(panel.Height < 100);
                Thread.Sleep(60);
                panel.Collapse();                                                       // açılırken geri kapanır
                panel.Expand();
                WaitUntil(() => !panel.IsCollapsing);
                Assert.Equal(100, panel.Height);
                Assert.True(panel.Visible);
            });
            WithMotion(false, () =>
            {
                panel.Collapse();
                Assert.False(panel.Visible);
                Assert.False(panel.IsCollapsing);
                panel.Expand();
                Assert.True(panel.Visible);
                Assert.Equal(100, panel.Height);
            });
            Assert.Equal(6, changes);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Auto_size_panel_expands_to_its_preferred_height() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        var panel = new TwPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Location = new Point(0, 0) };
        panel.Controls.Add(new Button { Location = new Point(0, 0), Size = new Size(80, 50) });
        form.Controls.Add(panel);
        form.Show();
        try
        {
            int height = panel.Height;
            WithMotion(true, () =>
            {
                provider.SetClasses(panel, "duration-75");
                panel.Collapse();
                WaitUntil(() => !panel.IsCollapsing);
                panel.Expand();
                Assert.False(panel.AutoSize);                                           // kayarken kapalı
                WaitUntil(() => !panel.IsCollapsing);
                Assert.True(panel.AutoSize);
                Assert.Equal(height, panel.Height);
            });
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Dialog_and_popover_close_at_once_and_fade_their_picture() => Sta.Run(() =>
    {
        using var owner = Host();
        owner.Show();
        var anchor = new Button { Size = new Size(80, 24) };
        owner.Controls.Add(anchor);
        using var popover = new TwPopover { Content = new Panel { Size = new Size(120, 60) } };
        try
        {
            WithMotion(true, () =>
            {
                // Ekran dışındaki pencerenin görüntüsü okunamaz: solma yapılmaz, kapanış yine hemen olur.
                using var dialog = TwDialog.Create(new TwDialogOptions { Message = "Kaydedildi" }, owner);
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-10000, -10000);
                dialog.Show(owner);
                int before = TwFadeOut.ActiveCount;
                dialog.Close();
                Assert.False(dialog.Visible);
                Assert.Equal(before, TwFadeOut.ActiveCount);

                popover.Show(anchor);
                popover.Close();
                Assert.False(popover.IsOpen);
            });
        }
        finally
        {
            owner.Close();
        }
    });

    [Fact]
    public void Fade_out_picture_of_an_on_screen_window_disappears_by_itself() => Sta.Run(() =>
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        using var window = new Form
        {
            StartPosition = FormStartPosition.Manual, FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
            Bounds = new Rectangle(area.Left, area.Top, 40, 40), BackColor = Color.White,
        };
        window.Show();
        Application.DoEvents();
        WithMotion(true, () =>
        {
            int before = TwFadeOut.ActiveCount;
            TwFadeOut.Play(window, null, 60, roundCorners: false);
            window.Close();
            if (TwFadeOut.ActiveCount == before) return;                               // ekran okunamadı (kilitli oturum)
            Assert.Equal(before + 1, TwFadeOut.ActiveCount);
            WaitUntil(() => TwFadeOut.ActiveCount == before);
            Assert.Equal(before, TwFadeOut.ActiveCount);
        });
        WithMotion(false, () =>
        {
            int before = TwFadeOut.ActiveCount;
            TwFadeOut.Play(window, null, 60, roundCorners: false);
            Assert.Equal(before, TwFadeOut.ActiveCount);
        });
    });
}
