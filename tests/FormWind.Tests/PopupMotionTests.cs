using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>TwPopover and FormWind menus grow and fade in when they open and fade out when they close.</summary>
[Collection(UiCollection.Name)]
public class PopupMotionTests
{
    private static Form Host()
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), Size = new Size(400, 300), ShowInTaskbar = false };
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

    [Fact]
    public void Popover_grows_from_a_picture_while_it_stays_invisible() => Sta.Run(() =>
    {
        using var form = Host();
        var anchor = new Button { Text = "Filtre", Location = new Point(20, 20) };
        form.Controls.Add(anchor);
        using var popover = new TwPopover { Content = new Panel { Size = new Size(160, 90), BackColor = Color.White } };
        int before = TwPopupMotion.ActiveCount;

        popover.Show(anchor);
        var dropDown = popover.DropDown!;
        Assert.True(TwPopupMotion.IsOpening(dropDown));
        Assert.True(dropDown.Opacity < 1);                                          // gerçek pencere açılış boyunca görünmez
        Assert.Equal(before + 1, TwPopupMotion.ActiveCount);                        // yerine büyüyen görüntüsü

        WaitUntil(() => !TwPopupMotion.IsOpening(dropDown));
        Assert.Equal(1.0, dropDown.Opacity);
        Assert.Equal(before, TwPopupMotion.ActiveCount);
        Assert.True(popover.IsOpen);
        popover.Close();
        Assert.False(popover.IsOpen);                                               // kapanış beklemez
        form.Close();
    });

    [Fact]
    public void Closing_while_opening_shows_the_popover_at_once_and_drops_the_picture() => Sta.Run(() =>
    {
        using var form = Host();
        var anchor = new Button { Location = new Point(20, 20) };
        form.Controls.Add(anchor);
        using var popover = new TwPopover { Content = new Panel { Size = new Size(120, 60) } };
        int before = TwPopupMotion.ActiveCount;
        popover.Show(anchor);
        popover.Close();
        Assert.False(TwPopupMotion.IsOpening(popover.DropDown!));
        Assert.Equal(before, TwPopupMotion.ActiveCount);
        Assert.Equal(1.0, popover.DropDown!.Opacity);
        form.Close();
    });

    [Fact]
    public void FormWind_menus_open_and_close_with_the_same_motion() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = Host();
        using var menu = new ContextMenuStrip();
        menu.Items.Add("Kopyala");
        menu.Items.Add("Yapıştır");
        provider.SetClasses(menu, "bg-white text-gray-900");
        Assert.IsType<TwToolStripRenderer>(menu.Renderer);

        menu.Show(form, new Point(30, 30));
        Assert.True(TwPopupMotion.IsOpening(menu));
        Assert.True(menu.Opacity < 1);
        WaitUntil(() => !TwPopupMotion.IsOpening(menu));
        Assert.Equal(1.0, menu.Opacity);

        int fading = TwFadeOut.ActiveCount;
        menu.Close();
        Assert.False(menu.Visible);
        Assert.True(TwFadeOut.ActiveCount >= fading);                               // ekran okunabildiyse son görüntüsü solar
        form.Close();
    });

    [Fact]
    public void Without_motion_popups_open_fully_visible() => Sta.Run(() =>
    {
        var previous = TwMotion.Enabled;
        TwMotion.Enabled = false;
        try
        {
            using var form = Host();
            var anchor = new Button { Location = new Point(20, 20) };
            form.Controls.Add(anchor);
            using var popover = new TwPopover { Content = new Panel { Size = new Size(120, 60) } };
            popover.Show(anchor);
            Assert.False(TwPopupMotion.IsOpening(popover.DropDown!));
            Assert.Equal(1.0, popover.DropDown!.Opacity);
            popover.Close();
            form.Close();
        }
        finally
        {
            TwMotion.Enabled = previous;
        }
    });

    [Fact]
    public void Growth_starts_from_the_point_nearest_the_anchor()
    {
        var bounds = new Rectangle(100, 100, 200, 80);
        Assert.Equal(new Point(150, 100), TwPopupMotion.Clamp(new Point(150, 60), bounds));    // çapa üstte
        Assert.Equal(new Point(300, 180), TwPopupMotion.Clamp(new Point(400, 400), bounds));   // köşe
        Assert.Equal(new Point(120, 130), TwPopupMotion.Clamp(new Point(120, 130), bounds));   // içeride
    }
}
