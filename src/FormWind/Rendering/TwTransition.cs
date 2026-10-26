using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Transitions between two looks of a part of the window: a page switch, a theme change. The old look is kept as a
/// picture over the part, the change is applied underneath and painted, then the picture fades away.
/// <code>
/// TwTransition.CrossFade(pnlPage, () => ShowPage(next));
/// TwTransition.CrossFade(this, () => provider.DarkMode = !provider.DarkMode);   // the whole form, title bar included
/// </code>
/// <para>
/// The picture is what is on screen once the pending paints are done (overlapping controls, a custom title bar and
/// scrolled panels come out as shown). When the part is covered by another application's window or is off screen, the
/// window's content is asked from the desktop window manager instead, and <see cref="Control.DrawToBitmap"/> is the last
/// resort, which redraws the controls and can get overlapping controls wrong. Anything above the part on screen, such as
/// a notification of the same window, is in the picture too and fades with it.
/// </para>
/// </summary>
public static class TwTransition
{
    /// <summary>Applies <paramref name="change"/> to <paramref name="host"/> and cross-fades from its old look (250 ms).</summary>
    public static void CrossFade(Control host, Action change) => CrossFade(host, change, TwMotion.Durations.M);

    /// <summary>
    /// Applies <paramref name="change"/> to <paramref name="host"/> (a form, a panel) and fades its old look away over the new
    /// one in <paramref name="durationMs"/>. The new look is painted before the fade starts, so the whole fade is seen
    /// however long the change takes. The picture lets clicks through and covers only the visible part of the host.
    /// When animations are off, the host is not visible or no picture can be taken, the change is simply applied.
    /// </summary>
    public static void CrossFade(Control host, Action change, int durationMs)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        if (change is null) throw new ArgumentNullException(nameof(change));
        if (!TwMotion.CanAnimate(host) || durationMs <= 0 || host.Width <= 0 || host.Height <= 0)
        {
            _lastCovered = false;
            change();
            return;
        }

        var bounds = ScreenBounds(host);
        var visible = VisibleScreenBounds(host, bounds);
        var picture = visible.IsEmpty ? null : TwCapture.Take(host, visible);

        // Eski görüntü önce ekrana çıkar; değişiklik onun altında uygulanıp boyanır, solma ancak ondan sonra başlar.
        var cover = picture is null ? null : TwFadeOut.Cover(picture, visible, host.FindForm(), roundCorners: host is Form);
        _lastCovered = cover != null;
        try
        {
            change();
        }
        finally
        {
            if (cover != null)
            {
                TwCapture.PaintNow(host);
                cover.Fade(durationMs, TwEasing.InOut);
            }
        }
    }

    /// <summary>Whether the last <see cref="CrossFade(Control, Action, int)"/> showed a picture (for tests).</summary>
    [ThreadStatic]
    private static bool _lastCovered;

    internal static bool LastCovered => _lastCovered;

    // Form: pencerenin görünen tamamı (başlık çubuğu dahil, görünmez kenarlıklar hariç); alt kontrol: ebeveyn içindeki sınırları.
    private static Rectangle ScreenBounds(Control host) =>
        host is Form form && host.Parent is null ? TwCapture.VisibleFormBounds(form)
        : host.Parent is null ? host.Bounds : host.Parent.RectangleToScreen(host.Bounds);

    /// <summary>The part of <paramref name="bounds"/> not clipped away by the host's parents.</summary>
    internal static Rectangle VisibleScreenBounds(Control host, Rectangle bounds)
    {
        var visible = bounds;
        for (var parent = host.Parent; parent != null && !visible.IsEmpty; parent = parent.Parent)
            visible.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
        return visible.Width > 0 && visible.Height > 0 ? visible : Rectangle.Empty;
    }
}
