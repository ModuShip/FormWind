using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace FormWind.Tests;

public class ClassMergeTests
{
    [Theory]
    [InlineData("p-2 px-4", "p-3", "p-3")]                                         // p tüm kenarları ezer
    [InlineData("p-2", "px-4", "p-2 px-4")]                                        // px, p'nin dikey kenarlarını ezmez
    [InlineData("px-2 pl-1 pr-3 py-2", "px-4", "py-2 px-4")]
    [InlineData("m-2 mt-1", "my-4", "m-2 my-4")]
    [InlineData("text-sm text-gray-700 text-left", "text-red-500", "text-sm text-left text-red-500")]
    [InlineData("text-sm text-gray-700", "text-[13px]", "text-gray-700 text-[13px]")]
    [InlineData("bg-white hover:bg-gray-100 dark:bg-gray-800", "hover:bg-blue-500", "bg-white dark:bg-gray-800 hover:bg-blue-500")]
    [InlineData("bg-white bg-gradient-to-r", "bg-[#123456]", "bg-gradient-to-r bg-[#123456]")]
    [InlineData("border border-gray-200 border-b-2", "border-2", "border-gray-200 border-b-2 border-2")]
    [InlineData("border border-gray-200", "border-red-500", "border border-red-500")]
    [InlineData("border-x border-l-4", "border-x-2", "border-x-2")]
    [InlineData("ring-2 ring-blue-500 ring-offset-2 ring-offset-white", "ring ring-offset-4", "ring-blue-500 ring-offset-white ring ring-offset-4")]
    [InlineData("shadow-md shadow-black", "shadow-lg", "shadow-black shadow-lg")]
    [InlineData("rounded-md font-bold font-mono", "rounded-full font-light", "font-mono rounded-full font-light")]
    [InlineData("w-full min-w-20 max-w-md whitespace-nowrap", "w-1/2", "min-w-20 max-w-md whitespace-nowrap w-1/2")]
    [InlineData("gap-4 gap-x-2", "gap-y-1", "gap-4 gap-x-2 gap-y-1")]                // gap-4 yatay aralığı da verir
    [InlineData("flex flex-col justify-between", "grid justify-center", "flex-col grid justify-center")]
    [InlineData("btn-primary px-6", "btn-primary", "px-6 btn-primary")]               // bilinmeyen class yalnızca kendini ezer
    [InlineData("", "  p-2   bg-white ", "p-2 bg-white")]
    public void Merge_removes_the_classes_it_overrides(string classes, string add, string expected) =>
        Assert.Equal(expected, TwClassMerge.Merge(classes, add));

    [Fact]
    public void Select_and_replace_work_on_one_variant()
    {
        var groups = new[] { "bg-color" };
        const string classes = "card bg-white hover:bg-gray-100 dark:bg-gray-800 p-4";
        Assert.Equal("bg-white", TwClassMerge.Select(classes, "", groups));
        Assert.Equal("hover:bg-gray-100", TwClassMerge.Select(classes, "hover:", groups));
        Assert.Equal("card hover:bg-gray-100 dark:bg-gray-800 p-4 bg-blue-600", TwClassMerge.Replace(classes, "", groups, "bg-blue-600"));
        Assert.Equal("card bg-white dark:bg-gray-800 p-4", TwClassMerge.Replace(classes, "hover:", groups, ""));
    }

    private static readonly string[] Pool =
    {
        "p-2", "px-4", "py-1", "pt-3", "pl-0.5", "p-px", "m-2", "mx-1", "mb-4",
        "bg-white", "bg-blue-600", "bg-[#123456]", "bg-gradient-to-r", "from-blue-500", "to-red-500",
        "text-white", "text-gray-700", "text-sm", "text-lg", "text-[13px]", "text-center", "text-left",
        "font-bold", "font-light", "font-mono", "font-sans", "italic", "not-italic", "underline",
        "border", "border-0", "border-2", "border-gray-300", "border-b", "border-x-2", "border-[#abcdef]",
        "rounded", "rounded-md", "rounded-[10px]", "shadow", "shadow-lg", "shadow-blue-500", "shadow-none",
        "ring", "ring-2", "ring-blue-500", "ring-offset-2", "ring-offset-white",
        "w-10", "w-full", "h-8", "min-w-20", "max-w-md", "gap-2", "gap-x-4",
        "flex", "grid", "flex-col", "flex-1", "grow", "shrink-0", "basis-1/2", "justify-center", "items-center", "self-end",
        "divide-y", "divide-y-0", "divide-gray-200", "cursor-pointer", "hidden", "visible", "truncate", "whitespace-normal",
        "transition", "duration-300", "btn-primary", "card",
        "hover:bg-gray-100", "hover:bg-blue-700", "hover:text-white", "dark:bg-gray-800", "dark:text-gray-100", "dark:hover:bg-gray-700",
    };

    private static readonly TwState[] States =
    {
        TwState.None, TwState.Hover, TwState.Dark, TwState.Dark | TwState.Hover, TwState.Focus | TwState.Disabled,
    };

    // Birleştirme bir class'ı yalnızca sonraki bir class onu tamamen eziyorsa siler: her çiftte ve rastgele dizilerde
    // sonuç, class'ları art arda yazmakla aynı stile çözülmeli.
    [Fact]
    public void Merge_never_changes_the_resolved_style()
    {
        var theme = new TwTheme();
        foreach (var first in Pool)
            foreach (var second in Pool)
                AssertSameStyle(theme, first + " " + second, TwClassMerge.Merge(first, second));

        var random = new Random(1234);
        for (int i = 0; i < 500; i++)
        {
            var tokens = Enumerable.Range(0, random.Next(2, 8)).Select(_ => Pool[random.Next(Pool.Length)]).ToArray();
            var merged = tokens.Aggregate("", (classes, token) => TwClassMerge.Merge(classes, token));
            AssertSameStyle(theme, string.Join(" ", tokens), merged);
        }
    }

    private static void AssertSameStyle(TwTheme theme, string concatenated, string merged)
    {
        foreach (var state in States)
        {
            var expected = Snapshot(theme.Parse(concatenated).Resolve(state));
            var actual = Snapshot(theme.Parse(merged).Resolve(state));
            Assert.True(expected == actual, $"'{concatenated}' → '{merged}' ({state}):\n{expected}\n≠\n{actual}");
        }
    }

    private static string Snapshot(object value)
    {
        var builder = new StringBuilder();
        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            var item = property.GetValue(value);
            builder.Append(property.Name).Append('=');
            builder.Append(item is TwSides sides ? Snapshot(sides) : item?.ToString() ?? "null").Append(';');
        }
        return builder.ToString();
    }

    [Fact]
    public void Theme_lists_its_own_colors_first()
    {
        var theme = new TwTheme();
        theme.SetColor("brand", "500", System.Drawing.Color.Purple);
        var keys = theme.ColorKeys().ToList();
        Assert.True(keys.IndexOf("brand-500") < keys.IndexOf("slate-50"));
        Assert.Contains("white", keys);
        Assert.Contains("blue-600", keys);
        Assert.DoesNotContain("blue", keys);                                         // tonlu renklerin tonsuz anahtarı yok
    }
}
