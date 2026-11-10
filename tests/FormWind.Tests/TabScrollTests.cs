using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>Kayan sekme şeridi (TwTabOverflow.Scroll), araç çubuğundaki daralması ve kesilen etiketlerin ipucu.</summary>
public class TabScrollTests
{
    private static readonly string[] ManyTabs = { "Genel bakış", "Duruşlar", "Parça & maliyet", "Kök neden", "Ekip", "Geçmiş", "Raporlar" };

    private static Form NewForm(int width = 640, int height = 300) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private static TwTabStrip ScrollingStrip(TwTabOverflow overflow = TwTabOverflow.Scroll, int width = 260)
    {
        var tabs = new TwTabStrip { Size = new Size(width, 40), Overflow = overflow };
        tabs.Tabs.AddRange(ManyTabs);
        return tabs;
    }

    private static void Paint(Control control)
    {
        using var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
    }

    [Fact]
    public void Scrolling_strip_keeps_every_tab_in_order_between_the_arrows() => Sta.Run(() =>
    {
        using var tabs = ScrollingStrip();
        Assert.True(tabs.IsScrollable);
        Assert.Empty(tabs.OverflowTabs);                                             // menüye sekme taşınmaz
        Assert.True(tabs.OverflowButtonBounds.IsEmpty);
        var back = tabs.ScrollBackButtonBounds;
        var forward = tabs.ScrollForwardButtonBounds;
        Assert.False(back.IsEmpty);
        Assert.False(forward.IsEmpty);
        Assert.True(back.Right < forward.Left);
        Assert.True(forward.Right <= tabs.Width);
        Assert.Equal(0, tabs.ScrollOffset);
        Assert.True(tabs.MaxScrollOffset > 0);

        // Sekmeler sırasını korur ve hiçbiri boş değildir.
        for (int i = 1; i < ManyTabs.Length; i++)
            Assert.True(tabs.GetTabBounds(i).Left > tabs.GetTabBounds(i - 1).Right - 1);
        Assert.All(Enumerable.Range(0, ManyTabs.Length), i => Assert.False(tabs.GetTabBounds(i).IsEmpty));

        // İlk sekme görünür, sonuncusu görünür alanın dışında.
        Assert.False(tabs.GetVisibleTabBounds(0).IsEmpty);
        Assert.True(tabs.GetVisibleTabBounds(ManyTabs.Length - 1).IsEmpty);
        Assert.True(tabs.GetVisibleTabBounds(0).Left >= back.Right);

        // Geniş şerit kaymaz.
        tabs.Width = 1400;
        Assert.False(tabs.IsScrollable);
        Assert.True(tabs.ScrollBackButtonBounds.IsEmpty);
        Assert.Equal(0, tabs.MaxScrollOffset);
    });

    [Fact]
    public void Selecting_a_tab_scrolls_it_into_view_and_offsets_move_the_bounds() => Sta.Run(() =>
    {
        using var tabs = ScrollingStrip();
        var before = tabs.GetTabBounds(3);
        tabs.SelectedIndex = ManyTabs.Length - 1;                                   // canlandırma testte hemen biter
        Assert.Equal(tabs.MaxScrollOffset, tabs.ScrollOffset);
        var last = tabs.GetVisibleTabBounds(ManyTabs.Length - 1);
        Assert.Equal(tabs.GetTabBounds(ManyTabs.Length - 1), last);               // tümüyle görünür
        Assert.Equal(before.Left - tabs.ScrollOffset, tabs.GetTabBounds(3).Left);
        Assert.True(tabs.GetVisibleTabBounds(0).IsEmpty);

        // ScrollOffset sınırlanır.
        tabs.ScrollOffset = -50;
        Assert.Equal(0, tabs.ScrollOffset);
        tabs.ScrollOffset = 100000;
        Assert.Equal(tabs.MaxScrollOffset, tabs.ScrollOffset);

        // ScrollTabIntoView ilk sekmeye geri döner.
        tabs.ScrollTabIntoView(0);
        Assert.Equal(0, tabs.ScrollOffset);
    });

    [Fact]
    public void Arrows_scroll_without_changing_the_selection_and_clicks_hit_the_shifted_tab() => Sta.Run(() =>
    {
        // Animated scrolling moves the tabs between reading their bounds and clicking (slow CI machines), so the
        // positions are checked with the scroll applied at once.
        var motion = TwMotion.Enabled;
        TwMotion.Enabled = false;
        try
        {
            using var form = NewForm();
            var tabs = ScrollingStrip();
            form.Controls.Add(tabs);
            form.Show();
            Application.DoEvents();

            var forward = tabs.ScrollForwardButtonBounds;
            var center = new Point(forward.Left + forward.Width / 2, forward.Top + forward.Height / 2);
            MouseDown(tabs, center);
            Assert.Equal(0, tabs.SelectedIndex);
            Assert.True(tabs.ScrollOffset > 0);

            // Görünür alandaki bir sekmeye tıklamak onu seçer (kaydırma hesaba katılır).
            int index = Enumerable.Range(0, ManyTabs.Length).First(i => i > 0 && !tabs.GetVisibleTabBounds(i).IsEmpty);
            var visible = tabs.GetVisibleTabBounds(index);
            MouseDown(tabs, new Point(visible.Left + visible.Width / 2, visible.Top + visible.Height / 2));
            Assert.Equal(index, tabs.SelectedIndex);

            // Okların altına kaymış sekmeye tıklanamaz: ok kazanır.
            int offset = tabs.ScrollOffset;
            var back = tabs.ScrollBackButtonBounds;
            MouseDown(tabs, new Point(back.Left + back.Width / 2, back.Top + back.Height / 2));
            Assert.Equal(index, tabs.SelectedIndex);
            Assert.True(tabs.ScrollOffset < offset);
            form.Close();
        }
        finally
        {
            TwMotion.Enabled = motion;
        }
    });

    private static void MouseDown(Control control, Point point)
    {
        var method = typeof(Control).GetMethod("OnMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        method.Invoke(control, new object[] { new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0) });
    }

    [Fact]
    public void Shift_wheel_scrolls_and_a_plain_wheel_passes_through() => Sta.Run(() =>
    {
        using var tabs = ScrollingStrip();
        var method = typeof(Control).GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var plain = new HandledMouseEventArgs(MouseButtons.None, 0, 10, 10, -120);
        method.Invoke(tabs, new object[] { plain });
        Assert.Equal(0, tabs.ScrollOffset);
        Assert.False(plain.Handled);                                                 // sayfa kaydırması bozulmaz
    });

    [Fact]
    public void Scroll_and_menu_lists_every_tab_with_the_selected_one_checked() => Sta.Run(() =>
    {
        using var form = NewForm();
        var tabs = ScrollingStrip(TwTabOverflow.ScrollAndMenu, 300);
        tabs.SelectedIndex = 2;
        form.Controls.Add(tabs);
        form.Show();
        try
        {
            Assert.True(tabs.IsScrollable);
            var button = tabs.OverflowButtonBounds;
            Assert.False(button.IsEmpty);
            Assert.True(tabs.ScrollForwardButtonBounds.Right <= button.Left);
            Assert.True(button.Right <= tabs.Width);

            tabs.ShowOverflow();
            var menu = tabs.OverflowDropDown!;
            Assert.Equal(ManyTabs.Length, menu.Items.Count);
            Assert.True(((ToolStripMenuItem)menu.Items[2]).Checked);
            Assert.False(((ToolStripMenuItem)menu.Items[0]).Checked);
            menu.Items[6].PerformClick();
            menu.Close();
            Assert.Equal(6, tabs.SelectedIndex);
            Assert.Equal(tabs.MaxScrollOffset, tabs.ScrollOffset);                  // hedef hemen; çizim kayarak gelir
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (tabs.GetVisibleTabBounds(6) != tabs.GetTabBounds(6) && watch.ElapsedMilliseconds < 2000) Application.DoEvents();
            Assert.False(tabs.GetVisibleTabBounds(6).IsEmpty);
            Assert.Equal(tabs.GetTabBounds(6), tabs.GetVisibleTabBounds(6));
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Overflow_menu_flag_maps_onto_the_overflow_mode() => Sta.Run(() =>
    {
        using var tabs = new TwTabStrip();
        Assert.Equal(TwTabOverflow.Menu, tabs.Overflow);
        Assert.True(tabs.OverflowMenu);
        tabs.OverflowMenu = false;
        Assert.Equal(TwTabOverflow.Clip, tabs.Overflow);
        tabs.OverflowMenu = true;
        Assert.Equal(TwTabOverflow.Menu, tabs.Overflow);
        tabs.Overflow = TwTabOverflow.ScrollAndMenu;
        tabs.OverflowMenu = false;
        Assert.Equal(TwTabOverflow.Scroll, tabs.Overflow);
        Assert.False(tabs.OverflowMenu);
        tabs.OverflowMenu = true;
        Assert.Equal(TwTabOverflow.ScrollAndMenu, tabs.Overflow);
    });

    [Theory]
    [InlineData(TwTabVariant.Underline)]
    [InlineData(TwTabVariant.Pills)]
    [InlineData(TwTabVariant.Segmented)]
    public void Scrolling_strip_paints_in_every_variant_at_both_ends(TwTabVariant variant) => Sta.Run(() =>
    {
        using var tabs = ScrollingStrip();
        tabs.Variant = variant;
        tabs.BackColor = Color.White;
        Paint(tabs);
        tabs.ScrollOffset = tabs.MaxScrollOffset / 2;
        Paint(tabs);
        tabs.ScrollOffset = tabs.MaxScrollOffset;
        Paint(tabs);
    });

    [Fact]
    public void Scrolled_out_tabs_are_offscreen_for_screen_readers() => Sta.Run(() =>
    {
        using var tabs = ScrollingStrip();
        var last = tabs.AccessibilityObject.GetChild(ManyTabs.Length - 1)!;
        Assert.True((last.State & AccessibleStates.Offscreen) != 0);
        Assert.True((tabs.AccessibilityObject.GetChild(0)!.State & AccessibleStates.Offscreen) == 0);
        // Durum çizilen konumu izler; kaydırma canlandırılmadan atlanır (net472'de tanıtıcı olunca canlandırma da çalışır).
        tabs.ScrollOffset = tabs.MaxScrollOffset;
        Assert.True((last.State & AccessibleStates.Offscreen) == 0);
        Assert.True((tabs.AccessibilityObject.GetChild(0)!.State & AccessibleStates.Offscreen) != 0);
    });

    [Fact]
    public void Toolbar_narrows_a_scrolling_strip_before_moving_it_to_the_menu() => Sta.Run(() =>
    {
        using var form = NewForm(900);
        var toolbar = new TwToolbar { Dock = DockStyle.Top, Height = 48 };
        var title = new Label { Text = "Duruş analizi", AutoSize = true };
        var tabs = new TwTabStrip { AutoSize = true, Overflow = TwTabOverflow.Scroll };
        tabs.Tabs.AddRange(ManyTabs);
        var save = new TwButton { Text = "Kaydet", AutoSize = true };
        toolbar.Controls.AddRange(new Control[] { title, tabs, save });
        form.Controls.Add(toolbar);
        form.Show();
        Application.DoEvents();

        int preferred = tabs.GetPreferredSize(Size.Empty).Width;
        Assert.Equal(preferred, tabs.Width);
        Assert.False(tabs.IsScrollable);

        // Daralan çubukta şerit menüye geçmez, daralır ve kayar.
        form.ClientSize = new Size(title.Width + save.Width + tabs.MinimumScrollWidth + 80, 300);
        Application.DoEvents();
        Assert.True(tabs.Visible);
        Assert.Empty(toolbar.OverflowItems);
        Assert.True(tabs.Width < preferred);
        Assert.True(tabs.Width >= tabs.MinimumScrollWidth);
        Assert.True(tabs.IsScrollable);

        // En küçük genişliği de sığmayınca menüye geçer.
        form.ClientSize = new Size(tabs.MinimumScrollWidth / 2, 300);
        Application.DoEvents();
        Assert.Contains(tabs, toolbar.OverflowItems);

        // Yer açılınca geri gelir ve tam genişliğine döner.
        form.ClientSize = new Size(900, 300);
        Application.DoEvents();
        Assert.True(tabs.Visible);
        Assert.Equal(preferred, tabs.Width);
        form.Close();
    });

    [Fact]
    public void Truncated_labels_get_a_themed_tooltip_only_while_cut() => Sta.Run(() =>
    {
        using var form = NewForm(400);
        using var tw = new TailwindProvider { DarkMode = true };
        var label = new Label { Text = "Çok uzun bir başlık metni burada kesilir", AutoSize = false, Size = new Size(80, 20) };
        form.Controls.Add(label);
        tw.SetClasses(label, "truncate");
        form.Show();
        Application.DoEvents();

        Assert.True(label.AutoEllipsis);
        var tip = TwTruncationTip.Get(label);
        Assert.NotNull(tip);
        Assert.Equal(label.Text, tip!.GetToolTip(label));
        Assert.True(tip.DarkMode);
        Assert.True(TwTruncationTip.IsTruncated(label));

        label.Width = 600;
        Assert.False(TwTruncationTip.IsTruncated(label));
        label.Text = "Yeni";
        Assert.Equal("Yeni", tip.GetToolTip(label));

        // truncate kalkınca ipucu da kalkar.
        tw.SetClasses(label, "text-sm");
        label.AutoEllipsis = false;
        tw.SetClasses(label, "text-base");
        Assert.Null(TwTruncationTip.Get(label));
        form.Close();
    });

    [Fact]
    public void Setting_row_caption_shows_its_full_text_in_a_tooltip() => Sta.Run(() =>
    {
        using var form = NewForm(260);
        using var tw = new TailwindProvider();
        var row = new TwSettingRow { LabelText = "Mikro duruşları hız kaybı olarak say ve raporla", Dock = DockStyle.Top };
        row.Controls.Add(new TwSwitch());
        form.Controls.Add(row);
        tw.SetClasses(row, "setting");
        form.Show();
        Application.DoEvents();
        var tip = TwTruncationTip.Get(row.CaptionLabel);
        Assert.NotNull(tip);
        Assert.Equal(row.LabelText, tip!.GetToolTip(row.CaptionLabel));
        form.Close();
    });

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;

    [Theory]
    [InlineData(TwTabVariant.Pills)]
    [InlineData(TwTabVariant.Segmented)]
    public void Dark_pills_and_segments_get_a_dark_active_tab_and_track(TwTabVariant variant) => Sta.Run(() =>
    {
        using var form = NewForm();
        using var tw = new TailwindProvider { DarkMode = true };
        var tabs = new TwTabStrip { Variant = variant, AutoSize = true };
        tabs.Tabs.AddRange(new[] { "Gün", "Hafta", "Ay" });
        var custom = new TwTabStrip { Variant = variant, AutoSize = true, Top = 60 };
        custom.Tabs.AddRange(new[] { "Gün", "Hafta" });
        form.Controls.AddRange(new Control[] { tabs, custom });
        tw.SetClasses(tabs, "tabs");
        tw.SetClasses(custom, "tabs dark:selected:bg-blue-600 dark:fill-gray-700");
        form.Show();
        Application.DoEvents();

        // Etkin sekmenin yazısı zemininden okunur ayrışır (beyaz üstüne beyaz değil).
        Assert.True(Luminance(tabs.Colors.ActiveFore) - Luminance(tabs.Colors.ActiveBack) > 0.4);
        Assert.True(Luminance(tabs.Colors.Track) < 0.3);
        Assert.Equal(ColorTranslator.FromHtml("#2563eb"), custom.Colors.ActiveBack);   // verilen class'lar kazanır
        Assert.Equal(ColorTranslator.FromHtml("#374151"), custom.Colors.Track);

        // Açık temada varsayılanlar değişmez.
        tw.DarkMode = false;
        Application.DoEvents();
        Assert.Equal(Color.White, tabs.Colors.ActiveBack);
        Assert.Equal(ColorTranslator.FromHtml("#f3f4f6"), tabs.Colors.Track);
        form.Close();
    });
}
