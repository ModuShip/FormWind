using System;
using System.Drawing;
using System.Windows.Forms;
using BenchmarkDotNet.Attributes;

namespace FormWind.PerfBenchmarks;

/// <summary>
/// The picture of a page with <see cref="TwCapture.Take(Control)"/>, the first step of every transition. The form is off
/// screen, so the picture comes from the desktop window manager (<c>PrintWindow</c>), the path FormWind takes when the
/// window is covered; a copy from the screen is usually faster.
/// </summary>
[MemoryDiagnoser]
public class CaptureBenchmarks
{
    private OffscreenHost _host = null!;
    private Panel _page = null!;

    [GlobalSetup]
    public void Setup()
    {
        _host = new OffscreenHost(new Size(900, 620));
        _page = TestPage.Create(_host.Provider, "A");
        _host.Form.Controls.Add(_page);
        _host.Show();
    }

    [GlobalCleanup]
    public void Cleanup() => _host.Dispose();

    [Benchmark]
    [STAThread]
    public void Capture()
    {
        using var picture = TwCapture.Take(_page)
            ?? throw new InvalidOperationException("No picture of the page could be taken.");
    }
}
