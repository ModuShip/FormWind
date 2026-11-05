using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Reporting of classes with a wrong value: suggestions for a missing or wrong unit, which event carries what, and
/// <see cref="TwTheme.Explain"/>.
/// </summary>
public class ClassDiagnosticsTests
{
    private readonly TwTheme _theme = new TwTheme();

    [Theory]
    [InlineData("duration-[120]", "duration-[120ms]")]
    [InlineData("delay-[50]", "delay-[50ms]")]
    [InlineData("duration-[120px]", "duration-[120ms]")]
    [InlineData("w-[120]", "w-[120px]")]
    [InlineData("h-[40]", "h-[40px]")]
    [InlineData("min-w-[100]", "min-w-[100px]")]
    [InlineData("max-h-[300em]", "max-h-[300px]")]
    [InlineData("p-[8]", "p-[8px]")]
    [InlineData("px-[12rem]", "px-[12px]")]
    [InlineData("mt-[4]", "mt-[4px]")]
    [InlineData("gap-x-[6]", "gap-x-[6px]")]
    [InlineData("basis-[200]", "basis-[200px]")]
    [InlineData("text-[18]", "text-[18px]")]
    [InlineData("rounded-[6]", "rounded-[6px]")]
    [InlineData("hover:dark:w-[120]", "hover:dark:w-[120px]")]
    public void Suggests_the_unit_of_an_arbitrary_value(string unknown, string expected)
    {
        Assert.NotEmpty(_theme.Parse(unknown).Unknown);
        Assert.Equal(expected, _theme.SuggestClass(unknown));
        Assert.Empty(_theme.Parse(expected).Unknown);
    }

    [Theory]
    [InlineData("duration-[120ms]")]
    [InlineData("w-[120px]")]
    [InlineData("w-[12.5px]")]            // ondalıklı px desteklenmez; birim zaten doğru, öneri yok
    [InlineData("grid-cols-[1fr_2fr]")]
    [InlineData("bg-[ff0000]")]
    public void Suggests_nothing_for_values_with_the_right_unit_or_other_problems(string classes) =>
        Assert.Null(_theme.SuggestClass(classes));

    [Fact]
    public void Describe_includes_the_unit_suggestion() =>
        Assert.Equal("duration-[120] (did you mean duration-[120ms]?)", TwSuggestions.Describe(new[] { "duration-[120]" }, _theme));

    [Fact]
    public void Unknown_classes_event_carries_the_suggestions_and_not_style_error() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var button = new Button();
        var unknown = new List<TwUnknownClassesEventArgs>();
        var errors = new List<TwStyleErrorEventArgs>();
        provider.UnknownClasses += (_, e) => unknown.Add(e);
        provider.StyleError += (_, e) => errors.Add(e);

        provider.SetClasses(button, "transition duration-[120] w-[80] hover:bg-blu-600 no-such-thing px-4");

        Assert.Empty(errors);                                                      // StyleError: uygulama sırasında oluşan hatalar
        var args = Assert.Single(unknown);
        Assert.Same(button, args.Control);
        Assert.Equal(new[] { "duration-[120]", "w-[80]", "hover:bg-blu-600", "no-such-thing" }, args.Unknown.ToArray());
        Assert.Equal("duration-[120ms]", args.Suggestions["duration-[120]"]);
        Assert.Equal("w-[80px]", args.Suggestions["w-[80]"]);
        Assert.Equal("hover:bg-blue-600", args.Suggestions["hover:bg-blu-600"]);
        Assert.False(args.Suggestions.ContainsKey("no-such-thing"));
        Assert.Equal("duration-[120] (did you mean duration-[120ms]?), w-[80] (did you mean w-[80px]?), "
            + "hover:bg-blu-600 (did you mean hover:bg-blue-600?), no-such-thing", args.ToString());
    });

    [Fact]
    public void Event_args_created_in_code_have_no_suggestions()
    {
        using var button = new Button();
        var args = new TwUnknownClassesEventArgs(button, new[] { "foo" });
        Assert.Empty(args.Suggestions);
        Assert.Equal("foo", args.ToString());
    }

    [Fact]
    public void Explain_shows_what_each_class_resolves_to()
    {
        var text = _theme.Explain("bg-white hover:bg-gray-100 px-4 rounded-md font-bold");
        var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        Assert.Equal("bg-white", lines[0]);
        Assert.Equal("  BackColor = #FFFFFF", lines[1]);
        Assert.Contains("hover:bg-gray-100", lines);
        Assert.Contains("  [Hover] BackColor = #F3F4F6", lines);
        Assert.Contains("  Padding = left 16px, right 16px", lines);
        Assert.Contains("  Radius = 6", lines);
        Assert.Contains("  FontWeight = 700", lines);
    }

    [Fact]
    public void Explain_flags_unknown_classes_with_their_suggestion()
    {
        var lines = _theme.Explain("duration-[120] bg-blu-600 no-such-thing flex").Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        Assert.Equal(new[]
        {
            "duration-[120]", "  unknown class (did you mean duration-[120ms]?)",
            "bg-blu-600", "  unknown class (did you mean bg-blue-600?)",
            "no-such-thing", "  unknown class",
            "flex", "  Display = Flex",
        }, lines);
    }

    [Fact]
    public void Explain_shows_parts_dark_variants_and_components()
    {
        var text = _theme.Explain("header:bg-gray-100 dark:hover:text-white btn-primary");

        Assert.Contains("  header: BackColor = #F3F4F6", text);
        Assert.Contains("  [Dark,Hover] ForeColor = #FFFFFF", text);
        Assert.Contains("btn-primary", text);
        Assert.Contains("BackColor", text.Substring(text.IndexOf("btn-primary", StringComparison.Ordinal)));
    }

    [Fact]
    public void Explain_handles_empty_input_and_an_arbitrary_value()
    {
        Assert.Equal("(no classes)", _theme.Explain(null));
        Assert.Equal("(no classes)", _theme.Explain("  "));
        var text = _theme.Explain("duration-[120ms] w-[80px]");
        Assert.Contains("  DurationMs = 120", text);
        Assert.Contains("  Width = 80px", text);
    }
}
