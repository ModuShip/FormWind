using System.Drawing;
using System.Globalization;
using System.Threading;
using Xunit;

namespace FormWind.Tests;

public class ParserTests
{
    private readonly TwTheme _theme = new TwTheme();

    private TwStyle Base(string classes) => _theme.Parse(classes).Resolve(TwState.None);

    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    [Fact]
    public void Parses_background_and_text_colors()
    {
        var style = Base("bg-blue-600 text-white");

        Assert.Equal(Argb("#2563eb"), style.BackColor!.Value.ToArgb());
        Assert.Equal(Color.White.ToArgb(), style.ForeColor!.Value.ToArgb());
    }

    [Fact]
    public void Parses_arbitrary_colors()
    {
        var style = Base("bg-[#1e3f6e] text-[#fff]");

        Assert.Equal(Argb("#1e3f6e"), style.BackColor!.Value.ToArgb());
        Assert.Equal(Color.White.ToArgb(), style.ForeColor!.Value.ToArgb());
    }

    [Fact]
    public void Later_axis_class_overrides_earlier_shorthand()
    {
        var style = Base("p-2 px-4");

        Assert.Equal(16, style.Padding.Left);
        Assert.Equal(16, style.Padding.Right);
        Assert.Equal(8, style.Padding.Top);
        Assert.Equal(8, style.Padding.Bottom);
    }

    [Fact]
    public void Spacing_supports_px_half_steps_and_arbitrary_values()
    {
        Assert.Equal(1, Base("m-px").Margin.Left);
        Assert.Equal(10, Base("pt-[10px]").Padding.Top);
        Assert.Equal((TwLength?)TwLength.Pixels(40), Base("h-10").Height);
        Assert.Equal((TwLength?)TwLength.Pixels(128), Base("w-32").Width);
    }

    [Fact]
    public void Decimal_spacing_is_culture_invariant()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal(2, new TwTheme().Parse("p-0.5").Resolve(TwState.None).Padding.Left);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("text-xs", 12f)]
    [InlineData("text-sm", 14f)]
    [InlineData("text-base", 16f)]
    [InlineData("text-2xl", 24f)]
    public void Parses_font_sizes(string classes, float expected) =>
        Assert.Equal(expected, Base(classes).FontSizePx);

    [Theory]
    [InlineData("rounded", 4)]
    [InlineData("rounded-md", 6)]
    [InlineData("rounded-none", 0)]
    [InlineData("rounded-full", 9999)]
    [InlineData("rounded-[10px]", 10)]
    public void Parses_radius(string classes, int expected) =>
        Assert.Equal(expected, Base(classes).Radius);

    [Fact]
    public void Border_color_does_not_imply_width()
    {
        Assert.Null(Base("border-red-500").BorderWidth);
        Assert.Equal(1, Base("border").BorderWidth);

        var style = Base("border-2 border-red-500");
        Assert.Equal(2, style.BorderWidth);
        Assert.Equal(Argb("#ef4444"), style.BorderColor!.Value.ToArgb());
    }

    [Fact]
    public void Collects_unknown_classes()
    {
        var parsed = _theme.Parse("bg-white bg-blue-1000 foo hover2:bg-white");

        Assert.Equal(new[] { "bg-blue-1000", "foo", "hover2:bg-white" }, parsed.Unknown);
        Assert.Equal(Color.White.ToArgb(), parsed.Resolve(TwState.None).BackColor!.Value.ToArgb());
    }

    [Fact]
    public void Expands_components_recursively()
    {
        var style = Base("btn-primary");

        Assert.Equal(16, style.Padding.Left);
        Assert.Equal(6, style.Radius);
        Assert.Equal(Argb("#2563eb"), style.BackColor!.Value.ToArgb());
    }

    [Fact]
    public void Cyclic_components_terminate()
    {
        _theme.SetComponent("a", "b");
        _theme.SetComponent("b", "a");

        Assert.NotEmpty(_theme.Parse("a").Unknown);
    }

    [Fact]
    public void Parse_results_are_cached_per_normalized_string()
    {
        Assert.Same(_theme.Parse("bg-white  p-2"), _theme.Parse(" bg-white p-2 "));
    }

    [Fact]
    public void Theme_change_invalidates_cache()
    {
        var before = _theme.Parse("p-1");
        _theme.SpacingUnit = 8;

        Assert.NotSame(before, _theme.Parse("p-1"));
        Assert.Equal(8, _theme.Parse("p-1").Resolve(TwState.None).Padding.Left);
    }
}
