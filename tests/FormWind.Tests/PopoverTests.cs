using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>TwPopover: yerleşim (yan, hizalama, karşı tarafa geçme, ekrana sığdırma) ve açma/kapama.</summary>
[Collection(UiCollection.Name)]
public class PopoverTests
{
    private static readonly Rectangle Area = new Rectangle(0, 0, 1000, 800);
    private static readonly Size PopSize = new Size(200, 100);

    private static Rectangle Place(Rectangle anchor, TwPopoverPlacement placement, bool rtl = false) =>
        TwPopover.Place(anchor, PopSize, Area, placement, 4, rtl);

    [Theory]
    [InlineData(TwPopoverPlacement.BottomStart, 100, 334)]
    [InlineData(TwPopoverPlacement.Bottom, 150, 334)]
    [InlineData(TwPopoverPlacement.BottomEnd, 200, 334)]
    [InlineData(TwPopoverPlacement.TopStart, 100, 196)]
    [InlineData(TwPopoverPlacement.Top, 150, 196)]
    [InlineData(TwPopoverPlacement.TopEnd, 200, 196)]
    public void Vertical_placements_align_to_the_anchor(TwPopoverPlacement placement, int x, int y)
    {
        // 300×30 çapa, iki tarafta da yer var; aralık 4 px.
        var anchor = new Rectangle(100, 300, 300, 30);
        Assert.Equal(new Rectangle(new Point(x, y), PopSize), Place(anchor, placement));
    }

    [Fact]
    public void Side_placements_align_to_the_anchor_edges()
    {
        var anchor = new Rectangle(400, 300, 100, 200);
        Assert.Equal(new Rectangle(504, 300, 200, 100), Place(anchor, TwPopoverPlacement.RightStart));
        Assert.Equal(new Rectangle(504, 350, 200, 100), Place(anchor, TwPopoverPlacement.Right));
        Assert.Equal(new Rectangle(504, 400, 200, 100), Place(anchor, TwPopoverPlacement.RightEnd));
        Assert.Equal(new Rectangle(196, 300, 200, 100), Place(anchor, TwPopoverPlacement.LeftStart));
    }

    [Fact]
    public void Flips_to_the_opposite_side_when_only_that_fits()
    {
        // Ekranın altındaki alan: aşağı sığmaz, yukarı açılır.
        Assert.Equal(new Rectangle(100, 646, 200, 100), Place(new Rectangle(100, 750, 300, 30), TwPopoverPlacement.BottomStart));
        // Ekranın üstündeki alan: yukarı sığmaz, aşağı açılır.
        Assert.Equal(new Rectangle(100, 54, 200, 100), Place(new Rectangle(100, 20, 300, 30), TwPopoverPlacement.TopStart));
        // Sağ kenardaki düğme: sağa sığmaz, sola açılır.
        Assert.Equal(new Rectangle(746, 400, 200, 100), Place(new Rectangle(950, 400, 40, 30), TwPopoverPlacement.RightStart));
    }

    [Fact]
    public void Stays_on_its_side_when_neither_side_fits_better()
    {
        // 150 px yüksek alanda iki taraf da sığmaz: daha geniş olan taraf (alt) seçilir ve ekrana kaydırılır.
        var area = new Rectangle(0, 0, 1000, 150);
        var bounds = TwPopover.Place(new Rectangle(100, 40, 300, 30), PopSize, area, TwPopoverPlacement.BottomStart, 4, false);
        Assert.Equal(new Rectangle(100, 50, 200, 100), bounds);
    }

    [Fact]
    public void Is_kept_inside_the_working_area()
    {
        // Sağ kenara yakın BottomStart: sağdan taşmaz.
        Assert.Equal(new Rectangle(800, 134, 200, 100), Place(new Rectangle(900, 100, 50, 30), TwPopoverPlacement.BottomStart));
        // Sol kenardaki ortalanmış popover: soldan taşmaz.
        Assert.Equal(new Rectangle(0, 134, 200, 100), Place(new Rectangle(0, 100, 40, 30), TwPopoverPlacement.Bottom));
    }

    [Fact]
    public void Right_to_left_mirrors_start_and_end_of_vertical_placements()
    {
        var anchor = new Rectangle(100, 100, 300, 30);
        Assert.Equal(200, Place(anchor, TwPopoverPlacement.BottomStart, rtl: true).X);
        Assert.Equal(100, Place(anchor, TwPopoverPlacement.BottomEnd, rtl: true).X);
        Assert.Equal(150, Place(anchor, TwPopoverPlacement.Bottom, rtl: true).X);
        // Yan yerleşimler aynalanmaz: sağ yine sağdır.
        Assert.Equal(404, Place(anchor, TwPopoverPlacement.RightStart, rtl: true).X);
    }

    [Fact]
    public void Shows_closes_and_toggles_the_content() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
        var button = new Button { Location = new Point(10, 10), Size = new Size(100, 30) };
        form.Controls.Add(button);
        form.Show();

        var content = new TextBox { Size = new Size(160, 24) };
        var popover = new TwPopover { Content = content };
        int opened = 0, closed = 0;
        var reason = (ToolStripDropDownCloseReason)(-1);
        popover.Opened += (_, _) => opened++;
        popover.Closed += (_, e) => { closed++; reason = e.CloseReason; };

        popover.Show(button);
        Assert.True(popover.IsOpen);
        Assert.Same(button, popover.Anchor);
        Assert.Equal(1, opened);
        Assert.Equal(content.Size, popover.DropDown!.Items[0].Size);

        popover.Toggle(button);
        Assert.False(popover.IsOpen);
        Assert.Equal(1, closed);
        Assert.Equal(ToolStripDropDownCloseReason.CloseCalled, reason);

        popover.Toggle(button);
        Assert.True(popover.IsOpen);
        popover.Close();
        Assert.False(popover.IsOpen);

        // Opening iptal edilirse açılmaz.
        popover.Opening += (_, e) => e.Cancel = true;
        popover.Show(button);
        Assert.False(popover.IsOpen);
        Assert.Equal(2, opened);

        popover.Dispose();
        Assert.True(content.IsDisposed);
        form.Close();
    });

    [Fact]
    public void Replaced_content_is_not_disposed() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
        var button = new Button { Size = new Size(100, 30) };
        form.Controls.Add(button);
        form.Show();

        using var first = new Label { Size = new Size(120, 40) };
        var second = new Label { Size = new Size(80, 60) };
        using var popover = new TwPopover { Content = first };
        popover.Show(button);
        popover.Content = second;
        Assert.False(popover.IsOpen);
        Assert.False(first.IsDisposed);
        Assert.Null(first.Parent);

        popover.Show(button);
        Assert.Equal(second.Size, popover.DropDown!.Items[0].Size);
        Assert.Single(popover.DropDown.Items);
        popover.Close();
        form.Close();
    });

    [Fact]
    public void Focuses_the_first_focusable_child_of_a_panel() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
        var button = new Button { Size = new Size(100, 30) };
        form.Controls.Add(button);
        form.Show();

        // Panel seçilemez, etiket odak almaz; ToolStripDropDown içinde Select değil Focus gerekir.
        var panel = new TwPanel { Size = new Size(180, 90) };
        var label = new Label { Text = "B · 3/5", Location = new Point(10, 10), AutoSize = true };
        var text = new TextBox { Location = new Point(10, 40), Width = 120 };
        panel.Controls.Add(label);
        panel.Controls.Add(text);
        using var popover = new TwPopover { Content = panel };
        popover.Show(button);
        Assert.True(text.Focused);

        popover.Close();
        Assert.True(button.Focused);
        form.Close();
    });

    [Fact]
    public void Show_without_content_throws()
    {
        using var popover = new TwPopover();
        using var anchor = new Button();
        Assert.Throws<InvalidOperationException>(() => popover.Show(anchor));
    }
}
