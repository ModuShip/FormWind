using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

public sealed class LayoutTests : IDisposable
{
    private readonly TailwindProvider _provider = new TailwindProvider();

    public void Dispose() => _provider.Dispose();

    // Çocuklar class'sız Panel'ler; Size değerleri "doğal" boyutlarıdır.
    private TwPanel Host(string classes, int width, int height, params (int Width, int Height, string Classes)[] items)
    {
        var host = new TwPanel { Size = new Size(width, height) };
        foreach (var (itemWidth, itemHeight, itemClasses) in items)
        {
            var child = new Panel { Size = new Size(itemWidth, itemHeight) };
            host.Controls.Add(child);
            if (itemClasses.Length > 0) _provider.SetClasses(child, itemClasses);
        }
        _provider.SetClasses(host, classes);
        host.PerformLayout();
        return host;
    }

    private static Rectangle[] Bounds(Control host) => host.Controls.Cast<Control>().Select(c => c.Bounds).ToArray();

    [Fact]
    public void Justify_between_spreads_items_and_stretch_fills_height()
    {
        using var host = Host("flex justify-between", 300, 100, (50, 20, ""), (50, 20, ""), (50, 20, ""));
        var bounds = Bounds(host);

        Assert.Equal(new[] { 0, 125, 250 }, bounds.Select(b => b.X));
        Assert.All(bounds, b => Assert.Equal(100, b.Height));
    }

    [Fact]
    public void Items_center_and_gap()
    {
        using var host = Host("flex items-center gap-2", 300, 100, (50, 20, ""), (50, 20, ""));
        var bounds = Bounds(host);

        Assert.Equal(new[] { 0, 58 }, bounds.Select(b => b.X));
        Assert.All(bounds, b => Assert.Equal(new Point(b.X, 40), b.Location));
        Assert.All(bounds, b => Assert.Equal(20, b.Height));
    }

    [Fact]
    public void Flex_1_items_share_remaining_space()
    {
        using var host = Host("flex gap-x-[10px]", 300, 50, (40, 20, "flex-1"), (90, 20, "flex-1"));
        var bounds = Bounds(host);

        Assert.Equal(new[] { 145, 145 }, bounds.Select(b => b.Width));
        Assert.Equal(155, bounds[1].X);
    }

    [Fact]
    public void Grow_takes_free_space_from_natural_size()
    {
        using var host = Host("flex", 300, 50, (50, 20, "grow"), (50, 20, ""));
        var bounds = Bounds(host);

        Assert.Equal(250, bounds[0].Width);
        Assert.Equal(250, bounds[1].X);
    }

    [Fact]
    public void Max_width_violation_redistributes_to_other_items()
    {
        using var host = Host("flex", 300, 50, (10, 20, "flex-1 max-w-20"), (10, 20, "flex-1"));
        var bounds = Bounds(host);

        Assert.Equal(new[] { 80, 220 }, bounds.Select(b => b.Width));
    }

    [Fact]
    public void Items_with_basis_shrink_proportionally()
    {
        using var host = Host("flex", 300, 50, (10, 20, "basis-50"), (10, 20, "basis-50"));

        Assert.Equal(new[] { 150, 150 }, Bounds(host).Select(b => b.Width));
    }

    [Fact]
    public void Auto_sized_items_do_not_shrink_below_natural_size()
    {
        using var host = Host("flex", 100, 50, (80, 20, ""), (80, 20, ""));

        Assert.Equal(new[] { 80, 80 }, Bounds(host).Select(b => b.Width));
    }

    [Fact]
    public void Column_stacks_with_gap_inside_padding_and_stretches_width()
    {
        using var host = Host("flex flex-col gap-4 p-4", 200, 300, (50, 30, ""), (60, 40, ""));
        var bounds = Bounds(host);

        Assert.Equal(new Rectangle(16, 16, 168, 30), bounds[0]);
        Assert.Equal(new Rectangle(16, 62, 168, 40), bounds[1]);
    }

    [Fact]
    public void Wrap_breaks_lines_and_uses_row_gap()
    {
        using var host = Host("flex flex-wrap items-start gap-[10px]", 250, 200,
            (100, 20, ""), (100, 20, ""), (100, 20, ""), (100, 20, ""));
        var bounds = Bounds(host);

        Assert.Equal(new[] { new Point(0, 0), new Point(110, 0), new Point(0, 30), new Point(110, 30) }, bounds.Select(b => b.Location));
    }

    [Fact]
    public void Fractional_widths_resolve_against_container()
    {
        using var host = Host("flex", 400, 50, (10, 20, "w-1/2"), (10, 20, "w-1/4"));

        Assert.Equal(new[] { 200, 100 }, Bounds(host).Select(b => b.Width));
    }

    [Fact]
    public void Item_margin_offsets_and_reduces_stretch()
    {
        using var host = Host("flex", 300, 100, (50, 20, "m-2"));

        Assert.Equal(new Rectangle(8, 8, 50, 84), Bounds(host)[0]);
    }

    [Fact]
    public void Hidden_items_are_skipped()
    {
        using var host = Host("flex", 300, 50, (50, 20, ""), (50, 20, ""), (50, 20, ""));
        host.Controls[1].Visible = false;
        host.PerformLayout();

        Assert.Equal(50, host.Controls[2].Left);
    }

    [Fact]
    public void Self_alignment_overrides_items()
    {
        using var host = Host("flex items-start", 300, 100, (50, 20, "self-end"), (50, 20, ""));
        var bounds = Bounds(host);

        Assert.Equal(80, bounds[0].Y);
        Assert.Equal(0, bounds[1].Y);
    }

    [Fact]
    public void Grid_places_items_by_column_span()
    {
        using var host = Host("grid grid-cols-3 gap-4", 300, 200, (10, 40, "col-span-2"), (10, 20, ""), (10, 30, ""));
        var bounds = Bounds(host);

        Assert.Equal(new Rectangle(0, 0, 195, 40), bounds[0]);
        Assert.Equal(new Rectangle(211, 0, 89, 40), bounds[1]);
        Assert.Equal(new Rectangle(0, 56, 89, 30), bounds[2]);
    }

    // CSS auto-placement: row-span'li öğenin kapladığı hücreler atlanır; eksik yükseklik kapladığı satırlara eşit dağıtılır.
    [Fact]
    public void Grid_row_span_reserves_cells_and_grows_the_spanned_rows()
    {
        using var host = Host("grid grid-cols-3 gap-4", 300, 200,
            (10, 60, "row-span-2"), (10, 20, ""), (10, 20, ""), (10, 20, ""), (10, 20, ""));
        var bounds = Bounds(host);

        Assert.Equal(new Rectangle(0, 0, 89, 60), bounds[0]);        // 22 + 16 + 22
        Assert.Equal(new Rectangle(105, 0, 90, 22), bounds[1]);
        Assert.Equal(new Rectangle(105, 38, 90, 22), bounds[3]);     // ikinci satır, ilk kolon dolu
        Assert.Equal(new Rectangle(211, 38, 89, 22), bounds[4]);
    }

    [Fact]
    public void Grid_rows_split_the_container_height_equally()
    {
        using var host = Host("grid grid-cols-2 grid-rows-2 gap-4", 200, 100, (10, 10, ""), (10, 10, ""), (10, 10, ""), (10, 10, ""));
        var bounds = Bounds(host);

        Assert.Equal(new Rectangle(0, 0, 92, 42), bounds[0]);
        Assert.Equal(new Rectangle(108, 58, 92, 42), bounds[3]);
    }

    [Fact]
    public void Row_span_full_covers_the_explicit_rows()
    {
        using var host = Host("grid grid-cols-2 grid-rows-3 gap-2", 200, 100, (10, 10, "row-span-full"), (10, 10, ""), (10, 10, ""), (10, 10, ""));
        var bounds = Bounds(host);

        Assert.Equal(new Rectangle(0, 0, 96, 100), bounds[0]);
        Assert.Equal(104, bounds[3].X);
        Assert.Equal(72, bounds[3].Y);  // üçüncü satır: 2 × (28 + 8)
    }

    [Fact]
    public void Dark_variant_can_switch_direction()
    {
        using var host = Host("flex dark:flex-col", 300, 300, (50, 20, ""), (50, 20, ""));
        Assert.Equal(0, host.Controls[1].Top);

        _provider.DarkMode = true;

        Assert.Equal(0, host.Controls[1].Left);
        Assert.Equal(20, host.Controls[1].Top);
    }

    [Fact]
    public void Flex_host_measures_its_content()
    {
        using var host = Host("flex gap-2 p-2", 500, 500, (50, 20, ""), (30, 24, ""));

        Assert.Equal(new Size(50 + 30 + 8 + 16, 24 + 16), host.GetPreferredSize(Size.Empty));
    }

    [Fact]
    public void Natural_size_survives_relayout()
    {
        using var host = Host("flex", 300, 50, (50, 20, "grow"));
        host.Width = 200;
        host.PerformLayout();

        Assert.Equal(200, host.Controls[0].Width);

        host.Width = 40;
        host.PerformLayout();

        // Doğal boyut (50) motorun 300/200 atamalarından etkilenmez; grow öğesi doğal boyutunun altına inmez.
        Assert.Equal(50, host.Controls[0].Width);
    }

    [Fact]
    public void AutoSize_items_get_their_preferred_size()
    {
        using var host = new TwPanel { Size = new Size(300, 300) };
        var label = new Label { AutoSize = true, Text = "Müşteri bilgileri" };
        host.Controls.Add(label);
        _provider.SetClasses(label, "text-2xl font-bold");
        _provider.SetClasses(host, "flex flex-col items-start");
        host.PerformLayout();

        Assert.Equal(label.GetPreferredSize(Size.Empty), label.Size);
    }

    [Fact]
    public void TwIcon_font_change_relayouts_its_flex_parent()
    {
        using var host = new TwPanel { Size = new Size(300, 100) };
        var icon = new TwIcon { IconName = "user" };
        host.Controls.Add(icon);
        _provider.SetClasses(host, "flex items-start");
        host.PerformLayout();
        var before = icon.Height;

        _provider.SetClasses(icon, "text-2xl");

        Assert.Equal(icon.GetPreferredSize(Size.Empty), icon.Size);
        Assert.True(icon.Height > before);
    }

    [Fact]
    public void Growing_child_of_an_autosize_host_resizes_the_host_in_its_parent()
    {
        using var card = new TwPanel { Size = new Size(300, 300) };
        var header = new TwPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var icon = new TwIcon { IconName = "user" };
        header.Controls.Add(icon);
        card.Controls.Add(header);
        _provider.SetClasses(card, "flex flex-col");
        _provider.SetClasses(header, "flex items-center");
        card.PerformLayout();

        _provider.SetClasses(icon, "text-3xl");

        Assert.True(header.Height >= icon.Height, $"header {header.Height}, icon {icon.Height}");
        Assert.True(icon.Top >= 0, $"icon top {icon.Top}");
        // text-3xl = 30 px: ikon kutusu kare ve font boyutunun piksel karşılığı
        Assert.Equal(new Size(30, 30), icon.Size);
    }

    [Fact]
    public void Children_are_laid_out_inside_the_border()
    {
        using var host = Host("flex items-stretch border-b-2 border-l", 300, 40, (50, 10, ""));

        Assert.Equal(new Rectangle(1, 0, 50, 38), host.Controls[0].Bounds);
    }

    [Fact]
    public void Autoscroll_column_uses_the_visible_width()
    {
        using var host = new TwPanel { Size = new Size(300, 200), AutoScroll = true };
        for (int i = 0; i < 10; i++) host.Controls.Add(new Panel { Size = new Size(10, 50) });
        _provider.SetClasses(host, "flex flex-col");
        host.PerformLayout();

        Assert.All(host.Controls.Cast<Control>(), c => Assert.Equal(host.ClientSize.Width, c.Width));
    }

    [Fact]
    public void Flex_on_a_non_host_control_is_ignored()
    {
        using var panel = new Panel { Size = new Size(300, 100) };
        var child = new Panel { Location = new Point(40, 30), Size = new Size(50, 20) };
        panel.Controls.Add(child);

        _provider.SetClasses(panel, "flex justify-end");
        panel.PerformLayout();

        Assert.Equal(new Point(40, 30), child.Location);
    }
}

// Form açan yerleşim testleri; UI koleksiyonunda sırayla çalışır.
[Collection(UiCollection.Name)]
public class LayoutScalingTests
{
    // Konteyner henüz küçükken motor bir öğeyi geçici olarak daraltır; ardından gelen AutoScale bu daraltılmış boyutu
    // ölçekleyip doğal boyut sanıyordu (ortalanan bir kart kalıcı olarak kısa kalıyordu).
    [Fact]
    public void AutoScale_keeps_the_natural_size_of_a_temporarily_squeezed_item() => Sta.Run(() =>
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        using var tw = new TailwindProvider();
        form.SuspendLayout();
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF(80F, 80F);   // 96 DPI'da 1,2 kat
        form.ClientSize = new Size(440, 460);
        var page = new TwPanel { Dock = DockStyle.Fill };
        var card = new TwPanel { Size = new Size(380, 400) };
        card.Controls.Add(new Label { Text = "Hoş geldiniz", AutoSize = true });
        page.Controls.Add(card);
        form.Controls.Add(page);
        tw.SetClasses(page, "flex items-center justify-center");
        tw.SetClasses(card, "flex flex-col gap-3 p-8");
        form.ResumeLayout();
        form.Show();
        Application.DoEvents();

        Assert.Equal(new Size(456, 480), card.Size);
        Assert.Equal((page.ClientSize.Height - 480) / 2, card.Top);   // dikeyde ortalı
    });
}
