using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Xunit;
using Xunit.Abstractions;

namespace FormWind.Tests.Performance;

/// <summary>
/// Baselines for the 0.1.8 development report: the steps of a page cross-fade (capture, cover, change, paint) and resizing
/// a page of shadowed cards. They print the medians so a change can be compared with the previous version; the limits are
/// only loose sanity checks (timings on a shared machine vary by 30–60 % between runs).
/// </summary>
[Collection(PerformanceCollection.Name)]
[Trait("Category", "Performance")]
public class TransitionAndShadowBaselineTests
{
    private const int PageControls = 60;
    private const int Switches = 30;
    private const int Cards = 12;
    private const int ResizeSteps = 40;

    private readonly ITestOutputHelper _output;

    public TransitionAndShadowBaselineTests(ITestOutputHelper output) => _output = output;

    // Both pages on one UI thread, as in an application: a cross-fade test run after another one on a new STA thread
    // of the same process measures several times slower capture and paint (not yet explained; see the 0.1.8 report work).
    [PerformanceFact]
    public void Page_cross_fade_steps_are_measured_with_and_without_card_shadows() => Sta.Run(() =>
    {
        MeasureCrossFade("card shadow-md");
        MeasureCrossFade("card");
    });

    private void MeasureCrossFade(string cardClasses)
    {
        using var provider = new TailwindProvider();
        using var form = new Form
        {
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(80, 80), ClientSize = new Size(900, 620),
        };
        var host = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(host);
        var pages = new[] { Page(provider, "A", cardClasses), Page(provider, "B", cardClasses) };
        host.Controls.Add(pages[0]);
        form.Show();
        form.Activate();
        Pump(200);

        var steps = new List<TwTransitionEventArgs>();
        long drawnBefore = TwDiagnostics.ShadowCache.Hits + TwDiagnostics.ShadowCache.Misses;
        EventHandler<TwTransitionEventArgs> record = (_, e) => steps.Add(e);
        TwDiagnostics.TransitionCompleted += record;
        try
        {
            for (int i = 0; i <= Switches; i++)
            {
                var next = pages[(i + 1) % 2];
                TwTransition.CrossFade(host, () =>
                {
                    host.Controls.Clear();
                    host.Controls.Add(next);
                });
                // The user looks at the new page before switching again: the fade ends first.
                WaitUntil(() => !TwTransition.IsFading(host));
            }
        }
        finally
        {
            TwDiagnostics.TransitionCompleted -= record;
            foreach (var page in pages) page.Dispose();
        }

        var measured = steps.Skip(1).ToList();   // the first switch warms up JIT and caches
        long drawn = TwDiagnostics.ShadowCache.Hits + TwDiagnostics.ShadowCache.Misses - drawnBefore;
        Assert.NotEmpty(measured);
        _output.WriteLine($"Cross-fade, cards \"{cardClasses}\", {measured.Count} switches of a {PageControls}-control page ({form.ClientSize.Width}×{form.ClientSize.Height}):");
        _output.WriteLine($"  shadow layers drawn: {drawn / (double)steps.Count:F0} per switch");
        _output.WriteLine($"  sources: {string.Join(", ", measured.GroupBy(s => s.CaptureSource).Select(g => $"{g.Key} {g.Count()}"))}");
        Report("capture", measured.Select(s => s.CaptureMs));
        Report("cover", measured.Select(s => s.CoverMs));
        Report("change", measured.Select(s => s.ChangeMs));
        Report("paint", measured.Select(s => s.PaintMs));
        Report("total", measured.Select(s => s.TotalMs));

        Assert.True(Median(measured.Select(s => s.TotalMs)) < 1000, "A cross-fade held the UI thread for more than a second.");
    }

    [PerformanceFact]
    public void Resizing_a_page_of_shadowed_cards_is_measured() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form
        {
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(80, 80), ClientSize = new Size(900, 620),
        };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        form.Controls.Add(layout);
        provider.SetClasses(layout, "bg-gray-50");
        for (int i = 0; i < Cards; i++)
        {
            var card = new TwPanel { Size = new Size(240, 140), Margin = new Padding(16) };
            layout.Controls.Add(card);
            provider.SetClasses(card, i % 2 == 0 ? "card shadow-lg" : "card shadow-md");
        }
        form.Show();
        Pump(200);

        var timings = new List<double>();
        for (int i = 0; i <= ResizeSteps; i++)
        {
            int delta = (i % 10) * 12;
            var stopwatch = Stopwatch.StartNew();
            form.ClientSize = new Size(900 - delta, 620 - delta / 2);
            form.Refresh();
            stopwatch.Stop();
            if (i > 0) timings.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        _output.WriteLine($"Resize with {Cards} shadowed cards, {timings.Count} steps:");
        Report("resize+paint", timings);
        _output.WriteLine($"  shadow cache: {TwDiagnostics.ShadowCache}");
        Assert.True(Median(timings) < 500, "Resizing a page of cards took more than half a second per step.");
    });

    // Where the time of a card shadow goes: blended pixels (whole picture) or the number of parts drawn (sliced).
    [PerformanceFact]
    public void Shadow_layer_drawing_cost_is_measured() => Sta.Run(() =>
    {
        const int rounds = 300;
        var card = new Rectangle(40, 40, 240, 140);
        var layers = TwShadowLayer.Preset(TwShadowSize.Md, null);
        using var surface = new Bitmap(400, 300, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(surface);
        foreach (var (name, slice, underBox) in new[] { ("whole picture", false, false), ("sliced", true, false), ("sliced, under box skipped", true, true) })
        {
            TwShadowRenderer.NoSliceForTests = !slice;
            try
            {
                foreach (var layer in layers) TwShadowRenderer.Draw(g, card, 8, layer, 1f, underBox);   // cache
                var stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < rounds; i++)
                    foreach (var layer in layers) TwShadowRenderer.Draw(g, card, 8, layer, 1f, underBox);
                stopwatch.Stop();
                _output.WriteLine($"{name,-27} {stopwatch.Elapsed.TotalMilliseconds * 1000 / rounds,7:F0} µs per card ({layers.Length} layers)");
            }
            finally
            {
                TwShadowRenderer.NoSliceForTests = false;
            }
        }
    });

    // Why captured pictures are premultiplied: a sliding picture is drawn again on every frame.
    [PerformanceFact]
    public void Picture_formats_are_compared_for_capture_and_per_frame_drawing() => Sta.Run(() =>
    {
        const int rounds = 40;
        using var form = new Form
        {
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(80, 80), ClientSize = new Size(900, 620),
        };
        form.Show();
        Pump(200);
        var bounds = form.RectangleToScreen(form.ClientRectangle);
        foreach (var format in new[] { PixelFormat.Format32bppArgb, PixelFormat.Format32bppPArgb, PixelFormat.Format32bppRgb })
        {
            Bitmap? picture = null;
            var capture = Stopwatch.StartNew();
            for (int i = 0; i < rounds; i++)
            {
                picture?.Dispose();
                picture = new Bitmap(bounds.Width, bounds.Height, format);
                using var g = Graphics.FromImage(picture);
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            capture.Stop();
            using (picture)
            using (var frame = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb))
            using (var g = Graphics.FromImage(frame))
            {
                var draw = Stopwatch.StartNew();
                for (int i = 0; i < rounds; i++) g.DrawImageUnscaled(picture!, 0, 0);
                draw.Stop();
                _output.WriteLine($"{format,-22} capture {capture.Elapsed.TotalMilliseconds / rounds,6:F2} ms   draw {draw.Elapsed.TotalMilliseconds / rounds,6:F2} ms   alpha {picture!.GetPixel(10, 10).A}");
                Assert.Equal(255, picture.GetPixel(10, 10).A);
            }
        }
    });

    private static Panel Page(TailwindProvider provider, string name, string cardClasses)
    {
        var page = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        provider.SetClasses(page, "bg-gray-50");
        for (int i = 0; i < PageControls; i++)
        {
            Control control = (i % 4) switch
            {
                0 => new Label { AutoSize = true, Text = $"{name} label {i}" },
                1 => new TwButton { Size = new Size(120, 36), Text = $"{name} {i}" },
                2 => new TwPanel { Size = new Size(160, 70) },
                _ => new TwTextBox { Size = new Size(160, 36), Text = $"{name} text {i}" },
            };
            page.Controls.Add(control);
            provider.SetClasses(control, (i % 4) switch
            {
                0 => "text-sm text-gray-700",
                1 => "btn-primary",
                2 => cardClasses,
                _ => "input",
            });
        }
        return page;
    }

    private void Report(string step, IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        _output.WriteLine($"  {step,-13} median {Median(sorted),6:F1} ms   p90 {sorted[(int)(sorted.Count * 0.9)],6:F1} ms   max {sorted[sorted.Count - 1],6:F1} ms");
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    private static void Pump(int milliseconds) => WaitUntil(() => false, milliseconds);

    private static void WaitUntil(Func<bool> condition, int milliseconds = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }
}
