using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Bozuk girdi: hatalı class dizileri, bozuk tema dosyaları ve hata fırlatan özel uygulayıcılar uygulamayı düşürmemeli,
/// kilitlememeli; sorun tanınmayan class ya da StyleError olarak raporlanmalı.
/// </summary>
[Collection(UiCollection.Name)]
public class BadInputTests
{
    private static readonly string[] Malformed =
    {
        string.Concat(Enumerable.Repeat("p-4 ", 10000)),
        new string('x', 100_000),
        "bg-red-500\0text-white", "bg-İ p-ç ğ-4 😀 ​ ﻿",
        "bg-[#zz]", "bg-[]", "w-[", "]", "[", "bg-[rgb(999,999,999)]",
        "w-[99999999999px]", "h-[2147483648px]", "p-[99999999999px]", "rounded-[99999999999px]", "text-[0px]", "text-[0.00px]",
        "grid grid-cols-99999999999", "col-span-99999999999", "border-99999999999", "ring-99999999999", "opacity-99999999999",
        "bg-red-500/99999999999", "w-99999999999/2", "duration-99999999999", "-", ":", "hover:", ":bg-red-500",
        string.Concat(Enumerable.Repeat("dark:hover:", 200)) + "bg-red-500",
        "font-[" + new string('a', 10000) + "]",
    };

    // w-[99999999999px] gibi int'e sığmayan değerler OverflowException'ı SetClasses'tan dışarı fırlatıyordu; text-[0px] sıfır
    // boyutlu font oluşturmaya çalışıp her uygulamada StyleError veriyordu. İkisi de artık tanınmayan class.
    [Fact]
    public void Malformed_classes_never_throw_and_are_reported_as_unknown() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var errors = new List<TwStyleErrorEventArgs>();
        var unknown = new HashSet<string>();
        provider.StyleError += (_, e) => errors.Add(e);
        provider.UnknownClasses += (_, e) => unknown.UnionWith(e.Unknown);
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var panel = new TwPanel { Size = new Size(300, 200) };
        panel.Controls.Add(new Label { Text = "x" });
        var controls = new Control[] { new Button { Text = "a" }, new Label { Text = "a" }, new TextBox(), panel, new DataGridView { ColumnCount = 1, RowCount = 2 }, new ListView(), new TwButton { Text = "a" } };
        form.Controls.AddRange(controls);
        form.Show();

        var watch = Stopwatch.StartNew();
        foreach (var classes in Malformed)
            foreach (var control in controls)
            {
                provider.SetClasses(control, classes);
                provider.DarkMode = !provider.DarkMode;
                form.Refresh();
                provider.SetClasses(control, "");
            }

        Assert.Empty(errors);
        Assert.Contains("w-[99999999999px]", unknown);
        Assert.Contains("text-[0px]", unknown);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"{watch.Elapsed}");
        form.Close();
    });

    // Kendini 30 kez içeren bileşen açılımı 30^5 class'a büyütüp ayrıştırmayı ~20 s kilitliyordu. Her bileşen bir kez açılır,
    // tekrarlar ayıklanır; sonra gelen class yine kazanır.
    [Fact]
    public void Repeated_and_cyclic_components_expand_quickly_and_keep_the_last_class()
    {
        var fan = string.Join(" ", Enumerable.Repeat("a", 30));
        var theme = TwTheme.FromJson("{ \"components\": { \"a\": \"" + fan + " p-4\", \"loop\": \"other\", \"other\": \"loop px-2\", \"pair\": \"p-4 px-2 p-4\" } }");

        var watch = Stopwatch.StartNew();
        var parsed = theme.Parse("a");
        Assert.True(watch.ElapsedMilliseconds < 1000, $"{watch.ElapsedMilliseconds} ms");
        Assert.Equal(16, parsed.Resolve(TwState.None).Padding.Left);

        var cyclic = theme.Parse("loop");
        Assert.Equal(new[] { "loop" }, cyclic.Unknown);                                          // döngüyü kapatan ad
        Assert.Equal(8, cyclic.Resolve(TwState.None).Padding.Left);

        var pair = theme.Parse("pair").Resolve(TwState.None);                                     // p-4 son: px-2'yi ezer
        Assert.Equal(16, pair.Padding.Left);
    }

    // Bozuk tema dosyası türüne göre JsonException, InvalidOperationException ya da FormatException veriyordu.
    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{ \"colors\": {")]
    [InlineData("{ \"components\": { \"a\": 5 } }")]
    [InlineData("{ \"autoStyle\": { \"Button\": 5 } }")]
    [InlineData("{ \"colors\": { \"brand\": \"#zzz\" } }")]
    public void Invalid_theme_files_throw_format_exception(string json)
    {
        var error = Assert.Throws<FormatException>(() => TwTheme.FromJson(json));
        Assert.StartsWith("tw.config.json:", error.Message);
    }

    [Fact]
    public void Deeply_nested_theme_file_is_rejected_without_stack_overflow()
    {
        var json = string.Concat(Enumerable.Repeat("{\"a\":", 5000)) + "1" + new string('}', 5000);
        Assert.IsType<System.Text.Json.JsonException>(Assert.Throws<FormatException>(() => TwTheme.FromJson(json)).InnerException, exactMatch: false);
    }

    private sealed class PickyTarget : Control { }

    // CanApply'ı hata fırlatan özel uygulayıcı: uygulayıcı seçilirken çıkan hata da raporlanır, form açılır.
    private sealed class ThrowingSelector : IStyleApplier
    {
        public bool CanApply(Control control) => control is PickyTarget ? throw new InvalidOperationException("seçici hatası") : false;

        public void Apply(Control control, TwStyle style, TwApplyContext context) { }
    }

    static BadInputTests() => TwAppliers.Register(new ThrowingSelector());

    [Fact]
    public void Applier_whose_selection_throws_is_reported() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var errors = new List<TwStyleErrorEventArgs>();
        provider.StyleError += (_, e) => errors.Add(e);
        using var target = new PickyTarget();
        using var button = new Button();

        provider.SetClasses(target, "bg-red-500");
        provider.SetClasses(button, "bg-red-500");                                                // diğer kontroller etkilenmez

        var error = Assert.Single(errors);
        Assert.Same(target, error.Target);
        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(), button.BackColor.ToArgb());
    });
}
