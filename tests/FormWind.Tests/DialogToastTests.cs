using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class DialogToastTests
{
    private static Form Host()
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(40, 40), ClientSize = new Size(640, 400) };
        form.Show();
        return form;
    }

    [Fact]
    public void Dialog_builds_buttons_defaults_and_cancel() => Sta.Run(() =>
    {
        using var dialog = TwDialog.Create(new TwDialogOptions
        {
            Title = "Silme onayı", Message = "Sipariş silinsin mi?", Kind = TwDialogKind.Warning, Buttons = TwDialogButtons.YesNo, Danger = true,
        });
        Assert.Equal(new[] { "Hayır", "Evet" }, dialog.Buttons.Select(b => b.Text));
        Assert.Same(dialog.Buttons[1], dialog.AcceptButton);
        Assert.Same(dialog.Buttons[0], dialog.CancelButton);
        Assert.Equal(DialogResult.Yes, dialog.Buttons[1].DialogResult);
        Assert.Equal("btn-danger", dialog.Provider.GetClasses(dialog.Buttons[1]));
        Assert.Equal("warning", dialog.IconBadge.IconName);
        Assert.Equal(AccessibleRole.Dialog, dialog.AccessibleRole);

        using var ok = TwDialog.Create(new TwDialogOptions { Message = "Kaydedildi", Kind = TwDialogKind.Success });
        Assert.Single(ok.Buttons);
        Assert.Same(ok.Buttons[0], ok.CancelButton);   // Esc de kapatır
        Assert.False(ok.TitleLabel.Visible);
    });

    [Fact]
    public void Dialog_returns_the_clicked_result() => Sta.Run(() =>
    {
        using var dialog = TwDialog.Create(new TwDialogOptions { Message = "Devam?", Buttons = TwDialogButtons.OkCancel });
        dialog.Shown += (_, _) => dialog.Buttons.First(b => b.DialogResult == DialogResult.OK).PerformClick();
        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
    });

    [Fact]
    public void Dialog_follows_owner_dark_mode_and_renders() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var owner = Host();
        provider.SetClasses(owner, "bg-gray-900");
        provider.DarkMode = true;
        using var dialog = TwDialog.Create(new TwDialogOptions { Title = "Bağlantı hatası", Message = "Sunucuya ulaşılamadı. Ağ bağlantınızı kontrol edip yeniden deneyin.", Kind = TwDialogKind.Error, Buttons = TwDialogButtons.OkCancel }, owner);
        Assert.True(dialog.Provider.DarkMode);
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(-5000, -5000);
        dialog.Show();
        Application.DoEvents();
        Assert.True(dialog.MessageLabel.Width <= 360 * dialog.DeviceDpi / 96 + 2);   // uzun mesaj kaydırılır
        dialog.Close();
    });

    [Fact]
    public void Toasts_stack_in_the_owner_corner_and_close() => Sta.Run(() =>
    {
        using var owner = Host();
        var first = TwToast.Show(owner, "Sipariş kaydedildi", TwDialogKind.Success, "Başarılı", durationMs: 0);
        var second = TwToast.Show(owner, "Bağlantı yavaş", TwDialogKind.Warning, durationMs: 0);
        Application.DoEvents();
        // Kayarak giriş bitince yerlerindedir.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while ((first.Entering || second.Entering) && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
        Assert.Equal(2, TwToast.OpenToasts(owner).Count);
        var client = owner.RectangleToScreen(owner.ClientRectangle);
        Assert.True(second.Bottom <= client.Bottom && second.Right <= client.Right);
        Assert.True(first.Bottom <= second.Top);                     // eskisi yukarıda
        Assert.False(owner.ContainsFocus && first.ContainsFocus);    // odak çalmaz
        Assert.Equal(AccessibleRole.Alert, first.AccessibleRole);
        Assert.Equal("Başarılı: Sipariş kaydedildi", first.AccessibleName);

        first.Close();
        Application.DoEvents();
        Assert.Single(TwToast.OpenToasts(owner));
        Assert.Equal(client.Bottom - (int)Math.Round(16 * owner.DeviceDpi / 96f), second.Bottom);   // aşağı kaydı
        second.Close();
    });

    [Fact]
    public void Toasts_release_their_owner() => Sta.Run(() =>
    {
        int before = TwToast.TrackedOwners;
        using (var owner = Host())
        {
            TwToast.Show(owner, "Bir", durationMs: 0).Close();
            Application.DoEvents();
            Assert.Equal(before, TwToast.TrackedOwners);              // son bildirim kapandı

            TwToast.Show(owner, "İki", durationMs: 0);
            Assert.Equal(before + 1, TwToast.TrackedOwners);
            owner.Close();                                             // açık bildirimle kapanan form
        }
        Application.DoEvents();
        Assert.Equal(before, TwToast.TrackedOwners);
    });
}
