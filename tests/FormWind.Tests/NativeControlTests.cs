using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// Renkleri yok sayan standart kontroller: TabControl sekmeleri FormWind'in çizimine, ProgressBar klasik çubuğa geçer.
[Collection(UiCollection.Name)]
public class NativeControlTests
{
    private static Color Color(string hex) => ColorTranslator.FromHtml(hex);

    [Fact]
    public void Tab_control_colors_are_painted_without_changing_the_draw_mode() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var tabs = new TabControl { TabPages = { new TabPage("Genel"), new TabPage("Yükleme") } };
        form.Controls.Add(tabs);

        provider.SetClasses(tabs, "text-gray-600 selected:bg-white selected:text-gray-900 selected:border-blue-600");
        Assert.Equal(TabDrawMode.Normal, tabs.DrawMode);                                  // handle yeniden oluşmaz
        Assert.True(TabControlApplier.IsPainted(tabs));
        Assert.Empty(TwContrast.FindOwnerDrawn(form));                                    // FormWind çiziyor

        provider.SetClasses(tabs, "");
        Assert.Equal(TabDrawMode.Normal, tabs.DrawMode);
        Assert.False(TabControlApplier.IsPainted(tabs));

        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;                                       // uygulamanın kendi çizimi
        provider.SetClasses(tabs, "selected:bg-white");
        Assert.False(TabControlApplier.IsPainted(tabs));                                   // ezilmez
        Assert.Single(TwContrast.FindOwnerDrawn(form));
    });

    [Fact]
    public void Tab_control_background_class_colors_the_tabs_not_the_pages() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var page = new TabPage("Genel");
        var tabs = new TabControl { TabPages = { page } };
        form.Controls.Add(tabs);
        var before = tabs.BackColor;

        provider.SetClasses(tabs, "bg-gray-800 text-gray-100");

        Assert.Equal(before.ToArgb(), tabs.BackColor.ToArgb());
        Assert.True(TabControlApplier.IsPainted(tabs));
    });

    [Fact]
    public void Auto_style_styles_tab_controls_pages_and_progress_bars() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var page = new TabPage("Genel");
        var tabs = new TabControl { TabPages = { page } };
        var progress = new ProgressBar { Value = 40 };
        page.Controls.Add(progress);
        form.Controls.Add(tabs);
        provider.ContainerControl = form;
        provider.AutoStyle = true;

        Assert.Equal("tab-control", provider.GetAutomaticClasses(tabs));
        Assert.Equal("tab-page", provider.GetAutomaticClasses(page));
        Assert.Equal("range", provider.GetAutomaticClasses(progress));
        Assert.Equal(Color("#ffffff").ToArgb(), page.BackColor.ToArgb());

        provider.DarkMode = true;
        Assert.Equal(Color("#111827").ToArgb(), page.BackColor.ToArgb());
        var colors = ProgressBarApplier.Colors(progress);
        Assert.NotNull(colors);
        Assert.Equal(Color("#3b82f6").ToArgb(), colors!.Value.Bar.ToArgb());               // dark:accent-primary-500
        Assert.Equal(Color("#374151").ToArgb(), colors.Value.Track.ToArgb());              // dark:fill-surface-700
    });

    // KeePass'te görüldü: koyu temada ListView başlığı beyaz kalıyordu. Başlık ItemsView temasını alır, yazısı listenin
    // yazı rengiyle çizilir; açık temada Windows'un çizimine dönülür.
    [Fact]
    public void List_view_header_follows_dark_mode() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var list = new ListView { View = View.Details, Columns = { "Title", "User Name" } };
        var drawn = new ListView { View = View.Details, OwnerDraw = true, Columns = { "Title" } };
        form.Controls.Add(list);
        form.Controls.Add(drawn);
        form.Show();
        provider.SetClasses(list, "list");
        provider.SetClasses(drawn, "list");

        Assert.True(TwNativeTheme.PaintsHeader(list));
        Assert.False(TwNativeTheme.PaintsHeader(drawn));                                   // uygulama kendisi çizer
        provider.DarkMode = false;
        Assert.False(TwNativeTheme.PaintsHeader(list));
        form.Close();
    });

    // KeePass'in Hakkında penceresi: LinkLabel bağlantıları koyu zeminde koyu mavi (2,1:1) kalıyordu.
    [Fact]
    public void Link_label_links_take_the_accent_color_and_the_audit_checks_them() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form();
        var link = new LinkLabel { Text = "KeePass Website" };
        var plain = new LinkLabel { Text = "License" };
        form.Controls.Add(link);
        form.Controls.Add(plain);
        var originalLink = link.LinkColor;
        provider.SetClasses(form, "bg-gray-900 text-gray-100");
        provider.SetClasses(link, "text-gray-200 accent-blue-600 dark:accent-blue-400");
        provider.SetClasses(plain, "text-gray-200");

        Assert.Equal(Color("#60a5fa").ToArgb(), link.LinkColor.ToArgb());
        Assert.Equal(Color("#60a5fa").ToArgb(), link.VisitedLinkColor.ToArgb());
        var issue = Assert.Single(TwContrast.Audit(form, provider));
        Assert.Same(plain, issue.Control);
        Assert.Equal("link", issue.Part);

        provider.SetClasses(link, "text-gray-200");
        Assert.Equal(originalLink.ToArgb(), link.LinkColor.ToArgb());
    });

    [Fact]
    public void Auto_style_gives_link_labels_the_link_label_component() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var link = new LinkLabel { Text = "Help" };
        form.Controls.Add(link);
        provider.ContainerControl = form;
        provider.AutoStyle = true;

        Assert.Equal("link-label", provider.GetAutomaticClasses(link));
        Assert.Equal(Color("#2563eb").ToArgb(), link.LinkColor.ToArgb());                 // primary-600
    });

    [Fact]
    public void Progress_bar_colors_turn_off_the_theme_and_removing_them_restores_it() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var bar = new ProgressBar { Value = 50, Size = new Size(200, 20) };
        form.Controls.Add(bar);
        form.Show();

        provider.SetClasses(bar, "accent-[#ff0000] fill-[#0000ff]");
        Assert.Equal(System.Drawing.Color.Red.ToArgb(), ProgressBarApplier.Colors(bar)!.Value.Bar.ToArgb());
        using (var bitmap = new Bitmap(bar.Width, bar.Height))
        {
            bar.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bar.Size));
            var pixels = Enumerable.Range(2, bar.Width - 4).Select(x => bitmap.GetPixel(x, bar.Height / 2)).ToList();
            Assert.Contains(pixels, p => p.R > 200 && p.G < 60 && p.B < 60);                 // çubuk
            Assert.Contains(pixels, p => p.B > 200 && p.R < 60 && p.G < 60);                 // iz
        }

        provider.SetClasses(bar, "");
        Assert.Null(ProgressBarApplier.Colors(bar));
        form.Close();
    });
}
