using System.Drawing;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// Düşük riskli bileşenler: yedek renkler temanın anlamsal renkleri, ▲▼ arrow: ile, kart şeridi aralıkları gap-* ile.
[Collection(UiCollection.Name)]
public class ControlPartClassTests
{
    private static Color ThemeColor(TwTheme theme, string classes)
    {
        var style = theme.Parse(classes).Resolve(TwState.None);
        return style.ForeColor ?? style.BackColor!.Value;
    }

    [Fact]
    public void Field_fallback_colors_follow_the_theme_and_match_the_old_values_on_tailwind() => Sta.Run(() =>
    {
        var previous = TwTheme.Current;
        try
        {
            using var tw = new TailwindProvider();
            using var field = new TwField { LabelText = "E-posta", HintText = "İş adresiniz" };
            tw.SetClasses(field, "label:text-sm");
            Assert.Equal(ColorTranslator.FromHtml("#6b7280").ToArgb(), field.MessageLabel.ForeColor.ToArgb());

            var material = new TwTheme(TwPreset.Material);
            TwTheme.Current = material;
            Assert.Equal(ThemeColor(material, "text-surface-500").ToArgb(), field.MessageLabel.ForeColor.ToArgb());
        }
        finally
        {
            TwTheme.Current = previous;
        }
    });

    [Fact]
    public void Progress_fallback_colors_follow_the_theme_and_match_the_old_values_on_tailwind() => Sta.Run(() =>
    {
        var previous = TwTheme.Current;
        try
        {
            using var tw = new TailwindProvider();
            using var progress = new TwProgress { Value = 40 };
            tw.SetClasses(progress, "rounded-full");
            Assert.Equal(ColorTranslator.FromHtml("#e5e7eb").ToArgb(), progress.TrackColor.ToArgb());
            Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), progress.BarColor.ToArgb());

            var fluent = new TwTheme(TwPreset.Fluent);
            TwTheme.Current = fluent;
            Assert.Equal(ThemeColor(fluent, "bg-primary-600").ToArgb(), progress.BarColor.ToArgb());
            Assert.Equal(ThemeColor(fluent, "bg-surface-200").ToArgb(), progress.TrackColor.ToArgb());
        }
        finally
        {
            TwTheme.Current = previous;
        }
    });

    [Fact]
    public void Number_box_arrows_take_arrow_part_classes() => Sta.Run(() =>
    {
        using var tw = new TailwindProvider();
        using var box = new TwNumberBox { Size = new Size(120, 30) };
        tw.SetClasses(box, "input");
        Assert.Null(box.ArrowColor);
        Assert.Null(box.ArrowSize);

        tw.SetClasses(box, "input arrow:text-primary-600 arrow:w-3");
        Assert.Equal(ThemeColor(TwTheme.Current, "text-primary-600").ToArgb(), box.ArrowColor!.Value.ToArgb());
        Assert.Equal(TwSides.Scale(12, TwDpi.DeviceDpi(box) / 96f), box.ArrowSize);
        using var bitmap = new Bitmap(box.Width, box.Height);
        box.DrawToBitmap(bitmap, new Rectangle(Point.Empty, box.Size));
    });

    [Fact]
    public void Card_strip_spacing_comes_from_gap_classes() => Sta.Run(() =>
    {
        using var tw = new TailwindProvider();
        using var strip = new TwCardStrip { Size = new Size(400, 160) };
        tw.SetClasses(strip, "p-0");
        Assert.Equal(12, strip.Gap);
        Assert.Null(strip.NavSpacing);

        tw.SetClasses(strip, "p-0 gap-x-4 gap-y-6");
        Assert.Equal(16, strip.Gap);
        Assert.Equal(24, strip.NavSpacing);
    });
}
