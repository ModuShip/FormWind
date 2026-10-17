using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class RtlTests
{
    private readonly TwTheme _theme = new TwTheme();

    [Fact]
    public void Rtl_and_ltr_variants_follow_the_direction()
    {
        var parsed = _theme.Parse("pl-4 rtl:pl-0 rtl:pr-4 ltr:text-left rtl:text-right");
        Assert.Empty(parsed.Unknown);

        var rtl = parsed.Resolve(TwState.Rtl);
        Assert.Equal(0, rtl.Padding.Left);
        Assert.Equal(16, rtl.Padding.Right);                     // piksel: 4 birim × 4 px
        Assert.Equal(TwTextAlign.Right, rtl.TextAlign);

        var ltr = parsed.Resolve(TwState.Ltr);
        Assert.Equal(16, ltr.Padding.Left);
        Assert.Equal(TwTextAlign.Left, ltr.TextAlign);
    }

    [Fact]
    public void Provider_reapplies_when_the_inherited_direction_changes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var parent = new Panel();
        var label = new Label { AutoSize = false, Size = new Size(100, 20) };
        parent.Controls.Add(label);
        provider.SetClasses(label, "ltr:bg-white rtl:bg-black");
        Assert.Equal(Color.White.ToArgb(), label.BackColor.ToArgb());

        parent.RightToLeft = RightToLeft.Yes;                    // kalıtılan değer
        Assert.Equal(Color.Black.ToArgb(), label.BackColor.ToArgb());
    });

    [Theory]
    [InlineData(false, false, 0)]     // soldan sağa: ilk öğe solda
    [InlineData(true, false, 150)]    // sağdan sola: ilk öğe sağda
    [InlineData(true, true, 0)]       // RightToLeftLayout: Windows aynalar, FormWind koordinatları değiştirmez
    public void Flex_row_is_mirrored_right_to_left(bool rtl, bool windowsLayout, int firstX) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No, RightToLeftLayout = windowsLayout };
        var panel = new TwPanel { Bounds = new Rectangle(0, 0, 200, 40) };
        var first = new Panel { Size = new Size(50, 20) };
        var second = new Panel { Size = new Size(50, 20) };
        panel.Controls.Add(first);
        panel.Controls.Add(second);
        form.Controls.Add(panel);
        provider.SetClasses(panel, "flex gap-x-2.5 items-start");
        panel.PerformLayout();

        Assert.Equal(firstX, first.Left);
        Assert.Equal(rtl && !windowsLayout ? 90 : 60, second.Left);
    });

    [Fact]
    public void Check_box_indicator_moves_to_the_right() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var box = new TwCheckBox { Text = "Aktif", AutoSize = false, Size = new Size(120, 24), Checked = true, RightToLeft = RightToLeft.Yes };
        provider.SetClasses(box, "bg-white accent-red-600");

        using var bitmap = new Bitmap(box.Width, box.Height);
        box.DrawToBitmap(bitmap, new Rectangle(Point.Empty, box.Size));
        var red = ColorTranslator.FromHtml("#dc2626").ToArgb();
        Assert.Equal(red, bitmap.GetPixel(box.Width - 4, box.Height / 2).ToArgb());   // gösterge sağda
        Assert.NotEqual(red, bitmap.GetPixel(4, box.Height / 2).ToArgb());
    });
}
