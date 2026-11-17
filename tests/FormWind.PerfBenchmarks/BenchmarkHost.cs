using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace FormWind.PerfBenchmarks;

/// <summary>Where <see cref="BenchmarkHost"/> puts its form.</summary>
internal enum HostPlacement
{
    /// <summary>
    /// Far outside the desktop. Windows paints nothing there (<see cref="Control.Refresh"/> returns without a paint), so
    /// use it only where the benchmark paints itself (<see cref="Control.DrawToBitmap"/>) or needs no paint.
    /// </summary>
    Offscreen,

    /// <summary>
    /// On the primary screen but fully transparent, not activated and letting clicks through: Windows sends real paint
    /// messages, as for a visible window, without anything appearing. Not for screen captures, which would copy what is
    /// behind the window.
    /// </summary>
    InvisibleOnScreen,
}

/// <summary>
/// A shown form for the benchmarks, so they use real window handles, layout and painting without a window appearing on
/// screen. Its controls are styled by one <see cref="TailwindProvider"/>, as in an application.
/// </summary>
internal sealed class BenchmarkHost : IDisposable
{
    private static readonly Point OffscreenLocation = new Point(-20000, -20000);

    public BenchmarkHost(Size clientSize, HostPlacement placement = HostPlacement.Offscreen)
    {
        // Without an STA thread WinForms fails late and confusingly (drag-and-drop, clipboard, OLE); fail early instead.
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("The benchmarks must run on an STA thread: mark the benchmark methods with [STAThread].");

        // As in an application's Main: native controls paint with the themed renderer (before the first window).
        Application.EnableVisualStyles();
        Provider = new TailwindProvider();
        bool invisible = placement == HostPlacement.InvisibleOnScreen;
        Form = new HostForm(clickThrough: invisible)
        {
            StartPosition = FormStartPosition.Manual,
            Location = invisible ? Screen.PrimaryScreen!.WorkingArea.Location : OffscreenLocation,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            ClientSize = clientSize,
        };
        if (invisible) Form.Opacity = 0;
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

    /// <summary>A form that never takes the focus from the user's window while the benchmarks run.</summary>
    private sealed class HostForm : Form
    {
        private const int WsExTransparent = 0x00000020, WsExNoActivate = 0x08000000;

        private readonly bool _clickThrough;

        public HostForm(bool clickThrough) => _clickThrough = clickThrough;

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExNoActivate;
                if (_clickThrough) parameters.ExStyle |= WsExTransparent;
                return parameters;
            }
        }
    }
}
