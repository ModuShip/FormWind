using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

public class ApplierTests
{
    private readonly TwTheme _theme = new TwTheme();

    private void Apply(Control control, TwOriginal original, string classes, TwState state = TwState.None, float scale = 1f)
    {
        var parsed = _theme.Parse(classes);
        var context = new TwApplyContext(parsed, state, scale, _theme, original, designMode: false);
        new WinFormsApplier().Apply(control, parsed.Resolve(state), context);
    }

    private static bool IsExplicit(Control control, string property) => TwOriginal.IsExplicit(control, property);

    [Fact]
    public void Explicit_value_checks_do_not_depend_on_TypeDescriptor()
    {
        Assert.True(TwOriginal.UsesDirectChecks);
    }

    [Fact]
    public void Explicit_value_checks_distinguish_inherited_from_assigned()
    {
        using var label = new Label();
        Assert.False(TwOriginal.IsExplicit(label, nameof(Control.ForeColor)));

        label.ForeColor = Color.Red;
        Assert.True(TwOriginal.IsExplicit(label, nameof(Control.ForeColor)));
    }

    [Fact]
    public void Styles_button_and_makes_it_flat()
    {
        using var button = new Button();
        Apply(button, TwOriginal.Capture(button, false), "bg-blue-600 text-white rounded-md");

        Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), button.BackColor.ToArgb());
        Assert.Equal(Color.White.ToArgb(), button.ForeColor.ToArgb());
        Assert.Equal(FlatStyle.Flat, button.FlatStyle);
        Assert.NotNull(button.Region);
    }

    [Fact]
    public void Removing_classes_restores_inherited_values()
    {
        using var button = new Button();
        var original = TwOriginal.Capture(button, false);

        Apply(button, original, "bg-blue-600 text-white rounded-md");
        Apply(button, original, "");

        Assert.False(IsExplicit(button, nameof(Control.BackColor)));
        Assert.False(IsExplicit(button, nameof(Control.ForeColor)));
        Assert.Equal(FlatStyle.Standard, button.FlatStyle);
        Assert.Null(button.Region);
    }

    [Fact]
    public void Leaving_a_state_restores_explicit_original_value()
    {
        using var label = new Label { BackColor = Color.Yellow };
        var original = TwOriginal.Capture(label, false);

        Apply(label, original, "hover:bg-red-500", TwState.Hover);
        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(), label.BackColor.ToArgb());

        Apply(label, original, "hover:bg-red-500", TwState.None);
        Assert.Equal(Color.Yellow.ToArgb(), label.BackColor.ToArgb());
    }

    [Fact]
    public void Untouched_properties_are_left_alone()
    {
        using var label = new Label();
        var original = TwOriginal.Capture(label, false);
        Apply(label, original, "p-2");

        label.BackColor = Color.Pink; // uygulama kodunun ataması
        Apply(label, original, "p-2", TwState.Hover);

        Assert.Equal(Color.Pink.ToArgb(), label.BackColor.ToArgb());
    }

    [Fact]
    public void Applies_font_size_in_points()
    {
        using var label = new Label();
        Apply(label, TwOriginal.Capture(label, false), "text-sm font-bold");

        Assert.True(label.Font.Bold);
        Assert.Equal(10.5f, label.Font.SizeInPoints, 2);
    }

    [Fact]
    public void Scales_spacing_with_dpi()
    {
        using var panel = new Panel();
        Apply(panel, TwOriginal.Capture(panel, false), "px-4 py-2", scale: 1.5f);

        Assert.Equal(new Padding(24, 12, 24, 12), panel.Padding);
    }

    [Fact]
    public void Provider_defers_styling_until_EndInit()
    {
        using var provider = new TailwindProvider();
        using var label = new Label();

        provider.BeginInit();
        provider.SetClasses(label, "bg-red-500");
        Assert.False(IsExplicit(label, nameof(Control.BackColor)));

        provider.EndInit();
        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(), label.BackColor.ToArgb());
        Assert.Equal("bg-red-500", provider.GetClasses(label));
    }

    [Fact]
    public void Provider_applies_dark_variants()
    {
        using var provider = new TailwindProvider();
        using var label = new Label();
        provider.SetClasses(label, "bg-white dark:bg-gray-900");

        provider.DarkMode = true;
        Assert.Equal(ColorTranslator.FromHtml("#111827").ToArgb(), label.BackColor.ToArgb());

        provider.DarkMode = false;
        Assert.Equal(Color.White.ToArgb(), label.BackColor.ToArgb());
    }

    [Fact]
    public void Provider_reports_unknown_classes()
    {
        using var provider = new TailwindProvider();
        using var label = new Label();
        TwUnknownClassesEventArgs? reported = null;
        provider.UnknownClasses += (_, e) => reported = e;

        provider.SetClasses(label, "bg-white not-a-class");

        Assert.NotNull(reported);
        Assert.Equal(new[] { "not-a-class" }, reported!.Unknown);
    }

    [Fact]
    public void Provider_can_disable_dpi_scaling()
    {
        using var provider = new TailwindProvider { ScaleWithDpi = false };
        using var panel = new Panel();
        provider.SetClasses(panel, "px-4");

        Assert.Equal(16, panel.Padding.Left);
    }

    [Fact]
    public void Owner_draw_controls_receive_chrome_instead_of_region()
    {
        using var panel = new TwPanel();
        Apply(panel, TwOriginal.Capture(panel, false), "rounded-lg border", scale: 2f);

        Assert.Null(panel.Region);
        Assert.Equal(16f, panel.CornerRadius);
        Assert.Equal(2f, panel.BorderWidth);
    }

    [Fact]
    public void Color_set_by_app_after_apply_survives_state_changes()
    {
        using var button = new Button();
        var original = TwOriginal.Capture(button, false);
        const string classes = "bg-white hover:bg-gray-100 dark:bg-gray-800 text-gray-700";
        Apply(button, original, classes);

        button.BackColor = Color.LightGreen; // örn. durum rengi
        Apply(button, original, classes, TwState.Hover);
        Assert.Equal(Color.LightGreen.ToArgb(), button.BackColor.ToArgb());

        Apply(button, original, classes, TwState.Dark);
        Assert.Equal(Color.LightGreen.ToArgb(), button.BackColor.ToArgb());
        Assert.True(WinFormsApplier.IsColorOverridden(button, back: true));

        // Diğer kanal FormWind'de kalır.
        Assert.False(WinFormsApplier.IsColorOverridden(button, back: false));
        Assert.Equal(ColorTranslator.FromHtml("#374151").ToArgb(), button.ForeColor.ToArgb());
    }

    // Dönüştürme aracı Git Extensions'ta denenirken bulundu: %125'te p-2 (8 px) önce FormWind'le 10 px, sonra formun
    // AutoScale'iyle 12 px oluyordu.
    [Fact]
    public void Classes_are_reapplied_after_the_form_auto_scales() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var panel = new Panel();
        form.Controls.Add(panel);
        provider.SetClasses(panel, "p-2");
        var applied = panel.Padding;

        form.Scale(new SizeF(1.25f, 1.25f)); // InitializeComponent sonundaki AutoScale'in yaptığı
        Assert.Equal(applied, panel.Padding);

        form.Show(); // handle oluşur
        Assert.Equal(applied, panel.Padding);
        form.Close();
    });

    // Kendi AutoScaleMode'u olan iç içe UserControl'ler handle oluştuktan sonra da ölçeklenebilir (Git Extensions'ta
    // InteractiveGitActionControl).
    [Fact]
    public void Padding_and_margin_survive_scaling_after_the_handle_exists() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var host = new UserControl();
        var panel = new Panel();
        host.Controls.Add(panel);
        form.Controls.Add(host);
        form.Show();
        provider.SetClasses(panel, "p-2 m-1");
        var padding = panel.Padding;
        var margin = panel.Margin;

        host.Scale(new SizeF(1.25f, 1.25f));

        Assert.Equal(padding, panel.Padding);
        Assert.Equal(margin, panel.Margin);
        form.Close();
    });

    [Fact]
    public void Class_padding_wins_over_runtime_assignments_until_classes_are_removed()
    {
        using var provider = new TailwindProvider();
        using var panel = new Panel();
        provider.SetClasses(panel, "p-2");
        var padding = panel.Padding;

        panel.Padding = new Padding(20);
        Assert.Equal(padding, panel.Padding);

        provider.SetClasses(panel, "");
        panel.Padding = new Padding(20);
        Assert.Equal(new Padding(20), panel.Padding);
    }

    [Fact]
    public void Watching_padding_keeps_fixed_button_fitting()
    {
        using var provider = new TailwindProvider();
        using var button = new Button { Text = "OK", Size = new Size(84, 25) };
        provider.SetClasses(button, "px-4 py-2 text-sm");
        var fitted = button.Padding;

        button.Padding = new Padding(0);   // geri alınır: sığdırılmış değere döner
        Assert.Equal(fitted, button.Padding);

        button.Height = 60;                // büyüyünce istenen değer
        Assert.Equal(new Padding(16, 8, 16, 8), button.Padding);
    }

    [Theory]
    [InlineData(2, 1.25f, 3)]
    [InlineData(8, 1.25f, 10)]
    [InlineData(3, 1.5f, 5)]
    [InlineData(4, 1f, 4)]
    public void Spacing_scales_with_midpoint_rounding_away_from_zero(int pixels, float scale, int expected) =>
        Assert.Equal(expected, TwSides.Scale(pixels, scale));

    // ScottPlot demosunda bulundu: imleç görünmesin diye devre dışı bırakılan RichTextBox koyu temada açık zeminde açık
    // yazıyla çiziliyordu (Windows devre dışı RichTextBox'ta zemin rengine uymaz, yazı rengine uyar).
    [Fact]
    public void Disabled_rich_text_box_keeps_windows_colors_unless_disabled_variants()
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var box = new RichTextBox();
        form.Controls.Add(box);
        provider.SetClasses(box, "bg-gray-900 text-gray-100");

        box.Enabled = false;
        Assert.False(IsExplicit(box, nameof(Control.BackColor)));
        Assert.False(IsExplicit(box, nameof(Control.ForeColor)));

        box.Enabled = true;
        Assert.Equal(ColorTranslator.FromHtml("#111827").ToArgb(), box.BackColor.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#f3f4f6").ToArgb(), box.ForeColor.ToArgb());

        provider.SetClasses(box, "bg-gray-900 text-gray-100 disabled:bg-gray-800 disabled:text-gray-400");
        box.Enabled = false;
        Assert.Equal(ColorTranslator.FromHtml("#1f2937").ToArgb(), box.BackColor.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#9ca3af").ToArgb(), box.ForeColor.ToArgb());
    }

    // KeePass'te görüldü: devre dışı arama kutusu koyu temada beyaz kalıyordu. TextBox ve MaskedTextBox devre dışıyken de
    // zemini izler (yazıyı sistem grisiyle çizer); class'ların renkleri korunur.
    [Theory]
    [InlineData(typeof(TextBox))]
    [InlineData(typeof(MaskedTextBox))]
    public void Disabled_text_boxes_keep_the_class_background(Type type)
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var box = (TextBoxBase)Activator.CreateInstance(type)!;
        form.Controls.Add(box);
        provider.SetClasses(box, "bg-gray-900 text-gray-100");

        box.Enabled = false;
        Assert.Equal(ColorTranslator.FromHtml("#111827").ToArgb(), box.BackColor.ToArgb());
        box.Enabled = true;
        Assert.Equal(ColorTranslator.FromHtml("#111827").ToArgb(), box.BackColor.ToArgb());

        provider.SetClasses(box, "bg-gray-900 text-gray-100 disabled:bg-gray-800");
        box.Enabled = false;
        Assert.Equal(ColorTranslator.FromHtml("#1f2937").ToArgb(), box.BackColor.ToArgb());
    }

    [Fact]
    public void Disabled_owner_drawn_controls_keep_class_colors()
    {
        using var provider = new TailwindProvider();
        using var button = new Button();
        provider.SetClasses(button, "bg-gray-900 text-gray-100");

        button.Enabled = false;

        Assert.Equal(ColorTranslator.FromHtml("#111827").ToArgb(), button.BackColor.ToArgb());
    }

    // Değişmez testlerinin (InvariantTests) bulduğu hataların regresyonları.

    [Fact]
    public void Removing_hidden_shows_the_control_again()
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var label = new Label();
        form.Controls.Add(label);
        form.Show();

        provider.SetClasses(label, "hidden");
        Assert.False(label.Visible);
        provider.SetClasses(label, "text-sm");
        Assert.True(label.Visible);
        form.Close();
    }

    [Fact]
    public void Removing_classes_restores_the_original_toolstrip_renderer()
    {
        using var provider = new TailwindProvider();
        using var strip = new ToolStrip();
        var original = strip.Renderer.GetType();

        provider.SetClasses(strip, "bg-gray-800");
        Assert.IsType<TwToolStripRenderer>(strip.Renderer);
        provider.SetClasses(strip, "");

        Assert.Equal(original, strip.Renderer.GetType());
    }

    [Fact]
    public void Transparent_background_on_grid_does_not_throw()
    {
        using var provider = new TailwindProvider();
        using var grid = new DataGridView();
        var background = grid.BackgroundColor;

        provider.SetClasses(grid, "bg-transparent");

        Assert.Equal(background.ToArgb(), grid.BackgroundColor.ToArgb());
    }

    [Fact]
    public void Removing_padding_from_autosize_checkbox_restores_it()
    {
        using var provider = new TailwindProvider();
        using var check = new CheckBox { Text = "Aktif", AutoSize = true };

        provider.SetClasses(check, "px-4 py-2");
        provider.SetClasses(check, "");

        Assert.Equal(Padding.Empty, check.Padding);
    }

    [Fact]
    public void Fixed_button_with_only_horizontal_padding_does_not_divide_by_zero()
    {
        using var provider = new TailwindProvider();
        using var button = new Button { Text = "Kaydet", Size = new Size(30, 12) };

        provider.SetClasses(button, "px-4");
        button.Font = new Font("Segoe UI", 14f);

        Assert.Equal(0, button.Padding.Vertical);
    }

    [Fact]
    public void Color_set_by_app_before_formwind_touches_the_channel_is_kept()
    {
        using var provider = new TailwindProvider();
        using var panel = new Panel();
        provider.SetClasses(panel, "dark:bg-gray-800"); // açık temada arka plana dokunulmaz

        panel.BackColor = Color.LightGreen;
        provider.DarkMode = true;

        Assert.Equal(Color.LightGreen.ToArgb(), panel.BackColor.ToArgb());
    }

    [Fact]
    public void Class_color_equal_to_parent_color_does_not_follow_parent_changes()
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var strip = new ToolStrip();
        form.Controls.Add(strip);
        provider.SetClasses(form, "bg-gray-50 dark:bg-gray-900");
        provider.SetClasses(strip, "bg-gray-50 dark:bg-gray-800"); // açık temada ebeveynle aynı renk

        provider.DarkMode = true;

        Assert.False(WinFormsApplier.IsColorOverridden(strip, back: true));
        Assert.Equal(ColorTranslator.FromHtml("#1f2937").ToArgb(), strip.BackColor.ToArgb());

        provider.DarkMode = false;
        form.BackColor = Color.Red; // ebeveyn değişse de class rengi kalır
        Assert.Equal(ColorTranslator.FromHtml("#f9fafb").ToArgb(), strip.BackColor.ToArgb());
    }

    [Fact]
    public void Inherited_color_change_is_not_treated_as_app_override()
    {
        using var panel = new Panel { BackColor = Color.White };
        var label = new Label();
        panel.Controls.Add(label);
        var original = TwOriginal.Capture(label, false);
        const string classes = "hover:bg-blue-100";

        Apply(label, original, classes, TwState.Hover);
        Apply(label, original, classes); // kalıtıma döner
        panel.BackColor = Color.Black; // örn. ebeveyn koyu temaya geçti
        Apply(label, original, classes, TwState.Hover);

        Assert.False(WinFormsApplier.IsColorOverridden(label, back: true));
        Assert.Equal(ColorTranslator.FromHtml("#dbeafe").ToArgb(), label.BackColor.ToArgb());
    }

    [Fact]
    public void Explicit_color_set_while_inherited_is_treated_as_app_override()
    {
        using var label = new Label();
        var original = TwOriginal.Capture(label, false);
        const string classes = "hover:bg-blue-100";

        Apply(label, original, classes, TwState.Hover);
        Apply(label, original, classes);
        label.BackColor = Color.LightGreen;
        Apply(label, original, classes, TwState.Hover);

        Assert.Equal(Color.LightGreen.ToArgb(), label.BackColor.ToArgb());
    }

    [Fact]
    public void Running_transition_is_not_treated_as_app_override() => Sta.Run(() =>
    {
        using var button = new Button();
        _ = button.Handle;
        var original = TwOriginal.Capture(button, false);
        const string classes = "transition bg-white hover:bg-black";

        Apply(button, original, classes);
        Apply(button, original, classes, TwState.Hover);
        Assert.True(TwAnimator.IsAnimating(button, TwColorChannel.Back));
        Apply(button, original, classes); // geçiş ortasında durum değişti
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(1));

        Assert.False(WinFormsApplier.IsColorOverridden(button, back: true));
        Assert.Equal(Color.White.ToArgb(), button.BackColor.ToArgb());
    });

    [Fact]
    public void Assigning_new_classes_takes_color_back_from_app()
    {
        using var provider = new TailwindProvider();
        using var button = new Button();
        provider.SetClasses(button, "bg-white");
        button.BackColor = Color.LightGreen;
        provider.Refresh(button);
        Assert.Equal(Color.LightGreen.ToArgb(), button.BackColor.ToArgb());

        provider.SetClasses(button, "bg-red-500");

        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(), button.BackColor.ToArgb());
    }

    [Fact]
    public void Fixed_height_button_trims_vertical_padding_so_text_fits()
    {
        using var button = new Button { Text = "OK", Size = new Size(84, 25) };
        Apply(button, TwOriginal.Capture(button, false), "px-4 py-2 text-sm");

        Assert.Equal(16, button.Padding.Left);
        Assert.Equal(16, button.Padding.Right);
        Assert.True(button.ClientSize.Height - button.Padding.Vertical >= button.Font.Height,
            $"padding {button.Padding} leaves no room for text height {button.Font.Height}");
    }

    [Fact]
    public void Narrow_button_trims_horizontal_padding_so_text_stays_on_one_line()
    {
        using var button = new Button { Text = "Discard", Size = new Size(70, 40) };
        Apply(button, TwOriginal.Capture(button, false), "px-6 py-1 text-sm font-semibold");

        var text = TextRenderer.MeasureText(button.Text, button.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
        Assert.True(button.ClientSize.Width - button.Padding.Horizontal >= text.Width,
            $"padding {button.Padding} leaves less than text width {text.Width}");
        Assert.Equal(4, button.Padding.Top);
    }

    [Fact]
    public void Trimmed_padding_returns_when_button_grows()
    {
        using var button = new Button { Text = "OK", Size = new Size(84, 25) };
        Apply(button, TwOriginal.Capture(button, false), "px-4 py-2 text-sm");
        Assert.True(button.Padding.Vertical < 16);

        button.Height = 60;

        Assert.Equal(new Padding(16, 8, 16, 8), button.Padding);
    }

    [Fact]
    public void Padding_is_not_trimmed_when_it_fits_or_control_autosizes()
    {
        using var tall = new Button { Text = "OK", Size = new Size(84, 60) };
        Apply(tall, TwOriginal.Capture(tall, false), "px-4 py-2 text-sm");
        Assert.Equal(new Padding(16, 8, 16, 8), tall.Padding);

        using var auto = new Button { Text = "OK", AutoSize = true, Size = new Size(84, 25) };
        Apply(auto, TwOriginal.Capture(auto, false), "px-4 py-2 text-sm");
        Assert.Equal(new Padding(16, 8, 16, 8), auto.Padding);
    }

    // Native düz buton devre dışıyken ForeColor'ı yok sayar, yazıyı zeminin koyu tonuyla çizer (koyu zeminde görünmez).
    // 25 px: btn'in py-2'si sığmaz ve kısılır, yazı alanı yazıdan alçak kalır.
    [Theory]
    [InlineData(32)]
    [InlineData(25)]
    public void Disabled_native_button_draws_text_with_class_color_in_the_same_place(int height) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var button = new Button { Text = "Save", Size = new Size(100, height), Location = new Point(10, 10) };
        form.Controls.Add(button);
        provider.SetClasses(button, "btn-ghost");
        form.Show();

        Rectangle TextBox(out int textPixels, out Color brightest)
        {
            using var bitmap = new Bitmap(button.Width, button.Height);
            button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, button.Size));
            var back = button.BackColor;
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
            textPixels = 0;
            brightest = back;
            for (int x = 3; x < bitmap.Width - 3; x++)
                for (int y = 3; y < bitmap.Height - 3; y++)
                {
                    var c = bitmap.GetPixel(x, y);
                    if (Math.Abs(c.R - back.R) + Math.Abs(c.G - back.G) + Math.Abs(c.B - back.B) < 60) continue;
                    textPixels++;
                    if (c.GetBrightness() > brightest.GetBrightness()) brightest = c;
                    left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        var enabled = TextBox(out _, out _);
        button.Enabled = false;
        var disabled = TextBox(out int pixels, out var brightest);

        Assert.True(pixels > 20, "devre dışı yazı zeminden ayırt edilmeli");
        Assert.True(brightest.GetBrightness() > button.BackColor.GetBrightness(), "koyu zeminde yazı zeminden açık olmalı");
        // Soluk yazının kenar pikselleri eşiğin altında kalabilir; kutular değil merkezler karşılaştırılır.
        float dx = (disabled.Left + disabled.Right - enabled.Left - enabled.Right) / 2f;
        float dy = (disabled.Top + disabled.Bottom - enabled.Top - enabled.Bottom) / 2f;
        Assert.True(Math.Abs(dx) <= 1.5f && Math.Abs(dy) <= 1.5f, $"etkin {enabled}, devre dışı {disabled}");
        form.Close();
    });
}
