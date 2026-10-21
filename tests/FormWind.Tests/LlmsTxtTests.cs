using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace FormWind.Tests;

/// <summary>website/llms.txt is what coding agents read; the classes it names must be the ones FormWind recognizes.</summary>
public class LlmsTxtTests
{
    private static readonly string[] Lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "llms.txt"));
    private readonly TwTheme _theme = new TwTheme();

    // Bir bölümün satırları (bir sonraki "## " başlığına kadar).
    private static IEnumerable<string> Section(string title) =>
        Lines.SkipWhile(l => l != "## " + title).Skip(1).TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal));

    // `…` içindeki somut class'lar: kalıplar ({color}, *, |, …) ve tire içermeyen sözcükler atlanır.
    private static IEnumerable<string> Classes(IEnumerable<string> lines) =>
        lines.SelectMany(l => Regex.Matches(l, "`([^`]+)`").Cast<Match>().Select(m => m.Groups[1].Value))
            .Where(c => c.Contains('-') && Regex.IsMatch(c, @"^[a-z0-9:\-\[\]#/.]+$") && !c.StartsWith("/", StringComparison.Ordinal));

    [Fact]
    public void Lists_exactly_the_default_components()
    {
        var listed = Section("Components (same names in every preset)")
            .SelectMany(l => Regex.Matches(l, "`([a-z-]+)`").Cast<Match>().Select(m => m.Groups[1].Value))
            .OrderBy(c => c, StringComparer.Ordinal);
        var semantic = new HashSet<string>(TwTheme.SemanticColors.Keys);
        var components = TwTheme.DefaultComponents.Keys.Where(k => !semantic.Contains(k)).OrderBy(c => c, StringComparer.Ordinal);

        Assert.Equal(components, listed);
    }

    [Fact]
    public void Utilities_and_variants_are_recognized()
    {
        var lines = Section("Utilities").Concat(Section("Variants")).Where(l => !l.StartsWith("Not supported", StringComparison.Ordinal)).ToList();
        var classes = Classes(lines).Where(c => !c.EndsWith(":", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(classes);
        Assert.DoesNotContain(classes, c => _theme.Parse(c).Unknown.Count > 0);
    }

    [Fact]
    public void Unsupported_classes_are_unknown()
    {
        var line = Section("Utilities").Single(l => l.StartsWith("Not supported", StringComparison.Ordinal));
        var classes = Classes(new[] { line }).ToList();

        Assert.NotEmpty(classes);
        Assert.All(classes, c => Assert.NotEmpty(_theme.Parse(c).Unknown));
    }
}
