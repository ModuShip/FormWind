using System;
using System.Drawing;
using System.Runtime.CompilerServices;
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
    /// <summary>How far (96 DPI px) a window slides.</summary>
    internal const int Distance = 24;

    private static readonly ConditionalWeakTable<Form, Attached> Attachments = new ConditionalWeakTable<Form, Attached>();

    /// <summary>
    /// Plays the entrance and exit of <paramref name="window"/> from now on: it slides in from <paramref name="edge"/> each
    /// time it is shown and fades out toward the same edge each time it closes. Call it before the window is shown; a second
    /// call changes the edge.
    /// </summary>
    public static void Attach(Form window, TwEdge edge)
    {
        if (window is null) throw new ArgumentNullException(nameof(window));
        if (Attachments.TryGetValue(window, out var attached))
        {
            attached.Edge = edge;
            return;
        }
        attached = new Attached { Edge = edge };
        Attachments.Add(window, attached);
        // Gösterilmeden önce saydam kurulur; aksi halde yerinde bir kare görünüp sonra kayar.
        window.Load += (_, _) =>
        {
            if (!TwMotion.Enabled) return;
            attached.Opacity = window.Opacity;
            window.Opacity = 0;
        };
        window.Shown += (_, _) => SlideIn(window, attached.Edge, TwMotion.Durations.M, attached.Opacity);
        window.FormClosing += (_, e) =>
        {
            if (!e.Cancel) FadeOut(window, attached.Edge);
        };
    }

    /// <summary>Slides a shown window in from <paramref name="edge"/> while it fades in (250 ms).</summary>
    public static void SlideIn(Form window, TwEdge edge) => SlideIn(window, edge, TwMotion.Durations.M);

    /// <summary>Slides a shown window in from <paramref name="edge"/> while it fades in, in <paramref name="durationMs"/>.</summary>
    public static void SlideIn(Form window, TwEdge edge, int durationMs) =>
        SlideIn(window, edge, durationMs, window is { Opacity: > 0 } ? window.Opacity : 1);

    private static void SlideIn(Form window, TwEdge edge, int durationMs, double opacity)
    {
        if (window is null) throw new ArgumentNullException(nameof(window));
        var target = window.Location;
        if (!TwMotion.CanAnimate(window) || durationMs <= 0)
        {
            window.Opacity = opacity;
            return;
        }
        var offset = Offset(edge, Scaled(window));
        var start = new Point(target.X + offset.X, target.Y + offset.Y);
        window.Opacity = 0;
        window.Location = start;
        TwAnimator.Animate(window, "tw-window-slide", 0, 1, durationMs, p =>
        {
            if (window.IsDisposed) return;
            window.Location = new Point(start.X + (int)Math.Round((target.X - start.X) * p), start.Y + (int)Math.Round((target.Y - start.Y) * p));
            window.Opacity = opacity * Math.Max(0, Math.Min(1, p));
        }, TwEasing.Out, 0, () =>
        {
            if (window.IsDisposed) return;
            window.Location = target;
            window.Opacity = opacity;
        });
    }

    /// <summary>
    /// Fades the last picture of a closing window out while it slides toward <paramref name="edge"/> (180 ms). Call it just
    /// before the window closes (in <c>FormClosing</c>); the window itself closes at once. With <c>null</c> it only fades.
    /// </summary>
    public static void FadeOut(Form window, TwEdge? edge) => FadeOut(window, edge, TwMotion.Durations.S);

    /// <summary>Fades the last picture of a closing window out (sliding toward <paramref name="edge"/>) in <paramref name="durationMs"/>.</summary>
    public static void FadeOut(Form window, TwEdge? edge, int durationMs)
    {
        if (window is null) throw new ArgumentNullException(nameof(window));
        if (!window.Visible || !TwMotion.CanAnimate(window) || durationMs <= 0) return;
        TwAnimator.Cancel(window, "tw-window-slide");
        var bounds = TwCapture.VisibleFormBounds(window);
        var picture = TwCapture.Take(window, bounds);
        if (picture is null) return;
        var ghost = TwFadeOut.Cover(picture, bounds, window.Owner, roundCorners: true);
        ghost?.Fade(durationMs, TwEasing.In, edge is TwEdge to ? Offset(to, Scaled(window)) : Point.Empty);
    }

    private static int Scaled(Control control) => (int)Math.Round(Distance * TwDpi.DeviceDpi(control) / 96f);

    /// <summary>Displacement of <paramref name="distance"/> toward <paramref name="edge"/>.</summary>
    internal static Point Offset(TwEdge edge, int distance) => edge switch
    {
        TwEdge.Left => new Point(-distance, 0),
        TwEdge.Top => new Point(0, -distance),
        TwEdge.Bottom => new Point(0, distance),
        _ => new Point(distance, 0),
    };

    private sealed class Attached
    {
        public TwEdge Edge;
        public double Opacity = 1;
    }
}
