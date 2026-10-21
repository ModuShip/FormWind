using System.Linq;
using Xunit;

namespace FormWind.Tests;

public class SuggestionTests
{
    private readonly TwTheme _theme = new TwTheme();

    [Theory]
    [InlineData("bg-blu-600", "bg-blue-600")]
    [InlineData("bg-bleu-600", "bg-blue-600")]
    [InlineData("bg-blue-650", "bg-blue-600")]
    [InlineData("font-bolder", "font-bold")]
    [InlineData("justify-centre", "justify-center")]
    [InlineData("roundd-lg", "rounded-lg")]
    [InlineData("hovr:bg-blue-700", "hover:bg-blue-700")]
    [InlineData("dark:hovr:bg-gry-800", "dark:hover:bg-gray-800")]
    [InlineData("bg-blu-600/50", "bg-blue-600/50")]
    [InlineData("btn-primry", "btn-primary")]
    public void Suggests_the_closest_recognized_class(string unknown, string expected)
    {
        Assert.Equal(expected, _theme.SuggestClass(unknown));
    }

    [Theory]
    [InlineData("bg-blue-600")]
    [InlineData("hover:bg-blue-700")]
    [InlineData("completely-unrelated-thing")]
    [InlineData("bg-nope-500")]
    [InlineData("x")]
    [InlineData("")]
    public void Suggests_nothing_for_recognized_or_distant_classes(string classes)
    {
        Assert.Null(_theme.SuggestClass(classes));
    }

    [Fact]
    public void Suggests_the_theme_s_own_colors()
    {
        var theme = new TwTheme();
        theme.SetColor("brand", "500", System.Drawing.Color.Teal);

        Assert.Equal("bg-brand-500", theme.SuggestClass("bg-brnd-500"));
    }

    [Fact]
    public void Every_vocabulary_entry_is_recognized()
    {
        var unknown = TwSuggestions.Vocabulary(_theme).Where(c => _theme.Parse(c).Unknown.Count > 0).ToList();

        Assert.Empty(unknown);
    }

    [Fact]
    public void Describe_adds_suggestions_to_the_unknown_classes()
    {
        Assert.Equal("bg-blu-600 (did you mean bg-blue-600?), not-a-class-at-all",
            TwSuggestions.Describe(new[] { "bg-blu-600", "not-a-class-at-all" }, _theme));
    }
}
