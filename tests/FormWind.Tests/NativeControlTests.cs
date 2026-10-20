using System;
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
        Assert.True(TwNativeTheme.PaintsHeader(drawn));                                    // Windows'a bırakılan hücreler
        provider.DarkMode = false;
        Assert.False(TwNativeTheme.PaintsHeader(list));
        form.Close();
    });

    // Görsel stiller kapalıyken (EnableVisualStyles çağrılmamış, uzak masaüstü, klasik tema) koyu ItemsView teması uygulanamaz;
    // başlık klasik açık gri kalır. Yazısı açık renge çevrilseydi okunmazdı (ölçüldü: #F0F0F0 üzerinde 1,04:1); başlığa
    // dokunulmaz, liste kendi renklerini alır.
    [Fact]
    public void List_view_header_is_left_to_windows_without_visual_styles() => Sta.Run(() =>
    {
        TwNativeTheme.ThemesOverride = false;
        try
        {
            using var provider = new TailwindProvider { DarkMode = true };
            using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
            var list = new ListView { View = View.Details, Columns = { "Başlık" } };
            form.Controls.Add(list);
            form.Show();
            provider.SetClasses(list, "list");

            Assert.False(TwNativeTheme.PaintsHeader(list));
            Assert.Equal(Color("#1f2937").ToArgb(), list.BackColor.ToArgb());                   // liste yine koyu
            form.Close();
        }
        finally
        {
            TwNativeTheme.ThemesOverride = true;
        }
    });

    // KeePass'te görüldü: seçenekler penceresi listelere OnLoad'da SetWindowTheme(..., "explorer") veriyor ve FormWind'in
    // koyu temasını eziyordu (grup başlıkları koyu zeminde koyu mavi). Koyu temadaki liste, uygulamanın verdiği temadan sonra
    // koyu temayı geri alır; açık temada uygulamanın teması kalır.
    [Fact]
    public void List_view_keeps_the_dark_theme_when_the_app_sets_its_own() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var list = new ListView { View = View.Details, Columns = { "Seçenek" } };
        form.Controls.Add(list);
        form.Show();
        provider.SetClasses(list, "list");
        var pushes = TwNativeTheme.ListPushes(list);

        SetWindowTheme(list.Handle, "explorer", null);
        Assert.Equal(pushes + 1, TwNativeTheme.ListPushes(list));                       // bir kez geri verilir, döngü yok
        Assert.True(TwNativeTheme.PaintsHeader(list));

        provider.DarkMode = false;
        pushes = TwNativeTheme.ListPushes(list);
        SetWindowTheme(list.Handle, "explorer", null);
        Assert.Equal(pushes, TwNativeTheme.ListPushes(list));                           // açık temada dokunulmaz
        form.Close();
    });

    // KeePass grup ağacı: seçili düğüm koyu zeminde açık temanın koyu yazısıyla çiziliyordu.
    [Fact]
    public void Tree_view_keeps_the_dark_theme_when_the_app_sets_its_own() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var tree = new TreeView { Nodes = { "test" } };
        form.Controls.Add(tree);
        form.Show();
        provider.SetClasses(tree, "list");
        var pushes = TwNativeTheme.TreePushes(tree);

        SetWindowTheme(tree.Handle, "explorer", null);
        Assert.Equal(pushes + 1, TwNativeTheme.TreePushes(tree));

        provider.DarkMode = false;
        pushes = TwNativeTheme.TreePushes(tree);
        SetWindowTheme(tree.Handle, "explorer", null);
        Assert.Equal(pushes, TwNativeTheme.TreePushes(tree));
        form.Close();
    });

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    // Bulk Crap Uninstaller'da (ObjectListView) görüldü: owner-draw listenin başlığı koyu temaya geçiyor ama yazısı temanın
    // koyu rengiyle kalıyordu. Uygulama hücreyi Windows'a bıraktığında (DrawDefault, ObjectListView'ün varsayılanı) yazı
    // listenin yazı rengini alır; kendisi çizdiği hücrelere dokunulmaz. Test süreci görsel stilsiz çalıştığından başlığın
    // NM_CUSTOMDRAW bildirimi doğrudan gönderilir.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Owner_drawn_list_view_headers_left_to_windows_take_the_text_color_in_dark_mode(bool drawDefault) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(300, 120) };
        var list = new ListView { View = View.Details, OwnerDraw = true, Dock = DockStyle.Fill, Columns = { { "Başlık yazısı", 200 } } };
        list.DrawColumnHeader += (_, e) =>
        {
            if (drawDefault) e.DrawDefault = true;
            else e.Graphics.FillRectangle(Brushes.White, e.Bounds);
        };
        form.Controls.Add(list);
        form.Show();
        provider.SetClasses(list, "list");

        var header = SendMessage(list.Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
        using var bitmap = new Bitmap(200, 30);
        using var g = Graphics.FromImage(bitmap);
        var hdc = g.GetHdc();
        try
        {
            SetTextColor(hdc, 0);
            long prePaint = CustomDraw(list, header, hdc, 0x00000001 /* CDDS_PREPAINT */);
            Assert.True((prePaint & 0x20) != 0, "CDRF_NOTIFYITEMDRAW bekleniyordu");
            long itemPrePaint = CustomDraw(list, header, hdc, 0x00010001 /* CDDS_ITEMPREPAINT */);
            if (drawDefault)
            {
                Assert.Equal(0, itemPrePaint & 0x4);                                               // Windows çizer
                Assert.Equal(ColorTranslator.ToWin32(list.ForeColor), GetTextColor(hdc));
            }
            else
            {
                Assert.Equal(0x4, itemPrePaint & 0x4);                                             // CDRF_SKIPDEFAULT
                Assert.Equal(0, GetTextColor(hdc));                                                 // dokunulmadı
            }
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }
        form.Close();
    });

    private static long CustomDraw(ListView list, IntPtr header, IntPtr hdc, int stage)
    {
        var draw = new NmCustomDraw
        {
            Header = new NmHdr { HwndFrom = header, IdFrom = UIntPtr.Zero, Code = -12 /* NM_CUSTOMDRAW */ }, DrawStage = stage, Hdc = hdc,
            Rect = new NativeRect { Left = 0, Top = 0, Right = 200, Bottom = 30 }, ItemSpec = UIntPtr.Zero,
        };
        var memory = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<NmCustomDraw>());
        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(draw, memory, false);
            return SendMessage(list.Handle, 0x004E /* WM_NOTIFY */, IntPtr.Zero, memory).ToInt64();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(memory);
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NmHdr
    {
        public IntPtr HwndFrom;
        public UIntPtr IdFrom;
        public int Code;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NmCustomDraw
    {
        public NmHdr Header;
        public int DrawStage;
        public IntPtr Hdc;
        public NativeRect Rect;
        public UIntPtr ItemSpec;
        public int ItemState;
        public IntPtr ItemParam;
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, int color);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern int GetTextColor(IntPtr hdc);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);


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

    // Windows standart DateTimePicker'ın düzenleme alanını temadan çizer ve BackColor'ı yok sayar; koyu temada açık kalıyordu.
    // Koyu zeminde Windows'un çizimi koyuya çevrilir, açık zeminde Windows'a bırakılır. DrawToBitmap (WM_PRINTCLIENT) da
    // aynı yoldan geçer.
    [Fact]
    public void Date_time_picker_is_painted_dark_on_a_dark_background_only() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var picker = new DateTimePicker { Width = 200, Value = new DateTime(2026, 10, 18) };
        form.Controls.Add(picker);
        form.Show();
        provider.SetClasses(picker, "bg-white text-gray-900 dark:bg-gray-900 dark:text-gray-100");

        Assert.True(TwDateTimePickerPainter.IsPainted(picker));
        var dark = Pixels(picker);
        Assert.Contains(dark, p => p.ToArgb() == Color("#111827").ToArgb());                // gray-900 zemin
        Assert.Contains(dark, p => p.GetBrightness() > 0.8f);                               // açık yazı
        // Görsel stilsiz test sürecinde 3B kenarlık istemci alanı dışındadır (WS_EX_CLIENTEDGE); o da çevrilir.
        Assert.DoesNotContain(dark, p => p.GetBrightness() > 0.97f);
        Assert.True(dark[0].GetBrightness() < 0.5f && dark[dark.Length - 1].GetBrightness() < 0.5f, $"{dark[0]} {dark[dark.Length - 1]}");

        provider.DarkMode = false;
        Assert.False(TwDateTimePickerPainter.IsPainted(picker));
        Assert.Contains(Pixels(picker), p => p.GetBrightness() > 0.95f);                    // Windows'un açık zemini
        form.Close();
    });

    // AutoStyle DateTimePicker'ı eşlemiyordu; koyu temada açık kalırdı.
    [Fact]
    public void Auto_style_gives_date_time_pickers_the_select_component_and_paints_them_dark() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var picker = new DateTimePicker();
        form.Controls.Add(picker);
        form.Show();
        provider.ContainerControl = form;
        provider.AutoStyle = true;

        Assert.Equal("select", provider.GetAutomaticClasses(picker));
        Assert.True(TwDateTimePickerPainter.IsPainted(picker));
        Assert.DoesNotContain(Pixels(picker), p => p.GetBrightness() > 0.97f);
        form.Close();
    });

    private static Color[] Pixels(Control control)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return Enumerable.Range(0, control.Width).Select(x => bitmap.GetPixel(x, control.Height / 2)).ToArray();
    }

    [Fact]
    public void Date_time_picker_inversion_maps_neutrals_onto_the_colors_and_keeps_hues()
    {
        var back = Color("#111827");
        var fore = Color("#f3f4f6");
        int Map(Color c) => TwDateTimePickerPainter.Invert(c.ToArgb(), back, fore);

        Assert.Equal(back.ToArgb(), Map(System.Drawing.Color.White));
        Assert.Equal(fore.ToArgb(), Map(System.Drawing.Color.Black));
        var selection = System.Drawing.Color.FromArgb(Map(Color("#0078d7")));                // seçili alan
        Assert.True(selection.B > selection.G && selection.G > selection.R);                 // mavi kalır
        Assert.True(selection.GetBrightness() > Color("#0078d7").GetBrightness());          // açılır
        var day = System.Drawing.Color.FromArgb(Map(Color("#cce8ff")));                      // açık mavi seçili gün
        Assert.True(day.GetBrightness() < 0.3f && day.B > day.R);                            // koyu mavi
    }

    // Kenar yumuşatmanın renkli alt pikselleri çevrimde renkli kalırdı; etkinken gri tonlu kopya, kapanınca özgün font.
    [Fact]
    public void Dark_date_time_picker_uses_a_grayscale_copy_of_its_font_until_it_turns_light() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var picker = new DateTimePicker();
        form.Controls.Add(picker);
        form.Show();
        provider.SetClasses(picker, "bg-white dark:bg-gray-900 dark:text-gray-100");

        Assert.Equal(4 /* ANTIALIASED_QUALITY */, FontQuality(picker));
        picker.Font = new Font("Segoe UI", 12f);                                           // WinForms yeni fontu gönderir
        Assert.Equal(4, FontQuality(picker));

        provider.DarkMode = false;
        Assert.NotEqual(4, FontQuality(picker));
        form.Close();
    });

    private static int FontQuality(Control control)
    {
        var font = SendMessage(control.Handle, 0x0031 /* WM_GETFONT */, IntPtr.Zero, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, font);
        var logFont = new LogFont();
        Assert.NotEqual(0, GetObject(font, System.Runtime.InteropServices.Marshal.SizeOf<LogFont>(), ref logFont));
        return logFont.Quality;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct LogFont
    {
        public int Height, Width, Escapement, Orientation, Weight;
        public byte Italic, Underline, StrikeOut, CharSet, OutPrecision, ClipPrecision, Quality, PitchAndFamily;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetObject(IntPtr obj, int size, ref LogFont logFont);

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
