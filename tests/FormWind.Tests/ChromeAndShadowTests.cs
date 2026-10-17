using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class ChromeAndShadowTests
{
    private readonly TwTheme _theme = new TwTheme();

    private TwStyle Base(string classes) => _theme.Parse(classes).Resolve(TwState.None);

    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    [Fact]
    public void Opacity_modifier_applies_to_every_color_utility()
    {
        var style = Base("bg-black/50 text-white/25 border-blue-500/100 ring-[#ff0000]/10");

        Assert.Equal(128, style.BackColor!.Value.A);
        Assert.Equal(64, style.ForeColor!.Value.A);
        Assert.Equal(255, style.BorderColor!.Value.A);
        Assert.Equal(26, style.RingColor!.Value.A);
        Assert.Contains("bg-white/101", _theme.Parse("bg-white/101").Unknown);
    }

    [Fact]
    public void Parses_side_borders()
    {
        var style = Base("border-b border-x-2 border-gray-200");

        Assert.Null(style.BorderWidth);
        Assert.Equal(1, style.BorderSides.Bottom);
        Assert.Equal(2, style.BorderSides.Left);
        Assert.Equal(2, style.BorderSides.Right);
        Assert.Null(style.BorderSides.Top);
        Assert.True(style.HasChrome);
    }

    [Fact]
    public void Chrome_combines_all_and_side_widths_with_dpi()
    {
        var chrome = TwChrome.FromStyle(Base("border border-b-4 rounded-md"), 1.5f, Color.Gray);

        Assert.Equal(1.5f, chrome.BorderTop);
        Assert.Equal(6f, chrome.BorderBottom);
        Assert.Equal(9f, chrome.Radius);
        Assert.False(chrome.IsUniformBorder);
    }

    [Theory]
    [InlineData("shadow-sm", TwShadowSize.Sm)]
    [InlineData("shadow", TwShadowSize.Base)]
    [InlineData("shadow-md", TwShadowSize.Md)]
    [InlineData("shadow-2xl", TwShadowSize.Xl2)]
    [InlineData("shadow-none", TwShadowSize.None)]
    public void Parses_shadow_sizes(string classes, TwShadowSize expected) =>
        Assert.Equal(expected, Base(classes).Shadow);

    [Fact]
    public void Shadow_presets_follow_tailwind_and_accept_a_color()
    {
        var md = TwShadowLayer.Preset(TwShadowSize.Md, null);
        Assert.Equal(2, md.Length);
        Assert.Equal(new TwShadowLayer(4, 6, -1, Color.FromArgb(26, 0, 0, 0)), md[0]);

        var colored = TwShadowLayer.Preset(TwShadowSize.Lg, Color.FromArgb(128, 59, 130, 246));
        Assert.All(colored, l => Assert.Equal(Color.FromArgb(128, 59, 130, 246).ToArgb(), l.Color.ToArgb()));
    }

    [Fact]
    public void Ring_defaults_to_three_pixels_and_tailwind_blue()
    {
        var spec = TwOutsetSpec.FromStyle(Base("ring"), 1f);

        Assert.Equal(3f, spec.RingWidth);
        Assert.Equal(TwOutsetSpec.DefaultRingColor.ToArgb(), spec.RingColor.ToArgb());
        Assert.Equal(2f, TwOutsetSpec.FromStyle(Base("ring-2 ring-offset-2"), 1f).RingWidth);
        Assert.True(TwOutsetSpec.FromStyle(Base("ring-2 ring-offset-2"), 1f).Extent >= 4);
    }

    [Fact]
    public void Focus_ring_is_applied_only_in_focus_state()
    {
        using var textBox = new TwTextBox();
        var parsed = _theme.Parse("focus:ring-2 focus:ring-blue-500");
        var original = TwOriginal.Capture(textBox, false);
        var applier = new WinFormsApplier();

        applier.Apply(textBox, parsed.Resolve(TwState.Focus), new TwApplyContext(parsed, TwState.Focus, 1f, _theme, original, false));
        Assert.Equal(2f, TwOutset.For(textBox).Spec!.RingWidth);

        applier.Apply(textBox, parsed.Resolve(TwState.None), new TwApplyContext(parsed, TwState.None, 1f, _theme, original, false));
        Assert.Null(TwOutset.For(textBox).Spec);
    }

    [Fact]
    public void Parent_paints_a_blurred_shadow_below_the_child() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var parent = new Panel { Size = new Size(200, 200), BackColor = Color.White };
        var card = new TwPanel { Bounds = new Rectangle(50, 40, 100, 80) };
        parent.Controls.Add(card);
        provider.SetClasses(card, "bg-white rounded-lg shadow-xl");

        using var bitmap = new Bitmap(200, 200);
        parent.DrawToBitmap(bitmap, new Rectangle(0, 0, 200, 200));

        float Brightness(int x, int y) => bitmap.GetPixel(x, y).GetBrightness();
        // Kartın hemen altı gölgeli, uzağı beyaz; gölge alta doğru yumuşakça açılır (bulanık).
        Assert.True(Brightness(100, 125) < 0.97f, $"kart altı {Brightness(100, 125)}");
        Assert.True(Brightness(100, 125) < Brightness(100, 150));
        Assert.Equal(1f, Brightness(100, 195), 2);
        Assert.Equal(1f, Brightness(5, 5), 2);
    });

    [Fact]
    public void Side_border_is_drawn_only_on_that_side() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var panel = new TwPanel { Size = new Size(100, 40), BackColor = Color.White };
        provider.SetClasses(panel, "bg-white border-b-2 border-black");

        using var bitmap = new Bitmap(100, 40);
        panel.DrawToBitmap(bitmap, new Rectangle(0, 0, 100, 40));

        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(50, 39).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(50, 0).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(0, 20).ToArgb());
    });

    [Fact]
    public void Native_text_box_border_is_drawn_around_it_and_restored() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var parent = new Panel { Size = new Size(200, 80), BackColor = Color.LightGray };
        var textBox = new TextBox { Bounds = new Rectangle(50, 30, 100, 20) };
        parent.Controls.Add(textBox);
        provider.SetClasses(textBox, "bg-white border border-red-600 rounded-md");

        Assert.Equal(BorderStyle.None, textBox.BorderStyle);
        Assert.Equal(new Padding(4), TwOutset.For(textBox).Spec!.FrameInset);   // 3 px boşluk + 1 px kenarlık

        using var bitmap = new Bitmap(200, 80);
        parent.DrawToBitmap(bitmap, new Rectangle(0, 0, 200, 80));
        Assert.Equal(Argb("#dc2626"), bitmap.GetPixel(46, 40).ToArgb());          // kenarlık
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(48, 40).ToArgb());     // zeminle dolu boşluk
        Assert.Equal(Color.LightGray.ToArgb(), bitmap.GetPixel(44, 40).ToArgb());
        Assert.Equal(Color.LightGray.ToArgb(), bitmap.GetPixel(46, 26).ToArgb()); // yuvarlak köşe

        provider.SetClasses(textBox, "");
        Assert.Equal(BorderStyle.Fixed3D, textBox.BorderStyle);
        Assert.Null(TwOutset.For(textBox).Spec);
    });

    [Fact]
    public void Drop_down_list_combo_box_is_painted_with_border_and_corners() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { BackColor = Color.LightGray, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(10, 10, 160, 30) };
        combo.Items.AddRange(new object[] { "İstanbul", "İzmir" });
        combo.SelectedIndex = 0;
        form.Controls.Add(combo);
        form.Show();
        provider.SetClasses(combo, "bg-white text-gray-900 border border-red-600 rounded-md");
        Assert.True(TwComboPainter.IsAttached(combo));

        using (var bitmap = new Bitmap(combo.Width, combo.Height))
        {
            combo.DrawToBitmap(bitmap, new Rectangle(Point.Empty, combo.Size));
            Assert.Equal(Argb("#dc2626"), bitmap.GetPixel(0, combo.Height / 2).ToArgb());      // kenarlık
            Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(combo.Width / 2, 2).ToArgb());  // zemin
            Assert.Equal(Color.LightGray.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());           // yuvarlak köşe
        }

        // Düzenlenebilir kutu native kalır.
        combo.DropDownStyle = ComboBoxStyle.DropDown;
        provider.Refresh(combo);
        Assert.False(TwComboPainter.IsAttached(combo));

        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        provider.Refresh(combo);
        Assert.True(TwComboPainter.IsAttached(combo));
        provider.SetClasses(combo, "");
        Assert.False(TwComboPainter.IsAttached(combo));
        form.Close();
    });

    // TextAlign değişimi handle'ı yeniden oluşturur; bu sırada gelen odak olayıyla stil iç içe uygulanıp BorderStyle de
    // değişirse WinForms yönetilen nesnesi olmayan bir pencere bırakıyordu ve çöp toplamadan sonra süreç düşüyordu
    // (InvariantTests seed 105'ten indirgenmiş senaryo).
    [Fact]
    public void Text_box_frame_survives_handle_recreation_during_focus_changes() => Sta.Run(() =>
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var parent = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(parent);
        var textBox = new TextBox { Text = "Ayşe" };
        parent.Controls.Add(textBox);
        form.Show();
        using var provider = new TailwindProvider();

        provider.SetClasses(textBox, "input disabled:text-gray-500 text-center");
        Assert.Equal(BorderStyle.None, textBox.BorderStyle);
        Assert.Equal(HorizontalAlignment.Center, textBox.TextAlign);
        Assert.NotNull(TwOutset.For(textBox).Spec?.Frame);

        textBox.ForeColor = Color.Red;
        parent.BackColor = Color.Green;
        provider.SetClasses(textBox, "text-sm text-gray-700 font-semibold bg-blue-600");
        Assert.Equal(BorderStyle.Fixed3D, textBox.BorderStyle);
        provider.SetClasses(textBox, "bg-transparent text-center bg-gray-50");
        RecreateHandle.Invoke(textBox, null);
        provider.SetClasses(textBox, "");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Application.DoEvents();
    });

    private static readonly System.Reflection.MethodInfo RecreateHandle =
        typeof(Control).GetMethod("RecreateHandle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    // transition varken gölge değişimi eski ve yeni gölgenin saydamlıkla geçişiyle olur; transition yoksa anında.
    [Fact]
    public void Shadow_changes_transition_when_transition_is_set() => Sta.Run(() =>
    {
        var previous = TwMotion.Enabled;
        TwMotion.Enabled = true;
        try
        {
            using var provider = new TailwindProvider();
            using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(300, 200), BackColor = Color.White };
            var card = new TwPanel { Bounds = new Rectangle(50, 30, 200, 100), BackColor = Color.White };
            var instant = new TwPanel { Bounds = new Rectangle(10, 10, 20, 20) };
            form.Controls.Add(card);
            form.Controls.Add(instant);
            provider.SetClasses(card, "shadow-none dark:shadow-xl transition duration-300");
            provider.SetClasses(instant, "shadow-none dark:shadow-xl");
            form.Show();

            provider.DarkMode = true;
            var outset = TwOutset.For(card);
            Assert.True(outset.Animating);
            Assert.False(TwOutset.For(instant).Animating);

            // Başlangıçta yeni gölge henüz görünmez (eski gölge yoktu); kartın 20 px altı boş.
            int Below()
            {
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.White);
                    outset.Paint(g);
                }
                return bitmap.GetPixel(150, card.Bottom + 20).R;
            }
            Assert.True(Below() > 250);

            var until = DateTime.UtcNow.AddMilliseconds(600);
            while (outset.Animating && DateTime.UtcNow < until)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(10);
            }
            Assert.False(outset.Animating);
            Assert.True(Below() < 250);                                   // xl gölge kartın altına taşar
            form.Close();
        }
        finally
        {
            TwMotion.Enabled = previous;
        }
    });
}
