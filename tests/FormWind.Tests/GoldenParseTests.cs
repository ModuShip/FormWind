using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Class söz diziminin sözleşmesi: örnek class dizilerinin çözümlenmiş <see cref="TwStyle"/> değerleri <c>Golden/parse.txt</c>
/// dosyasındakiyle aynı kalmalı. Bir class'ın anlamı değişirse (derleme hatası vermeden uygulamaların görünümünü değiştirir)
/// bu test düşer; değişiklik bilinçliyse <c>FORMWIND_UPDATE_BASELINES=1</c> ile dosya yeniden yazılır ve fark CHANGELOG'a eklenir.
/// </summary>
public class GoldenParseTests
{
    // Görsel referanslarla aynı değişken (VisualBaselines yalnızca net472 hedefinde derlenir).
    private const string UpdateVariable = "FORMWIND_UPDATE_BASELINES";

    private static bool UpdateMode => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    private static readonly string[] Classes =
    {
        // Renkler ve saydamlık
        "bg-blue-500 text-white",
        "bg-red-600/50 text-gray-900/75",
        "bg-[#1e3f6e] text-[#fff] border-[rgb(10,20,30)]",
        "bg-transparent text-black border-current",
        "accent-pink-500 fill-emerald-400",
        // Boşluk ve boyut
        "p-4",
        "px-3 py-1.5",
        "pt-1 pr-2 pb-3 pl-4",
        "m-2 mx-auto",
        "ms-2 me-4",
        "p-[13px] m-[7px]",
        "w-64 h-10",
        "w-1/2 h-full",
        "w-auto min-w-32 max-w-96 min-h-8 max-h-64",
        "w-[250px] h-[3rem]",
        // Yazı
        "text-sm font-medium",
        "text-2xl font-bold italic underline",
        "text-[15px] font-[650]",
        "font-sans font-mono",
        "text-center truncate",
        "text-right whitespace-nowrap",
        // Kenarlık, köşe, gölge, halka
        "border border-gray-300 rounded-md",
        "border-2 border-t-4 border-x-0",
        "rounded-full rounded-lg rounded-none",
        "rounded-[10px] border-[3px]",
        "divide-y divide-gray-200",
        "shadow-md shadow-blue-500/30",
        "shadow-none",
        "ring-2 ring-blue-500 ring-offset-2 ring-offset-white",
        // Geçişler, imleç, görünürlük
        "bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500",
        "bg-gradient-to-b from-white to-gray-100",
        "cursor-pointer hidden",
        "transition duration-300",
        "object-cover",
        // Yerleşim
        "flex flex-row gap-4 items-center justify-between",
        "flex flex-col flex-wrap gap-x-2 gap-y-6",
        "grow shrink-0 basis-1/3",
        "self-end",
        "grid grid-cols-3 grid-rows-2 col-span-2 row-span-2",
        "block",
        "group",
        // Varyantlar
        "bg-white hover:bg-gray-100 active:bg-gray-200",
        "focus:ring-2 focus-visible:ring-4 focus:ring-blue-500",
        "disabled:opacity-50 disabled:bg-gray-200 disabled:text-gray-400",
        "checked:bg-blue-600 invalid:border-red-500",
        "group-hover:text-blue-600",
        "dark:bg-gray-900 dark:text-white dark:hover:bg-gray-800",
        "p-2 md:p-4 lg:p-8",
        "ml-2 rtl:mr-2 rtl:ml-0 ltr:text-left",
        "bg-blue-500 forced-colors:bg-white",
        "p-2 p-4",
        "bg-red-500 bg-blue-500",
        // Bileşenler
        "btn",
        "btn-primary",
        "btn-secondary px-6",
        "btn-danger",
        "card",
        "input",
        "badge",
        "alert-warning",
        "link",
        "title",
        "muted",
        // Bilinmeyenler
        "not-a-class bg-blue-500 p-unknown",
        "",
    };

    // Bölüm (part) önekli class'lar: DataGridView, liste, sekme parçaları.
    private static readonly (string Classes, string Part)[] Parts =
    {
        ("header:bg-gray-100 header:font-semibold cell:px-3", "header"),
        ("header:bg-gray-100 cell:px-3 cell:hover:bg-blue-50", "cell"),
        ("item:py-2 selected:bg-blue-600 selected:text-white", "selected"),
    };

    private static readonly TwState[] States =
    {
        TwState.Hover, TwState.Active, TwState.Focus, TwState.FocusVisible, TwState.Disabled, TwState.Checked,
        TwState.Invalid, TwState.GroupHover, TwState.Dark, TwState.Dark | TwState.Hover, TwState.Md, TwState.Lg,
        TwState.Rtl, TwState.Ltr,
    };

    private static readonly (string Name, Func<TwTheme> Create, string[] Classes)[] Themes =
    {
        ("default", () => new TwTheme(), Classes),
        ("palette v4", () => new TwTheme(TwPaletteVersion.V4), new[] { "bg-blue-500 text-gray-900 border-red-600", "btn-primary" }),
        ("preset fluent", () => new TwTheme(TwPreset.Fluent), new[] { "btn-primary", "input", "card" }),
        ("preset antd", () => new TwTheme(TwPreset.Antd), new[] { "btn-primary", "input", "card" }),
        ("preset material", () => new TwTheme(TwPreset.Material), new[] { "btn-primary", "input", "card" }),
    };

    [Fact]
    public void Class_syntax_resolves_as_in_the_golden_file()
    {
        var actual = Render();
        var path = Path.Combine(AppContext.BaseDirectory, "Golden", "parse.txt");

        if (UpdateMode || !File.Exists(path))
        {
            // Kaynak klasöre yazılır (bir sonraki derleme çıktıya kopyalar); CI'da kaynak yolu /_/ olarak eşlendiğinden çıktıya.
            var source = SourceGoldenPath();
            if (!Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(source)!))) source = path;
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, actual, new UTF8Encoding(false));
            path = source;
            if (!UpdateMode)
                Assert.Fail($"{path} was missing and has been written; review and commit it.");
            return;
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n");
        if (expected == actual) return;

        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        int line = 0;
        while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line]) line++;
        string At(string[] lines) => line < lines.Length ? lines[line] : "<end of file>";
        Assert.Fail(
            $"The meaning of a class changed (Golden/parse.txt, line {line + 1}).{Environment.NewLine}" +
            $"  expected: {At(expectedLines)}{Environment.NewLine}" +
            $"  actual:   {At(actualLines)}{Environment.NewLine}" +
            $"If the change is intended, run the tests with {UpdateVariable}=1 and list it in CHANGELOG.md.");
    }

    // ContinuousIntegrationBuild (CI) yolları /_/ olarak eşler; okuma bu yüzden çıktıya kopyalanan dosyadan yapılır.
    private static string SourceGoldenPath([CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "Golden", "parse.txt");

    internal static string Render()
    {
        var output = new StringBuilder();
        output.Append("# Resolved TwStyle values of sample class strings (GoldenParseTests).\n");
        output.Append("# Only states whose result differs from the base state are listed.\n");
        foreach (var (name, create, classes) in Themes)
        {
            var theme = create();
            output.Append('\n').Append("## ").Append(name).Append('\n');
            foreach (var sample in classes) Append(output, theme, sample, null);
            if (name == "default")
                foreach (var (sample, part) in Parts) Append(output, theme, sample, part);
        }
        return output.ToString();
    }

    private static void Append(StringBuilder output, TwTheme theme, string classes, string? part)
    {
        var parsed = theme.Parse(classes);
        output.Append('\n').Append('"').Append(classes).Append('"');
        if (part != null) output.Append(" (part ").Append(part).Append(')');
        output.Append('\n');
        if (parsed.Unknown.Count > 0) output.Append("  unknown: ").Append(string.Join(" ", parsed.Unknown)).Append('\n');

        var baseLines = Describe(parsed.ResolvePart(part, TwState.None));
        foreach (var line in baseLines) output.Append("  ").Append(line).Append('\n');

        // Yüksek karşıtlık her stilde aynı sistem renklerini ekler; yalnızca forced-colors: içeren örneklerde listelenir.
        var states = classes.Contains("forced-colors:") ? States.Concat(new[] { TwState.HighContrast }) : States;
        foreach (var state in states)
        {
            var lines = Describe(parsed.ResolvePart(part, state));
            if (lines.SequenceEqual(baseLines)) continue;
            output.Append("  [").Append(state.ToString().Replace(", ", "|")).Append("]\n");
            foreach (var line in lines.Except(baseLines)) output.Append("    ").Append(line).Append('\n');
            foreach (var removed in baseLines.Select(l => l.Substring(0, l.IndexOf(" = ", StringComparison.Ordinal)))
                         .Except(lines.Select(l => l.Substring(0, l.IndexOf(" = ", StringComparison.Ordinal)))))
                output.Append("    ").Append(removed).Append(" = (unset)\n");
        }
    }

    private static List<string> Describe(TwStyle style)
    {
        var lines = new List<string>();
        foreach (var property in typeof(TwStyle).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => !(p.Name.StartsWith("Has", StringComparison.Ordinal) && p.PropertyType == typeof(bool)))
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            var text = Format(property.GetValue(style));
            if (text != null) lines.Add(property.Name + " = " + text);
        }
        return lines;
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        Color color => color.IsSystemColor ? color.Name : "#" + color.ToArgb().ToString("X8", CultureInfo.InvariantCulture),
        TwSides sides => sides.IsEmpty ? null : $"{Side(sides.Left)},{Side(sides.Top)},{Side(sides.Right)},{Side(sides.Bottom)}",
        // float.ToString() .NET Framework ile .NET'te farklı basamak verir; tüm hedeflerde aynı metin için biçim sabittir.
        TwLength length => length.IsAuto ? "auto"
            : length.IsPixels ? length.Value.ToString("0.####", CultureInfo.InvariantCulture) + "px"
            : (length.Value * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%",
        // Cursor.ToString() .NET Framework'te imlecin adını vermez; Cursors'taki adıyla yazılır.
        Cursor cursor => typeof(Cursors).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(p => ReferenceEquals(p.GetValue(null), cursor))?.Name ?? "custom",
        bool flag => flag ? "true" : "false",
        float number => number.ToString("0.####", CultureInfo.InvariantCulture),
        double number => number.ToString("0.####", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static string Side(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";
}
