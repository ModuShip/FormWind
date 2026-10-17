using Xunit;

namespace FormWind.Tests;

public class LayoutParserTests
{
    private readonly TwTheme _theme = new TwTheme();

    private TwStyle Base(string classes) => _theme.Parse(classes).Resolve(TwState.None);

    [Fact]
    public void Parses_container_classes()
    {
        var style = Base("flex flex-col flex-wrap gap-x-4 gap-y-2 justify-between items-center");

        Assert.Equal(TwDisplay.Flex, style.Display);
        Assert.Equal(TwFlexDirection.Column, style.FlexDirection);
        Assert.True(style.FlexWrap);
        Assert.Equal(16, style.GapX);
        Assert.Equal(8, style.GapY);
        Assert.Equal(TwJustify.Between, style.JustifyContent);
        Assert.Equal(TwAlign.Center, style.AlignItems);
    }

    [Fact]
    public void Gap_without_axis_sets_both()
    {
        var style = Base("gap-3");

        Assert.Equal(12, style.GapX);
        Assert.Equal(12, style.GapY);
    }

    [Fact]
    public void Parses_flex_shorthands()
    {
        var flex1 = Base("flex-1");
        Assert.Equal(1f, flex1.FlexGrow);
        Assert.Equal(1f, flex1.FlexShrink);
        Assert.Equal((TwLength?)TwLength.Pixels(0), flex1.FlexBasis);

        var none = Base("flex-none");
        Assert.Equal(0f, none.FlexGrow);
        Assert.Equal(0f, none.FlexShrink);
        Assert.Equal((TwLength?)TwLength.Auto, none.FlexBasis);

        Assert.Equal(0f, Base("shrink-0").FlexShrink);
    }

    [Fact]
    public void Parses_fractional_and_full_sizes()
    {
        Assert.Equal((TwLength?)TwLength.Fraction(0.5f), Base("w-1/2").Width);
        Assert.Equal((TwLength?)TwLength.Fraction(1), Base("h-full").Height);
        Assert.Equal((TwLength?)TwLength.Fraction(0.25f), Base("basis-1/4").FlexBasis);
        Assert.Equal((TwLength?)TwLength.Auto, Base("basis-auto").FlexBasis);
    }

    [Fact]
    public void Parses_min_and_max_sizes()
    {
        var style = Base("min-w-20 max-w-md min-h-[30px] max-h-px");

        Assert.Equal(80, style.MinWidth);
        Assert.Equal(448, style.MaxWidth);
        Assert.Equal(30, style.MinHeight);
        Assert.Equal(1, style.MaxHeight);
    }

    [Fact]
    public void Parses_grid_classes()
    {
        var style = Base("grid grid-cols-3 col-span-2");

        Assert.Equal(TwDisplay.Grid, style.Display);
        Assert.Equal(3, style.GridColumns);
        Assert.Equal(2, style.ColumnSpan);
        Assert.Equal(int.MaxValue, Base("col-span-full").ColumnSpan);
    }

    [Fact]
    public void Rejects_out_of_range_values()
    {
        var parsed = _theme.Parse("grid-cols-13 grid-cols-0 col-span-0 w-3/2 w-1/0");

        Assert.Equal(new[] { "grid-cols-13", "grid-cols-0", "col-span-0", "w-3/2", "w-1/0" }, parsed.Unknown);
    }
}
