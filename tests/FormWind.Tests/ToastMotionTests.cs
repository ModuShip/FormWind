using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>TwToast.Position, MaxVisible, the slide in, the success check mark and Subtitle.</summary>
[Collection(UiCollection.Name)]
public class ToastMotionTests
{
    private static Form Host()
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), Size = new Size(600, 400), ShowInTaskbar = false };
        form.Show();
        return form;
    }

    private static void WaitUntil(Func<bool> condition, int milliseconds = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    // Statik ayarları değiştiren testler eski değeri geri verir.
    private static void WithToastSettings(TwToastPosition position, int maxVisible, Action body)
    {
        var (oldPosition, oldMax) = (TwToast.Position, TwToast.MaxVisible);
        TwToast.Position = position;
        TwToast.MaxVisible = maxVisible;
        try
        {
            body();
        }
        finally
        {
            TwToast.Position = oldPosition;
            TwToast.MaxVisible = oldMax;
        }
    }

    [Fact]
    public void Top_right_stacks_downwards_with_the_newest_on_top() => Sta.Run(() => WithToastSettings(TwToastPosition.TopRight, 0, () =>
    {
        using var owner = Host();
        var first = TwToast.Show(owner, "Bir", durationMs: 0);
        var second = TwToast.Show(owner, "İki", durationMs: 0);
        WaitUntil(() => !first.Entering && !second.Entering);
        var client = owner.RectangleToScreen(owner.ClientRectangle);
        int margin = (int)Math.Round(16 * owner.DeviceDpi / 96f);
        Assert.Equal(client.Top + margin, second.Top);                              // en yenisi en üstte
        Assert.True(first.Top >= second.Bottom);
        Assert.Equal(client.Right - margin, second.Right);
        first.Close();
        second.Close();
    }));

    [Fact]
    public void Max_visible_one_replaces_the_previous_notification() => Sta.Run(() => WithToastSettings(TwToastPosition.BottomRight, 1, () =>
    {
        using var owner = Host();
        var first = TwToast.Show(owner, "Bir", durationMs: 0);
        WaitUntil(() => !first.Entering);
        var place = first.TargetLocation;
        var second = TwToast.Show(owner, "İki", durationMs: 0);
        Assert.Single(TwToast.OpenToasts(owner));
        Assert.Same(second, TwToast.OpenToasts(owner)[0]);
        Assert.Equal(place.Y + first.Height, second.TargetLocation.Y + second.Height);  // eskisinin yerine
        WaitUntil(() => first.IsDisposed);
        Assert.True(first.IsDisposed);                                              // eskisi solup kapandı
        second.Close();
    }));

    [Fact]
    public void A_new_notification_slides_in_from_its_edge() => Sta.Run(() =>
    {
        using var owner = Host();
        var toast = TwToast.Show(owner, "Kaydedildi", durationMs: 0);
        Application.DoEvents();
        Assert.True(toast.Entering);
        Assert.True(toast.Top > toast.TargetLocation.Y);                            // alttaki aşağıdan gelir
        WaitUntil(() => !toast.Entering);
        Assert.Equal(toast.TargetLocation, toast.Location);
        Assert.Equal(1.0, toast.Opacity);
        toast.Close();
    });

    [Fact]
    public void Success_check_draws_itself_after_the_fade() => Sta.Run(() =>
    {
        using var owner = Host();
        var toast = TwToast.Show(owner, "Kaydedildi", TwDialogKind.Success, durationMs: 0);
        Application.DoEvents();
        Assert.True(toast.CheckProgress < 1f);
        WaitUntil(() => toast.CheckProgress >= 1f);
        Assert.Equal(1f, toast.CheckProgress);
        Assert.Equal(1f, TwToast.Show(owner, "Bilgi", durationMs: 0).CheckProgress); // ikonlu türlerde yok
        foreach (var open in TwToast.OpenToasts(owner).ToArray()) open.Close();
    });

    [Fact]
    public void Subtitle_adds_a_muted_line_and_grows_the_notification() => Sta.Run(() =>
    {
        using var owner = Host();
        var toast = TwToast.Show(owner, "Duruş kaydı açıldı", TwDialogKind.Success, "Kaydedildi", durationMs: 0);
        Application.DoEvents();
        int height = toast.Height;
        Assert.False(toast.SubtitleLabel.Visible);

        toast.Subtitle = "DR-24817 · Hat 2";
        Application.DoEvents();
        Assert.True(toast.SubtitleLabel.Visible);
        Assert.True(toast.Height > height);
        Assert.True(toast.SubtitleLabel.Top > toast.MessageLabel.Top);
        Assert.Equal("Kaydedildi: Duruş kaydı açıldı (DR-24817 · Hat 2)", toast.AccessibleName);
        var client = owner.RectangleToScreen(owner.ClientRectangle);
        Assert.Equal(client.Bottom - (int)Math.Round(16 * owner.DeviceDpi / 96f), toast.TargetLocation.Y + toast.Height); // yeniden yerleşti
        toast.Close();
    });

    [Fact]
    public void Without_motion_the_notification_is_placed_and_drawn_at_once() => Sta.Run(() =>
    {
        var previous = TwMotion.Enabled;
        TwMotion.Enabled = false;
        try
        {
            using var owner = Host();
            var toast = TwToast.Show(owner, "Kaydedildi", TwDialogKind.Success, durationMs: 0);
            Application.DoEvents();
            Assert.False(toast.Entering);
            Assert.Equal(toast.TargetLocation, toast.Location);
            Assert.Equal(1f, toast.CheckProgress);
            toast.Close();
        }
        finally
        {
            TwMotion.Enabled = previous;
        }
    });
}
