using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using BenchmarkDotNet.Attributes;

namespace FormWind.PerfBenchmarks;

/// <summary>
/// Painting and resizing a page of cards with <c>shadow-sm</c> and <c>shadow-md</c>. The shadows are drawn by the parent,
/// so the page is measured with a double-buffered and a non-double-buffered parent.
/// </summary>
[MemoryDiagnoser]
public class CardPageBenchmarks
{
    public const int CardCount = 14;
    public const int ResizeSteps = 40;

    private static readonly Size PageSize = new Size(900, 620);

    private OffscreenHost _host = null!;
    private CardPage _page = null!;
    private Bitmap _surface = null!;

    [Params(false, true)]
    public bool DoubleBufferedParent { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _host = new OffscreenHost(PageSize);
        _page = new CardPage(DoubleBufferedParent) { Dock = DockStyle.Fill, Padding = new Padding(16) };
        _host.Form.Controls.Add(_page);
        _host.Provider.SetClasses(_page, "bg-surface-50");
        for (int i = 0; i < CardCount; i++)
        {
            var card = new TwPanel { Size = new Size(240, 140), Margin = new Padding(16) };
            _page.Controls.Add(card);
            _host.Provider.SetClasses(card, i % 2 == 0 ? "card shadow-sm" : "card shadow-md");
        }
        _host.Show();
        _surface = new Bitmap(PageSize.Width, PageSize.Height, PixelFormat.Format32bppPArgb);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _surface.Dispose();
        _host.Dispose();
    }

    /// <summary>One full paint of the page and its cards (<see cref="Control.DrawToBitmap"/> of the container).</summary>
    [Benchmark]
    [STAThread]
    public void Paint() => _page.DrawToBitmap(_surface, new Rectangle(Point.Empty, _page.Size));

    /// <summary>Resizing the form in <see cref="ResizeSteps"/> steps, with the layout and paint of each step, as a user drags its edge.</summary>
    [Benchmark]
    [STAThread]
    public void Resize()
    {
        for (int i = 1; i <= ResizeSteps; i++)
        {
            int delta = (i % 10) * 12;
            _host.Form.ClientSize = new Size(PageSize.Width - delta, PageSize.Height - delta / 2);
            _host.Form.Refresh();
        }
        _host.Form.ClientSize = PageSize;
    }

    /// <summary>A card container whose double buffering is chosen by the benchmark (the property is protected).</summary>
    private sealed class CardPage : FlowLayoutPanel
    {
        public CardPage(bool doubleBuffered) => DoubleBuffered = doubleBuffered;
    }
}
