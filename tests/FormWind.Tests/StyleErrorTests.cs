using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class StyleErrorTests
{
    private sealed class FailingTarget : Control { }

    private sealed class FailingApplier : IStyleApplier
    {
        public bool CanApply(Control control) => control is FailingTarget;

        public void Apply(Control control, TwStyle style, TwApplyContext context) =>
            throw new InvalidOperationException("uygulayıcı hatası");
    }

    static StyleErrorTests() => TwAppliers.Register(new FailingApplier());

    [Fact]
    public void Applier_errors_are_reported_and_swallowed() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var target = new FailingTarget();
        var errors = new List<TwStyleErrorEventArgs>();
        provider.StyleError += (_, e) => errors.Add(e);

        provider.SetClasses(target, "bg-red-500 hover:bg-red-600");   // istisna dışarı çıkmaz
        provider.DarkMode = true;                                       // yeniden denenir, yine raporlanır

        Assert.Equal(2, errors.Count);
        Assert.Same(target, errors[0].Target);
        Assert.Equal("bg-red-500 hover:bg-red-600", errors[0].Classes);
        Assert.IsType<InvalidOperationException>(errors[0].Exception);
    });

    [Fact]
    public void Without_listener_errors_do_not_escape_and_listener_can_rethrow() => Sta.Run(() =>
    {
        using (var provider = new TailwindProvider())
        using (var target = new FailingTarget())
            provider.SetClasses(target, "bg-red-500");

        using var strict = new TailwindProvider();
        using var other = new FailingTarget();
        strict.StyleError += (_, e) => throw new InvalidOperationException("test", e.Exception);
        Assert.Throws<InvalidOperationException>(() => strict.SetClasses(other, "bg-red-500"));
    });

    [Fact]
    public void Throwing_row_rule_does_not_break_grid_painting() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var grid = new DataGridView { ColumnCount = 2, RowCount = 3, Size = new Size(200, 120) };
        var errors = new List<TwStyleErrorEventArgs>();
        provider.StyleError += (_, e) => errors.Add(e);
        provider.SetRowClasses(grid, _ => throw new FormatException("kural hatası"));

        using var bitmap = new Bitmap(200, 120);
        grid.DrawToBitmap(bitmap, new Rectangle(0, 0, 200, 120));

        Assert.NotEmpty(errors);
        Assert.Same(grid, errors[0].Target);
        Assert.IsType<FormatException>(errors[0].Exception);
    });
}
