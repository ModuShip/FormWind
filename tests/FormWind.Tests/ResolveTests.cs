using System.Drawing;
using Xunit;

namespace FormWind.Tests;

public class ResolveTests
{
    private readonly TwTheme _theme = new TwTheme();

    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    private int? Back(string classes, TwState state) => _theme.Parse(classes).Resolve(state).BackColor?.ToArgb();

    [Fact]
    public void Hover_overrides_base()
    {
        const string classes = "bg-blue-600 hover:bg-blue-700";

        Assert.Equal(Argb("#2563eb"), Back(classes, TwState.None));
        Assert.Equal(Argb("#1d4ed8"), Back(classes, TwState.Hover));
    }

    [Fact]
    public void Disabled_wins_regardless_of_class_order()
    {
        const string classes = "disabled:bg-gray-300 hover:bg-blue-700 bg-blue-600";

        Assert.Equal(Argb("#d1d5db"), Back(classes, TwState.Hover | TwState.Disabled));
    }

    [Fact]
    public void Dark_hover_wins_over_hover_and_dark()
    {
        const string classes = "hover:bg-blue-700 dark:hover:bg-blue-400 dark:bg-blue-500";

        Assert.Equal(Argb("#60a5fa"), Back(classes, TwState.Dark | TwState.Hover));
        Assert.Equal(Argb("#3b82f6"), Back(classes, TwState.Dark));
        Assert.Equal(Argb("#1d4ed8"), Back(classes, TwState.Hover));
    }

    [Fact]
    public void Later_class_wins_within_same_variant()
    {
        Assert.Equal(Argb("#3b82f6"), Back("bg-red-500 bg-blue-500", TwState.None));
    }

    [Fact]
    public void Variant_order_is_normalized()
    {
        var parsed = _theme.Parse("hover:dark:bg-red-500 dark:hover:p-2");

        Assert.Single(parsed.Variants);
        Assert.Equal(TwState.Dark | TwState.Hover, parsed.Variants[0].Conditions);
    }

    [Fact]
    public void ResolveExact_returns_only_the_state_layer()
    {
        var parsed = _theme.Parse("bg-blue-600 p-2 hover:bg-blue-700 dark:hover:bg-blue-400");

        var tracking = parsed.ResolveExact(TwState.Hover, TwState.Hover);
        Assert.Equal(Argb("#1d4ed8"), tracking.BackColor?.ToArgb());
        Assert.Null(tracking.Padding.Left);

        var darkTracking = parsed.ResolveExact(TwState.Hover, TwState.Hover | TwState.Dark);
        Assert.Equal(Argb("#60a5fa"), darkTracking.BackColor?.ToArgb());
    }
}
