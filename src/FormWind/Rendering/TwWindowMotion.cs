using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>An edge of a window or a control, for slides.</summary>
public enum TwEdge
{
    Left,
    Top,
    Right,
    Bottom,
}

/// <summary>
/// Entrance and exit of windows such as sheets and side windows: the window slides in from an edge while it fades in, and
/// when it closes its last picture fades out while sliding back. The window itself closes at once (<c>DialogResult</c>
/// and the return of <c>ShowDialog</c> are not delayed). Nothing moves when animations are off.
/// <code>
/// var sheet = new FilterSheet();
/// TwWindowMotion.Attach(sheet, TwEdge.Right);   // every Show/ShowDialog slides in, every close slides out
/// sheet.ShowDialog(this);
/// </code>
/// </summary>
public static class TwWindowMotion
{
    /// <summary>Default of <see cref="Distance"/>.</summary>
    internal const int DefaultDistance = 24;

    private const string SlideName = "tw-window-slide";

    private static int _distance = DefaultDistance;

    private static readonly ConditionalWeakTable<Form, Attached> Attachments = new ConditionalWeakTable<Form, Attached>();

    /// <summary>
    /// How far (96 DPI px) windows slide when <see cref="TwMotionOptions.Distance"/> is not given (default 24); 0 only fades.
    /// Applies to slides that start afterwards.
    /// </summary>
    public static int Distance
    {
        get => _distance;
        set => _distance = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "A distance cannot be negative.");
    }

    /// <summary>
    /// Plays the entrance and exit of <paramref name="window"/> from now on: it slides in from <paramref name="edge"/> each
    /// time it is shown (also when it is shown again after being hidden) and fades out toward the same edge each time it
    /// closes. Call it before the window is shown; a second call changes the edge.
    /// </summary>
    public static void Attach(Form window, TwEdge edge) => Attach(window, edge, null, null);

    /// <summary>
    /// Plays the entrance and exit of <paramref name="window"/> with their own settings (see <see cref="Attach(Form, TwEdge)"/>):
    /// for example a spring entrance and an ease-in exit. <c>null</c> keeps the defaults (<see cref="TwMotion.Timings.WindowIn"/>
    /// ease-out, <see cref="TwMotion.Timings.WindowOut"/> ease-in). A second call replaces the edge and the settings.
    /// </summary>
    public static void Attach(Form window, TwEdge edge, TwMotionOptions? enter, TwMotionOptions? exit)
    {
        if (window is null) throw new ArgumentNullException(nameof(window));
        if (Attachments.TryGetValue(window, out var attached))
        {
            attached.Edge = edge;
            attached.Enter = enter;
            attached.Exit = exit;
            return;
        }
        attached = new Attached { Edge = edge, Enter = enter, Exit = exit, Opacity = window.Opacity > 0 ? window.Opacity : 1 };
        Attachments.Add(window, attached);
        // Gösterilmeden önce saydam kurulur; aksi halde yerinde bir kare görünüp sonra kayar. İlk gösteriş Load'dan,
        // sonrakiler gizlenirken bırakılan saydamlıktan başlar.
        window.Load += (_, _) => HideForEntrance(window, attached);
        window.VisibleChanged += (_, _) =>
        {
            if (window.Visible) StartWhenShown(window, attached.Edge, attached.Enter, attached.Opacity);
            else
            {
                TwAnimator.Cancel(window, SlideName);
                HideForEntrance(window, attached);
            }
        };
        window.FormClosing += (_, e) =>
        {
            if (!e.Cancel) FadeOut(window, attached.Edge, attached.Exit);
        };
    }

    private static void HideForEntrance(Form window, Attached attached)
    {
        if (!TwMotion.Enabled || window.IsDisposed || window.Disposing) return;
        if (window.Opacity > 0 && !TwAnimator.IsAnimating(window, SlideName)) attached.Opacity = window.Opacity;
        window.Opacity = 0;
    }

    /// <summary>Slides a window in from <paramref name="edge"/> while it fades in (<see cref="TwMotion.Timings.WindowIn"/>).</summary>
    public static void SlideIn(Form window, TwEdge edge) => SlideIn(window, edge, (TwMotionOptions?)null);

    /// <summary>Slides a window in from <paramref name="edge"/> while it fades in, in <paramref name="durationMs"/>.</summary>
    public static void SlideIn(Form window, TwEdge edge, int durationMs) =>
        SlideIn(window, edge, new TwMotionOptions { DurationMs = durationMs });

    /// <summary>
    /// Slides a window in from <paramref name="edge"/> while it fades in, with <paramref name="options"/> (duration, curve,
    /// distance). A window that is not on screen yet (created but not shown, or being shown: in <c>Load</c> or
    /// <c>VisibleChanged</c>) is made transparent now and slides in once it is shown.
    /// </summary>
    public static void SlideIn(Form window, TwEdge edge, TwMotionOptions? options)
    {
        if (window is null) throw new ArgumentNullException(nameof(window));
        double opacity = window.Opacity > 0 ? window.Opacity : 1;
        if (TwMotion.Enabled && !IsOnScreen(window) && !window.IsDisposed)
        {
            window.Opacity = 0;
            StartWhenShown(window, edge, options, opacity);
            return;
        }
        Slide(window, edge, options, opacity);
    }

    // Pencere ekrana çıkınca (Shown gibi mesaj kuyruğundan sonra) kayma başlar; konum o ana kadar yerleşmiş olur.
    private static void StartWhenShown(Form window, TwEdge edge, TwMotionOptions? options, double opacity)
    {
        if (window.Visible && window.IsHandleCreated)
        {
            window.BeginInvoke(new MethodInvoker(() =>
            {
                if (!window.IsDisposed && window.Visible) Slide(window, edge, options, opacity);
            }));
            return;
        }
        void Shown(object? sender, EventArgs e)
        {
            if (!window.Visible) return;
            window.VisibleChanged -= Shown;
            StartWhenShown(window, edge, options, opacity);
        }
        window.VisibleChanged += Shown;
    }

    private static void Slide(Form window, TwEdge edge, TwMotionOptions? options, double opacity)
    {
        var timing = TwMotionOptions.Timing(options, TwMotion.Timings.WindowIn, TwEasing.Out);
        if (TwAnimator.IsAnimating(window, SlideName)) return;
        var target = window.Location;
        if (!TwMotion.CanAnimate(window) || timing.TotalMs <= 0)
        {
            window.Opacity = opacity;
            return;
        }
        var offset = Offset(edge, Scaled(window, options));
        var start = new Point(target.X + offset.X, target.Y + offset.Y);
        window.Opacity = 0;
        window.Location = start;
        TwAnimator.Animate(window, SlideName, 0, 1, timing, p =>
        {
            if (window.IsDisposed) return;
            window.Location = new Point(start.X + (int)Math.Round((target.X - start.X) * p), start.Y + (int)Math.Round((target.Y - start.Y) * p));
            window.Opacity = opacity * Math.Max(0, Math.Min(1, p));
        }, () =>
        {
            if (window.IsDisposed) return;
            window.Location = target;
            window.Opacity = opacity;
        });
    }

    /// <summary>
    /// Fades the last picture of a closing window out while it slides toward <paramref name="edge"/>
    /// (<see cref="TwMotion.Timings.WindowOut"/>). Call it just before the window closes (in <c>FormClosing</c>); the window
    /// itself closes at once. With <c>null</c> it only fades.
    /// </summary>
    public static void FadeOut(Form window, TwEdge? edge) => FadeOut(window, edge, (TwMotionOptions?)null);

    /// <summary>Fades the last picture of a closing window out (sliding toward <paramref name="edge"/>) in <paramref name="durationMs"/>.</summary>
    public static void FadeOut(Form window, TwEdge? edge, int durationMs) =>
        FadeOut(window, edge, new TwMotionOptions { DurationMs = durationMs });

    /// <summary>Fades the last picture of a closing window out (sliding toward <paramref name="edge"/>) with <paramref name="options"/>.</summary>
    public static void FadeOut(Form window, TwEdge? edge, TwMotionOptions? options)
    {
        if (window is null) throw new ArgumentNullException(nameof(window));
        var timing = TwMotionOptions.Timing(options, TwMotion.Timings.WindowOut, TwEasing.In);
        if (!window.Visible || !TwMotion.CanAnimate(window) || timing.TotalMs <= 0) return;
        TwAnimator.Cancel(window, SlideName);
        var bounds = TwCapture.VisibleFormBounds(window);
        var picture = TwCapture.Take(window, bounds);
        if (picture is null) return;
        var ghost = TwFadeOut.Cover(picture, bounds, window.Owner, roundCorners: true);
        ghost?.Fade(timing, edge is TwEdge to ? Offset(to, Scaled(window, options)) : Point.Empty);
    }

    private static bool IsOnScreen(Form window) => window.Visible && window.IsHandleCreated && IsWindowVisible(window.Handle);

    private static int Scaled(Control control, TwMotionOptions? options) =>
        (int)Math.Round((options?.Distance ?? _distance) * TwDpi.DeviceDpi(control) / 96f);

    /// <summary>Displacement of <paramref name="distance"/> toward <paramref name="edge"/>.</summary>
    internal static Point Offset(TwEdge edge, int distance) => edge switch
    {
        TwEdge.Left => new Point(-distance, 0),
        TwEdge.Top => new Point(0, -distance),
        TwEdge.Bottom => new Point(0, distance),
        _ => new Point(distance, 0),
    };

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private sealed class Attached
    {
        public TwEdge Edge;
        public TwMotionOptions? Enter, Exit;
        public double Opacity = 1;
    }
}
