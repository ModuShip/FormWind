using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace FormWind.PerfBenchmarks;

/// <summary>
/// A shown form far outside the desktop, so the benchmarks use real window handles, layout and painting without a window
/// appearing on screen. Its controls are styled by one <see cref="TailwindProvider"/>, as in an application.
/// </summary>
internal sealed class OffscreenHost : IDisposable
{
    private static readonly Point OffscreenLocation = new Point(-20000, -20000);

    public OffscreenHost(Size clientSize)
    {
        // Without an STA thread WinForms fails late and confusingly (drag-and-drop, clipboard, OLE); fail early instead.
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("The benchmarks must run on an STA thread: mark the benchmark methods with [STAThread].");

        Provider = new TailwindProvider();
        Form = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = OffscreenLocation,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            ClientSize = clientSize,
        };
    }

    public TailwindProvider Provider { get; }

    public Form Form { get; }

    /// <summary>Shows the form and lets the first layout and paint finish, so they are not part of the first measurement.</summary>
    public void Show()
    {
        Form.Show();
        Pump();
    }

    /// <summary>Handles the pending window messages (paints, timers of fades and hover animations).</summary>
    public static void Pump() => Application.DoEvents();

    public void Dispose()
    {
        Form.Dispose();
        Provider.Dispose();
    }
}
