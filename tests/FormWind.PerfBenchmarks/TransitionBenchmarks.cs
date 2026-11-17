using System;
using System.Drawing;
using System.Windows.Forms;
using BenchmarkDotNet.Attributes;

namespace FormWind.PerfBenchmarks;

/// <summary>
/// A page switch with <see cref="TwTransition.CrossFade(Control, Action, TwMotionOptions)"/>. The form is off screen, so the
/// picture of the old page comes from the desktop window manager (<c>PrintWindow</c>, see <see cref="CaptureBenchmarks"/>).
/// </summary>
[MemoryDiagnoser]
public class TransitionBenchmarks
{
    private OffscreenHost _host = null!;
    private Panel _frame = null!;
    private Panel[] _pages = null!;
    private int _shown;

    /// <summary>
    /// <c>false</c>: duration 0, the change is applied without a picture (animations off). <c>true</c>: the default
    /// duration, the old look is captured and laid over the page before the change.
    /// </summary>
    [Params(false, true)]
    public bool Animated { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _host = new OffscreenHost(new Size(900, 620));
        _frame = new Panel { Dock = DockStyle.Fill };
        _host.Form.Controls.Add(_frame);
        _pages = new[] { TestPage.Create(_host.Provider, "A"), TestPage.Create(_host.Provider, "B") };
        _frame.Controls.Add(_pages[0]);
        _host.Show();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _host.Dispose();
        foreach (var page in _pages) page.Dispose();
    }

    // The fade's own frames are timer messages handled by Pump in both rows; a new switch ends the previous fade, as quick
    // navigation in an application does.
    [Benchmark]
    [STAThread]
    public void CrossFade()
    {
        var next = _pages[_shown = 1 - _shown];
        TwTransition.CrossFade(_frame, () =>
        {
            _frame.Controls.Clear();
            _frame.Controls.Add(next);
        }, Animated ? null : new TwMotionOptions { DurationMs = 0 });
        OffscreenHost.Pump();
    }
}
