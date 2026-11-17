using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Follow-ups of the 0.1.8 and 0.1.9 user feedback reports that were still open after the first round: diagnostics
/// counters, menu-level item variants, sizes under simulated DPI and the defaults of controls without classes.
/// </summary>
[Collection(UiCollection.Name)]
public class FeedbackFollowUpTests
{
    private static Form HostForm(int width = 640, int height = 300) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private sealed class BufferedPanel : Panel
    {
        public BufferedPanel() => DoubleBuffered = true;
    }

    private static (long Blends, long Readbacks) PaintShadowOn(TailwindProvider provider, Panel parent)
    {
        using var form = HostForm();
        parent.Dock = DockStyle.Fill;
        form.Controls.Add(parent);
        var card = new Panel { Bounds = new Rectangle(40, 40, 200, 100) };
        parent.Controls.Add(card);
        provider.SetClasses(card, "bg-white shadow-md");
        form.Show();
        Application.DoEvents();

        long blends = TwDiagnostics.ShadowParentBlends, readbacks = TwDiagnostics.ShadowParentReadbacks;
        using (var picture = new Bitmap(parent.Width, parent.Height)) parent.DrawToBitmap(picture, new Rectangle(Point.Empty, parent.Size));
        return (TwDiagnostics.ShadowParentBlends - blends, TwDiagnostics.ShadowParentReadbacks - readbacks);
    }

    private static Color TextColorOf(TwToolStripRenderer renderer, ToolStripItem item)
    {
        using var bitmap = new Bitmap(100, 20);
        using var g = Graphics.FromImage(bitmap);
        var e = new ToolStripItemTextRenderEventArgs(g, item, item.Text, new Rectangle(0, 0, 100, 20), Color.Black, item.Font, TextFormatFlags.Default);
        renderer.DrawItemText(e);
        return e.TextColor;
    }

    private static Color ForeOf(TailwindProvider provider, string classes) =>
        provider.Theme.Parse(classes).Resolve(TwState.None).ForeColor!.Value;

    [Fact]
    public void Menu_danger_component_colors_an_item_with_the_danger_color_in_both_modes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var archive = new ToolStripMenuItem("Arşivle");
        provider.SetClasses(archive, "menu-danger");

        var light = new TwToolStripRenderer(new TwMenuColors());
        var dark = new TwToolStripRenderer(new TwMenuColors { State = TwState.Dark, Fore = Color.White });
        Assert.Equal(ForeOf(provider, "text-danger-600").ToArgb(), TextColorOf(light, archive).ToArgb());
        Assert.Equal(ForeOf(provider, "text-danger-400").ToArgb(), TextColorOf(dark, archive).ToArgb());
    });

    [Fact]
    public void Menu_header_component_sets_a_small_semibold_font_and_a_theme_can_redefine_it() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var header = new ToolStripMenuItem("Kayıt 42");
        float normal = header.Font.Size;
        provider.SetClasses(header, "menu-header");
        // font-semibold: yarı kalın aile ("Segoe UI Semibold") ya da yoksa kalın stil.
        Assert.True(header.Font.Bold || header.Font.Name.Contains("Semibold"), header.Font.ToString());
        // text-xs (12 px) is 9 pt, the size of the default menu font: the header is never larger than the items.
        Assert.True(header.Font.SizeInPoints <= normal + 0.01f, header.Font.ToString());

        provider.Theme.SetComponent("menu-danger", "text-orange-600");
        using var item = new ToolStripMenuItem("Sil");
        provider.SetClasses(item, "menu-danger");
        Assert.Equal(ForeOf(provider, "text-orange-600").ToArgb(), TextColorOf(new TwToolStripRenderer(new TwMenuColors()), item).ToArgb());
    });

    // 0.1.9 report R6: under Simulate the select box stayed 30 px. A height set in code is not scaled by the simulation
    // (AutoScaleMode does that on a real display); a height given as a class is.
    [Theory]
    [InlineData(96, 30)]
    [InlineData(120, 38)]
    [InlineData(144, 45)]
    public void Select_height_from_a_class_scales_under_simulated_dpi(int dpi, int expected) => Sta.Run(() =>
    {
        using (TwDpi.Simulate(dpi))
        {
            using var provider = new TailwindProvider();
            using var form = HostForm();
            var coded = new TwSelect { Width = 160, Height = 30 };
            var classed = new TwSelect { Width = 160, Top = 50 };
            form.Controls.AddRange(new Control[] { coded, classed });
            provider.SetClasses(coded, "input");
            provider.SetClasses(classed, "input h-[30px]");
            form.Show();
            Application.DoEvents();

            Assert.Equal(expected, classed.Height);
            Assert.Equal(30, coded.Height);
        }
    });

    // 0.1.8 report: a TwSelect without classes had no border and opened a white menu on a dark form.
    [Fact]
    public void Select_without_classes_opens_its_menu_in_its_own_colors() => Sta.Run(() =>
    {
        using var form = HostForm();
        form.BackColor = ColorTranslator.FromHtml("#1f2937");
        form.ForeColor = Color.White;
        var select = new TwSelect { Width = 160 };
        select.Items.AddRange(new[] { "Bir", "İki" });
        form.Controls.Add(select);
        form.Show();
        Application.DoEvents();

        var style = select.MenuStyle;
        Assert.Equal(form.BackColor.ToArgb(), style.Back.ToArgb());
        Assert.Equal(Color.White.ToArgb(), style.Fore.ToArgb());

        using var picture = new Bitmap(select.Width, select.Height);
        select.DrawToBitmap(picture, new Rectangle(Point.Empty, select.Size));
        // The left edge, halfway down: the border, lighter than the dark background.
        Assert.True(picture.GetPixel(0, select.Height / 2).GetBrightness() > form.BackColor.GetBrightness());
    });

    [Fact]
    public void Select_with_classes_keeps_the_menu_colors_of_its_classes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm();
        form.BackColor = ColorTranslator.FromHtml("#1f2937");
        var select = new TwSelect { Width = 160 };
        form.Controls.Add(select);
        provider.SetClasses(select, "menu:bg-red-500");
        form.Show();
        Application.DoEvents();
        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(), select.MenuStyle.Back.ToArgb());
    });

    private static void WaitUntil(Func<bool> condition, int milliseconds = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(5);
        }
    }

    // 0.1.8 report: every transition created a new layered picture window (handle, DWM attributes).
    [Fact]
    public void Cross_fades_on_a_form_reuse_its_picture_window() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(120, 120), ClientSize = new Size(420, 260) };
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        form.Controls.Add(page);
        form.Show();
        form.Activate();
        Application.DoEvents();
        if (!TwMotion.CanAnimate(page)) return;   // animations off on this machine (remote session, setting)

        int completed = 0;
        int active = TwFadeOut.ActiveCount;
        TwTransition.CrossFade(page, () => page.BackColor = Color.Black, new TwMotionOptions { DurationMs = 60, Completed = () => completed++ });
        var first = Application.OpenForms.OfType<TwFadeOut.Ghost>().Single(g => g.InUse && g.Owner == form);
        WaitUntil(() => completed == 1);
        Assert.Equal(1, completed);
        Assert.Equal(active, TwFadeOut.ActiveCount);
        Assert.False(first.Visible);
        Assert.True(TwFadeOut.Ghost.HasIdle(form));

        TwTransition.CrossFade(page, () => page.BackColor = Color.White, new TwMotionOptions { DurationMs = 60, Completed = () => completed++ });
        Assert.True(first.InUse && first.Visible);
        Assert.False(TwFadeOut.Ghost.HasIdle(form));
        WaitUntil(() => completed == 2);
        Assert.Equal(2, completed);

        form.Close();
        Assert.True(first.IsDisposed);
    });

    [Fact]
    public void Shadow_blends_onto_a_parent_that_is_not_double_buffered_count_as_readbacks() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var (blends, readbacks) = PaintShadowOn(provider, new Panel());
        Assert.True(blends > 0);
        Assert.Equal(blends, readbacks);
    });

    [Fact]
    public void Shadow_blends_onto_a_double_buffered_parent_are_not_readbacks() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var (blends, readbacks) = PaintShadowOn(provider, new BufferedPanel());
        Assert.True(blends > 0);
        Assert.Equal(0, readbacks);
    });
}
