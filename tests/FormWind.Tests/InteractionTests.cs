using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class InteractionTests
{
    private static readonly MethodInfo OnMouseEnter =
        typeof(Control).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    private static void Enter(Control control) => OnMouseEnter.Invoke(control, new object[] { EventArgs.Empty });

    [Fact]
    public void Parses_transition_group_and_gradient_classes()
    {
        var theme = new TwTheme();
        var style = theme.Parse("group transition duration-300 bg-gradient-to-br from-red-500 via-white to-blue-500").Resolve(TwState.None);

        Assert.True(style.IsGroup);
        Assert.True(style.Transition);
        Assert.Equal(300, style.DurationMs);
        Assert.Equal(TwGradientDirection.ToBottomRight, style.GradientDirection);
        Assert.True(style.HasGradient);
        Assert.Equal(250, theme.Parse("duration-[250ms]").Resolve(TwState.None).DurationMs);

        var parsed = theme.Parse("group-hover:text-blue-600");
        Assert.Null(parsed.Resolve(TwState.Hover).ForeColor);
        Assert.NotNull(parsed.Resolve(TwState.GroupHover).ForeColor);
    }

    [Fact]
    public void Gradient_endpoints_default_to_transparent()
    {
        var gradient = new TwGradient(TwGradientDirection.ToRight, Color.Red, null, null);
        Assert.Equal(0, gradient.To.A);
        Assert.Equal(Color.Red.ToArgb() & 0xFFFFFF, gradient.To.ToArgb() & 0xFFFFFF);
    }

    [Fact]
    public void Animator_interpolates_with_ease_out_and_completes() => Sta.Run(() =>
    {
        using var label = new Label { BackColor = Color.Black };
        bool completed = false;
        var start = DateTime.UtcNow;

        TwAnimator.Animate(label, TwColorChannel.Back, Color.White, 100, () => completed = true);
        Assert.True(TwAnimator.IsAnimating(label, TwColorChannel.Back));

        TwAnimator.Step(start.AddMilliseconds(50));
        var mid = label.BackColor.R;
        Assert.InRange(mid, 180, 250); // ease-out: yarı zamanda yolun %87'si

        TwAnimator.Step(start.AddMilliseconds(200));
        Assert.Equal(Color.White.ToArgb(), label.BackColor.ToArgb());
        Assert.True(completed);
        Assert.False(TwAnimator.IsAnimating(label, TwColorChannel.Back));
    });

    [Fact]
    public void Transition_class_animates_hover_color_instead_of_jumping() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var button = new TwButton { Size = new Size(100, 30) };
        form.Controls.Add(button);
        form.Show();
        provider.SetClasses(button, "bg-white hover:bg-black transition duration-200");

        Enter(button);

        Assert.True(TwAnimator.IsAnimating(button, TwColorChannel.Back));
        Assert.NotEqual(Color.Black.ToArgb(), button.BackColor.ToArgb());
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(Color.Black.ToArgb(), button.BackColor.ToArgb());
        form.Close();
    });

    [Fact]
    public void Group_hover_styles_children_when_entering_the_group_or_a_child() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var row = new TwPanel { Size = new Size(200, 40) };
        var label = new Label { Text = "Rick", AutoSize = true };
        row.Controls.Add(label);
        form.Controls.Add(row);
        form.Show();
        provider.SetClasses(row, "group hover:bg-gray-50");
        provider.SetClasses(label, "text-gray-700 group-hover:text-blue-600");

        Assert.Equal(Argb("#374151"), label.ForeColor.ToArgb());

        // İmleç satıra doğrudan etiketin üzerinden girer: satırın kendi MouseEnter'ı tetiklenmez.
        Enter(label);

        Assert.Equal(Argb("#f9fafb"), row.BackColor.ToArgb());
        Assert.Equal(Argb("#2563eb"), label.ForeColor.ToArgb());
        form.Close();
    });

    [Fact]
    public void Placeholder_is_drawn_with_part_color_when_empty() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var box = new TwTextBox { Size = new Size(200, 36), PlaceholderText = "Search by Name..." };
        provider.SetClasses(box, "bg-white placeholder:text-red-600");

        Assert.Equal(Argb("#dc2626"), box.PlaceholderColor!.Value.ToArgb());
        Assert.True(CountColor(box, Argb("#dc2626"), tolerance: 60) > 10);

        box.Text = "Rick";
        Assert.Equal(0, CountColor(box, Argb("#dc2626"), tolerance: 60));
    });

    [Fact]
    public void Gradient_background_is_painted_on_TwPanel() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var panel = new TwPanel { Size = new Size(100, 20) };
        provider.SetClasses(panel, "bg-gradient-to-r from-red-500 to-blue-500");

        using var bitmap = new Bitmap(100, 20);
        panel.DrawToBitmap(bitmap, new Rectangle(0, 0, 100, 20));
        var left = bitmap.GetPixel(2, 10);
        var right = bitmap.GetPixel(97, 10);
        Assert.True(left.R > left.B, $"sol {left}");
        Assert.True(right.B > right.R, $"sağ {right}");
    });

    private static int CountColor(Control control, int argb, int tolerance)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        var target = Color.FromArgb(argb);
        int count = 0;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
            {
                var c = bitmap.GetPixel(x, y);
                if (Math.Abs(c.R - target.R) + Math.Abs(c.G - target.G) + Math.Abs(c.B - target.B) < tolerance) count++;
            }
        return count;
    }

    [Fact]
    public void Hover_does_not_reapply_style_without_hover_variants() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var strip = new ToolStrip();
        provider.SetClasses(strip, "bg-gray-50 text-gray-800");
        var renderer = strip.Renderer;

        Enter(strip);

        Assert.Same(renderer, strip.Renderer);
    });

    [Fact]
    public void Toolstrip_keeps_renderer_when_colors_are_unchanged() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var strip = new ToolStrip();
        provider.SetClasses(strip, "bg-gray-50 hover:text-gray-800");
        var renderer = strip.Renderer;

        provider.Refresh(strip);
        Assert.Same(renderer, strip.Renderer);

        provider.DarkMode = true; // renkler değişmez: dark: varyantı yok
        Assert.Same(renderer, strip.Renderer);

        provider.SetClasses(strip, "bg-gray-900");
        Assert.NotSame(renderer, strip.Renderer);
    });

    [Fact]
    public void Dark_toolstrip_derives_hover_colors_from_its_own_background() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var dark = new ToolStrip();
        provider.SetClasses(dark, "bg-gray-800 text-gray-100");
        var colors = ((TwToolStripRenderer)dark.Renderer).Colors;
        Assert.True(colors.ItemHoverBack.GetBrightness() < 0.5f, $"hover {colors.ItemHoverBack} on dark strip");
        Assert.NotEqual(colors.Back.ToArgb(), colors.ItemHoverBack.ToArgb());
        Assert.Equal(colors.Fore.ToArgb(), colors.ItemHoverFore.ToArgb());

        using var light = new ToolStrip();
        provider.SetClasses(light, "bg-white text-gray-700");
        Assert.Equal(Argb("#f3f4f6"), ((TwToolStripRenderer)light.Renderer).Colors.ItemHoverBack.ToArgb());

        using var custom = new ToolStrip();
        provider.SetClasses(custom, "bg-gray-800 item:hover:bg-blue-600");
        Assert.Equal(Argb("#2563eb"), ((TwToolStripRenderer)custom.Renderer).Colors.ItemHoverBack.ToArgb());
    });

    [Fact]
    public void Dark_mode_themes_native_tooltip_windows_of_the_thread() => Sta.Run(() =>
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var button = new Button();
        form.Controls.Add(button);
        form.Show();
        using var existing = new ToolTip();
        existing.SetToolTip(button, "önce");
        existing.Show("önce", button);
        Application.DoEvents();

        using var provider = new TailwindProvider();
        provider.DarkMode = true;
        Assert.True(TwNativeToolTips.HookInstalled);
        int themedBefore = TwNativeToolTips.Themed.Count;
        Assert.True(themedBefore > 0, "existing tooltip window should be themed");

        using var created = new ToolTip();
        created.SetToolTip(button, "sonra");
        created.Show("sonra", button);
        Application.DoEvents();
        Assert.True(TwNativeToolTips.Themed.Count > themedBefore, "tooltip created in dark mode should be themed");

        provider.DarkToolTips = false;
        Assert.Empty(TwNativeToolTips.Themed);
        Assert.False(TwNativeToolTips.IsDark);
        Assert.False(TwNativeToolTips.HookInstalled, "hook should be removed when no provider is dark");
        form.Close();
    });

    [Fact]
    public void Assigned_context_menu_dropdown_follows_owner_strip_theme() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var strip = new MenuStrip();
        using var shared = new ContextMenuStrip();
        shared.Items.Add("100%");
        var zoom = new ToolStripMenuItem("Zoom") { DropDown = shared };
        strip.Items.Add(zoom);
        using var custom = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer() };
        strip.Items.Add(new ToolStripMenuItem("Özel") { DropDown = custom });
        using var classed = new ContextMenuStrip();
        strip.Items.Add(new ToolStripMenuItem("Class'lı") { DropDown = classed });

        provider.SetClasses(classed, "bg-red-600");
        provider.SetClasses(strip, "bg-gray-50 dark:bg-gray-800 dark:text-gray-100");

        Assert.Same(strip.Renderer, shared.Renderer);
        Assert.IsType<ToolStripProfessionalRenderer>(custom.Renderer);
        Assert.Equal(Argb("#dc2626"), ((TwToolStripRenderer)classed.Renderer).Colors.Back.ToArgb());

        provider.DarkMode = true;
        Assert.Same(strip.Renderer, shared.Renderer);
        Assert.Equal(Argb("#1f2937"), ((TwToolStripRenderer)shared.Renderer).Colors.Back.ToArgb());
        Assert.Equal(Argb("#dc2626"), ((TwToolStripRenderer)classed.Renderer).Colors.Back.ToArgb());
    });
}
