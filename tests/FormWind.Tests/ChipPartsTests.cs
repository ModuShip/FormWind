using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;
using Xunit.Abstractions;

namespace FormWind.Tests;

// TwChip'in parçaları (icon:, dot:, remove:, gap-*) class'larla stillenir; class verilmezse görünüm eskisiyle aynıdır.
[Collection(UiCollection.Name)]
public class ChipPartsTests
{
    private readonly ITestOutputHelper _output;

    public ChipPartsTests(ITestOutputHelper output) => _output = output;

    private static Form NewForm() =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(400, 200) };

    private static Color Resolve(string classes, string? part = null)
    {
        var style = TwTheme.Current.Parse(classes).ResolvePart(part, TwState.None);
        return style.ForeColor ?? style.BackColor!.Value;
    }

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    [Fact]
    public void Chip_component_without_part_classes_keeps_the_built_in_look() => Sta.Run(() =>
    {
        using var tw = new TailwindProvider();
        foreach (var component in new[] { "chip", "chip-primary", "chip-success", "chip-warning", "chip-danger" })
        {
            using var chip = new TwChip { Text = "Hat 2", IconName = "check", Removable = true, Checkable = true };
            tw.SetClasses(chip, component);
            Assert.Equal(TwChipParts.Default, chip.Parts);
            chip.Checked = true;
            Assert.Equal(TwChipParts.Default, chip.Parts);
        }
    });

    [Fact]
    public void Part_classes_reach_the_chip() => Sta.Run(() =>
    {
        using var form = NewForm();
        using var tw = new TailwindProvider { ContainerControl = form };
        var chip = new TwChip { Text = "Canlı", IconName = "check", Removable = true };
        form.Controls.Add(chip);
        tw.SetClasses(chip, "chip icon:text-red-600 icon:w-5 dot:bg-green-500 dot:w-2 remove:text-gray-400 remove:hover:text-gray-900 "
                            + "remove:hover:bg-gray-200 remove:w-4 gap-2");
        float scale = TwDpi.DeviceDpi(chip) / 96f;
        int Px(int pixels) => TwSides.Scale(pixels, scale);

        var parts = chip.Parts;
        Assert.Equal(Resolve("text-red-600").ToArgb(), parts.IconColor!.Value.ToArgb());
        Assert.Equal(Px(20), parts.IconSize);
        Assert.Equal(Resolve("bg-green-500").ToArgb(), parts.DotColor!.Value.ToArgb());
        Assert.Equal(Px(8), parts.DotSize);
        Assert.Equal(Resolve("text-gray-400").ToArgb(), parts.RemoveColor!.Value.ToArgb());
        Assert.Equal(Resolve("text-gray-900").ToArgb(), parts.RemoveHoverColor!.Value.ToArgb());
        Assert.Equal(Resolve("bg-gray-200").ToArgb(), parts.RemoveHoverBack!.Value.ToArgb());
        Assert.Equal(Px(16), parts.RemoveSize);
        Assert.Equal(Px(8), parts.Gap);
        Assert.Equal(Px(16), chip.RemoveButtonBounds.Width);
        Assert.False(parts.DisabledFromClasses);
        Assert.False(parts.FocusRingFromClasses);
    });

    [Fact]
    public void Dot_class_shows_the_dot_and_dot_color_property_wins() => Sta.Run(() =>
    {
        using var form = NewForm();
        using var tw = new TailwindProvider { ContainerControl = form };
        var chip = new TwChip { Text = "Canlı" };
        form.Controls.Add(chip);
        tw.SetClasses(chip, "chip");
        int plain = chip.GetPreferredSize(Size.Empty).Width;

        tw.SetClasses(chip, "chip dot:bg-green-500 dot:w-2");
        chip.Size = chip.GetPreferredSize(Size.Empty);
        Assert.True(chip.Width > plain);
        var dotCenter = new Point(chip.Padding.Left + chip.Parts.DotSize!.Value / 2, chip.Height / 2);
        using (var picture = Render(chip))
            Assert.Equal(Resolve("dot:bg-green-500", "dot").ToArgb(), picture.GetPixel(dotCenter.X, dotCenter.Y).ToArgb());

        chip.DotColor = Color.FromArgb(255, 0, 0, 255);
        using (var picture = Render(chip))
            Assert.Equal(Color.FromArgb(255, 0, 0, 255).ToArgb(), picture.GetPixel(dotCenter.X, dotCenter.Y).ToArgb());
    });

    [Fact]
    public void Disabled_text_and_focus_ring_classes_replace_the_built_in_ones() => Sta.Run(() =>
    {
        using var tw = new TailwindProvider();
        using var chip = new TwChip { Text = "Hat 2", Clickable = true };
        tw.SetClasses(chip, "chip disabled:text-gray-400 focus-visible:ring-2 focus-visible:ring-primary-500");
        Assert.True(chip.Parts.DisabledFromClasses);
        Assert.True(chip.Parts.FocusRingFromClasses);
    });

    // Pilot ölçümü: aynı çip görünümü ilkellerden (TwPanel + TwIcon + TwHeading) class'larla kurulabilir.
    [Fact]
    public void Chip_look_can_be_built_from_primitives() => Sta.Run(() =>
    {
        using var form = NewForm();
        form.BackColor = Color.White;
        using var tw = new TailwindProvider { ContainerControl = form };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, WrapContents = false };
        form.Controls.Add(flow);

        var chip = new TwChip { Text = "Vardiya B", IconName = "check" };
        flow.Controls.Add(chip);
        tw.SetClasses(chip, "chip-success icon:w-3");

        var built = new TwPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var icon = new TwIcon { IconName = "check", AutoSize = true };
        var label = new TwHeading { Text = "Vardiya B" };
        built.Controls.AddRange(new Control[] { icon, label });
        flow.Controls.Add(built);
        tw.SetClasses(built, "chip-success flex items-center gap-1 rounded-full");
        tw.SetClasses(label, "text-xs font-medium text-success-800");
        tw.SetClasses(icon, "text-xs text-success-800");

        form.Show();
        try
        {
            Application.DoEvents();
            using var a = Render(chip);
            using var b = Render(built);
            int width = Math.Min(a.Width, b.Width), height = Math.Min(a.Height, b.Height), same = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (Close(a.GetPixel(x, y), b.GetPixel(x, y))) same++;
            double ratio = same / (double)(width * height);
            _output.WriteLine($"TwChip {chip.Size}, primitives {built.Size}, matching pixels {ratio:P1}");

            Assert.Equal(chip.BackColor.ToArgb(), built.BackColor.ToArgb());
            // Metin Label yerine TwHeading: Label GDI iç payıyla ölçer (96 DPI'da +7 px genişlik, +6 px yükseklik), TwHeading
            // çip gibi NoPadding ile. Simge boyu iki yanda da class'tan (icon:w-3 ve text-xs) gelince boyutlar birebir aynı olur.
            Assert.Equal(chip.Size, built.Size);
            Assert.True(ratio > 0.95, $"matching pixels {ratio:P1}");
        }
        finally
        {
            form.Close();
        }

        static bool Close(Color x, Color y) =>
            Math.Abs(x.R - y.R) <= 24 && Math.Abs(x.G - y.G) <= 24 && Math.Abs(x.B - y.B) <= 24;
    });
}
