using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Transitions between two looks of a part of the window: a page switch, a theme change. The old look is kept as a
/// picture over the part, the change is applied underneath, and the picture fades away.
/// <code>
/// TwTransition.CrossFade(pnlPage, () => ShowPage(next));
/// TwTransition.CrossFade(this, () => provider.DarkMode = !provider.DarkMode);   // the whole form, title bar included
/// </code>
/// </summary>
public static class TwTransition
{
    /// <summary>Applies <paramref name="change"/> to <paramref name="host"/> and cross-fades from its old look (250 ms).</summary>
    public static void CrossFade(Control host, Action change) => CrossFade(host, change, TwMotion.Durations.M);

    /// <summary>
    /// Applies <paramref name="change"/> to <paramref name="host"/> (a form, a panel) and fades its old look away over the new
    /// one in <paramref name="durationMs"/>. The picture lets clicks through and covers only the visible part of the host.
    /// When animations are off, the host is not visible or no picture can be taken, the change is simply applied.
    /// </summary>
    public static void CrossFade(Control host, Action change, int durationMs)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        if (change is null) throw new ArgumentNullException(nameof(change));
        if (!TwMotion.CanAnimate(host) || durationMs <= 0 || host.Width <= 0 || host.Height <= 0)
        {
            change();
            return;
        }

        var bounds = ScreenBounds(host);
        var visible = VisibleScreenBounds(host, bounds);
        Bitmap? picture = null;
        if (!visible.IsEmpty)
        {
            try
            {
                using var whole = new Bitmap(host.Width, host.Height);
                host.DrawToBitmap(whole, new Rectangle(Point.Empty, host.Size));
                // Görünmeyen kısım (kaydırılmış, ebeveyn dışında) kesilir; resim yalnızca görünen alanı örter.
                picture = whole.Clone(new Rectangle(visible.X - bounds.X, visible.Y - bounds.Y, visible.Width, visible.Height), whole.PixelFormat);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
            {
                picture?.Dispose();
                picture = null;
            }
        }

        // Eski görüntü önce ekrana çıkar; değişiklik onun altında çizilir, yani yeni hâl bir an bile tek başına görünmez.
        bool covered = picture != null && TwFadeOut.PlayPicture(picture, visible, host.FindForm(), durationMs, roundCorners: host is Form, TwEasing.InOut);
        _lastCovered = covered;
        change();
    }

    /// <summary>Whether the last <see cref="CrossFade(Control, Action, int)"/> showed a picture (for tests).</summary>
    [ThreadStatic]
    private static bool _lastCovered;

    internal static bool LastCovered => _lastCovered;

    // Form: pencerenin tamamı (başlık çubuğu dahil, DrawToBitmap da onu çizer); alt kontrol: ebeveyn içindeki sınırları.
    private static Rectangle ScreenBounds(Control host) =>
        host.Parent is null ? host.Bounds : host.Parent.RectangleToScreen(host.Bounds);

    /// <summary>The part of <paramref name="bounds"/> not clipped away by the host's parents.</summary>
    internal static Rectangle VisibleScreenBounds(Control host, Rectangle bounds)
    {
        var visible = bounds;
        for (var parent = host.Parent; parent != null && !visible.IsEmpty; parent = parent.Parent)
            visible.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
        return visible.Width > 0 && visible.Height > 0 ? visible : Rectangle.Empty;
    }
}
