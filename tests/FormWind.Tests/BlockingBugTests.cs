using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// 0.1.6 planının 1. adımı: Resize ile kenar çubuğu, ayar satırı EditorWidth = 0, araç çubuğu Shrink / taşma menüsü, çip yüksekliği.
[Collection(UiCollection.Name)]
public class BlockingBugTests
{
    private static void WaitUntil(Func<bool> condition, int milliseconds = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private static Form ShowForm(Size clientSize)
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(120, 120), ClientSize = clientSize };
        form.Show();
        form.Activate();
        Application.DoEvents();
        return form;
    }

    private static Form HiddenForm(Size clientSize) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = clientSize };

    private static void WithMotion(Action action)
    {
        var previous = TwMotion.Preference;
        TwMotion.Enabled = true;
        try { action(); }
        finally { TwMotion.Preference = previous; }
    }

    // ---- #1 TwTransition.Resize ----

    private static (Panel Content, Panel Sidebar, Panel Row) Layout(Form form)
    {
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 240, BackColor = Color.SteelBlue };
        var row = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.Orange };
        sidebar.Controls.Add(row);
        form.Controls.Add(content);
        form.Controls.Add(sidebar);
        Application.DoEvents();
        return (content, sidebar, row);
    }

    [Fact]
    public void Resize_where_the_change_hides_the_panel_reveals_it_closing() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(500, 300));
        var (_, sidebar, row) = Layout(form);
        int open = Application.OpenForms.Count;

        TwTransition.Resize(sidebar, () =>
        {
            sidebar.Width = 0;
            sidebar.Visible = false;
            sidebar.Controls.Remove(row);
            form.Controls.Add(row);                                                // çocuk formun içine taşınır
        }, new TwMotionOptions { DurationMs = 120 });
        Assert.True(TwTransition.LastCovered);
        Assert.True(TwTransition.LastRevealed);                                    // yalnızca çapraz solma değil
        Assert.False(sidebar.Visible);
        Assert.Same(form, row.Parent);
        WaitUntil(() => Application.OpenForms.Count == open && TwFadeOut.ActiveCount == 0);
        Assert.Equal(open, Application.OpenForms.Count);
        form.Close();
    }));

    [Fact]
    public void Resize_opening_a_hidden_panel_animates() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(500, 300));
        var (_, sidebar, _) = Layout(form);
        sidebar.Visible = false;
        sidebar.Width = 0;
        Application.DoEvents();
        int open = Application.OpenForms.Count;
        bool called = false;

        TwTransition.Resize(sidebar, () =>
        {
            called = true;
            sidebar.Width = 240;
            sidebar.Visible = true;
        }, new TwMotionOptions { DurationMs = 120 });
        Assert.True(called);
        Assert.True(TwTransition.LastCovered);
        Assert.True(TwTransition.LastRevealed);
        Assert.True(sidebar.Visible);
        Assert.Equal(240, sidebar.Width);
        WaitUntil(() => Application.OpenForms.Count == open && TwFadeOut.ActiveCount == 0);
        form.Close();
    }));

    [Fact]
    public void Resize_to_width_zero_and_back_animates_with_the_panel_visible() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(500, 300));
        var (_, sidebar, _) = Layout(form);
        int open = Application.OpenForms.Count;

        TwTransition.Resize(sidebar, 0, new TwMotionOptions { DurationMs = 120 });
        Assert.True(TwTransition.LastRevealed);
        Assert.Equal(0, sidebar.Width);
        WaitUntil(() => Application.OpenForms.Count == open && TwFadeOut.ActiveCount == 0);

        TwTransition.Resize(sidebar, 240, new TwMotionOptions { DurationMs = 120 });
        Assert.True(TwTransition.LastRevealed);
        Assert.Equal(240, sidebar.Width);
        WaitUntil(() => Application.OpenForms.Count == open && TwFadeOut.ActiveCount == 0);
        form.Close();
    }));

    [Fact]
    public void Resize_with_an_unchanged_size_is_only_a_cross_fade() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(500, 300));
        var (content, sidebar, _) = Layout(form);
        TwTransition.Resize(sidebar, () => content.BackColor = Color.Black, new TwMotionOptions { DurationMs = 60 });
        Assert.True(TwTransition.LastCovered);
        Assert.False(TwTransition.LastRevealed);
        WaitUntil(() => TwFadeOut.ActiveCount == 0);
        form.Close();
    }));

    [Fact]
    public void The_cover_of_a_resize_spans_the_form_not_only_the_parent() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(500, 300));
        var holder = new Panel { Dock = DockStyle.Left, Width = 300, BackColor = Color.Gray };
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 100, BackColor = Color.SteelBlue };
        var other = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        holder.Controls.Add(sidebar);
        form.Controls.Add(other);
        form.Controls.Add(holder);
        Application.DoEvents();
        int ghosts = 0;
        TwTransition.Resize(sidebar, () =>
        {
            // Örtü, panelin ebeveyninin dışını da kapsayan bir pencere olarak ekranda olmalı.
            var client = form.RectangleToScreen(form.ClientRectangle);
            Assert.Contains(Application.OpenForms.OfType<TwFadeOut.Ghost>(), g => g.Bounds.Contains(client));
            ghosts++;
            sidebar.Width = 40;
        }, new TwMotionOptions { DurationMs = 60 });
        Assert.Equal(1, ghosts);
        WaitUntil(() => TwFadeOut.ActiveCount == 0);
        form.Close();
    }));

    // ---- H1 TwSettingRow.EditorWidth = 0 ----

    [Fact]
    public void Setting_row_editor_width_zero_keeps_the_editors_own_width() => Sta.Run(() =>
    {
        using var form = HiddenForm(new Size(900, 400));
        TwSettingRow Row(Control editor)
        {
            var row = new TwSettingRow { LabelText = "Satır", Dock = DockStyle.Top };
            row.Controls.Add(editor);
            form.Controls.Add(row);
            return row;
        }
        var select = new TwSelect { Width = 180 };
        var toggle = new TwSwitch { AutoSize = false, Width = 104, Height = 24 };
        var label = new Label { AutoSize = false, Width = 280, Height = 24, Text = "Durum" };
        var rows = new[] { Row(select), Row(toggle), Row(label) };
        form.Show();
        Application.DoEvents();
        Assert.Equal(160, select.Width);                                           // varsayılan: 160 px

        foreach (var row in rows) row.EditorWidth = 0;
        Application.DoEvents();
        Assert.Equal(180, select.Width);
        Assert.Equal(104, toggle.Width);
        Assert.Equal(280, label.Width);

        // Önce 0, sonra ekleme.
        var late = new TwSelect { Width = 210 };
        var lateRow = new TwSettingRow { LabelText = "Geç", EditorWidth = 0, Dock = DockStyle.Top };
        form.Controls.Add(lateRow);
        lateRow.Controls.Add(late);
        Application.DoEvents();
        Assert.Equal(210, late.Width);

        // Uygulama genişliği sonradan değiştirir.
        select.Width = 220;
        Application.DoEvents();
        Assert.Equal(220, select.Width);
        rows[0].EditorWidth = 120;
        Application.DoEvents();
        Assert.Equal(120, select.Width);
        rows[0].EditorWidth = 0;
        Application.DoEvents();
        Assert.Equal(220, select.Width);
    });

    // ---- H2 / H4 TwToolbar ----

    [Fact]
    public void Toolbar_shrink_title_keeps_its_content_width_beside_buttons_and_a_tab_strip() => Sta.Run(() =>
    {
        using var form = HiddenForm(new Size(1200, 400));
        using var tw = new TailwindProvider();
        var toolbar = new TwToolbar { Dock = DockStyle.Top, Height = 44 };
        var title = new Label { Text = "Keşfet · duruş ağacı", AutoSize = true, AutoEllipsis = true };
        var tabs = new TwTabStrip();
        tabs.Tabs.AddRange(new[] { "Ağaç", "Pareto", "Zaman çizelgesi" });
        toolbar.Controls.Add(title);
        for (int i = 0; i < 4; i++) toolbar.Controls.Add(new TwButton { Text = "Komut " + i, AutoSize = true });
        toolbar.Controls.Add(tabs);
        toolbar.SetOverflow(title, TwToolbarOverflow.Shrink);
        form.Controls.Add(toolbar);
        form.Show();
        tw.SetClasses(title, "font-semibold text-[15px]");
        Application.DoEvents();

        Assert.False(toolbar.OverflowButton.Visible);
        int wanted = title.GetPreferredSize(Size.Empty).Width;
        Assert.True(title.Width >= wanted, $"{title.Width} < {wanted}");
        Assert.Empty(toolbar.OverflowItems);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Toolbar_shrink_title_is_not_shortened_when_the_items_exactly_fit_inside_the_padding(bool titleAutoSize) => Sta.Run(() =>
    {
        using var form = HiddenForm(new Size(1200, 400));
        var toolbar = new TwToolbar { Location = Point.Empty, Size = new Size(1100, 44), Padding = new Padding(40, 4, 40, 4) };
        var title = new Label { Text = "Keşfet · duruş ağacı", AutoSize = titleAutoSize, AutoEllipsis = true };
        if (!titleAutoSize) title.Size = new Size(60, 20);                          // eski/dar bir boy: içerik ölçülmeli
        var tabs = new TwTabStrip { AutoSize = true };
        tabs.Tabs.AddRange(new[] { "Ağaç", "Pareto", "Zaman çizelgesi" });
        toolbar.Controls.Add(title);
        for (int i = 0; i < 4; i++) toolbar.Controls.Add(new TwButton { Text = "Komut " + i, AutoSize = true });
        toolbar.Controls.Add(tabs);
        toolbar.SetOverflow(title, TwToolbarOverflow.Shrink);
        form.Controls.Add(toolbar);
        form.Show();
        Application.DoEvents();
        int wanted = title.GetPreferredSize(Size.Empty).Width;
        Assert.Equal(wanted, title.Width);

        // Çubuk, öğelerin tam sığacağı genişliğe daraltılır (dolgu içeride kalır): başlık kısalmamalı.
        int needed = tabs.Right + tabs.Margin.Right - (title.Left - title.Margin.Left);
        toolbar.Width = needed + toolbar.Padding.Horizontal;
        Application.DoEvents();
        Assert.False(toolbar.OverflowButton.Visible);
        Assert.Equal(wanted, title.Width);
    });

    private static (TwToolbar Toolbar, Control Item) ToolbarWithOverflowItem(Form form, Func<TwTabStrip, Control> wrap)
    {
        var toolbar = new TwToolbar { Dock = DockStyle.Top, Height = 44 };
        var tabs = new TwTabStrip();
        tabs.Tabs.AddRange(new[] { "Ağaç", "Pareto", "Zaman" });
        tabs.SelectedIndex = 1;
        var item = wrap(tabs);
        toolbar.Controls.Add(new TwButton { Text = "Kaydet", AutoSize = true });
        toolbar.Controls.Add(item);
        toolbar.SetOverflow(item, TwToolbarOverflow.Always);
        form.Controls.Add(toolbar);
        form.Show();
        Application.DoEvents();
        return (toolbar, item);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Toolbar_overflow_menu_never_has_an_empty_row_for_a_tab_strip(bool wrapped) => Sta.Run(() =>
    {
        using var form = HiddenForm(new Size(520, 300));
        var (toolbar, item) = ToolbarWithOverflowItem(form, tabs =>
        {
            if (!wrapped) return tabs;
            var panel = new Panel { AutoSize = true };
            panel.Controls.Add(tabs);
            return panel;
        });
        Assert.Contains(item, toolbar.OverflowItems);
        toolbar.ShowOverflowMenu();
        try
        {
            var menu = toolbar.OverflowMenu!;
            Assert.All(menu.Items.Cast<ToolStripItem>(), i => Assert.False(string.IsNullOrWhiteSpace(i.Text)));
            var entry = Assert.IsType<ToolStripMenuItem>(menu.Items[0]);
            Assert.Equal(3, entry.DropDownItems.Count);                            // sekmeler alt menü
            Assert.True(((ToolStripMenuItem)entry.DropDownItems[1]).Checked);
        }
        finally
        {
            toolbar.OverflowMenu?.Close();
        }
    });

    [Fact]
    public void Toolbar_overflow_menu_text_falls_back_to_accessible_name_then_more() => Sta.Run(() =>
    {
        using var form = HiddenForm(new Size(520, 300));
        var toolbar = new TwToolbar { Dock = DockStyle.Top, Height = 44 };
        var named = new Panel { Size = new Size(40, 20), AccessibleName = "Görünüm" };
        var anonymous = new Panel { Size = new Size(40, 20) };
        toolbar.Controls.AddRange(new Control[] { named, anonymous });
        toolbar.SetOverflow(named, TwToolbarOverflow.Always);
        toolbar.SetOverflow(anonymous, TwToolbarOverflow.Always);
        form.Controls.Add(toolbar);
        form.Show();
        Application.DoEvents();
        toolbar.ShowOverflowMenu();
        try
        {
            var texts = toolbar.OverflowMenu!.Items.Cast<ToolStripItem>().Select(i => i.Text).ToArray();
            Assert.Equal("Görünüm", texts[0]);
            Assert.Equal(TwTexts.Current.More, texts[1]);
        }
        finally
        {
            toolbar.OverflowMenu?.Close();
        }
    });

    // ---- H3 TwChip h-* ----

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void Chip_class_height_applies_in_either_order(bool classesFirst, bool shownFirst) => Sta.Run(() =>
    {
        using var form = HiddenForm(new Size(640, 400));
        using var tw = new TailwindProvider();
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        form.Controls.Add(flow);
        if (shownFirst) form.Show();
        var chip = new TwChip { Text = "Açık" };
        if (classesFirst)
        {
            tw.SetClasses(chip, "chip-acc px-3 h-[26px]");
            flow.Controls.Add(chip);
        }
        else
        {
            flow.Controls.Add(chip);
            tw.SetClasses(chip, "chip-acc px-3 h-[26px]");
        }
        form.Show();
        Application.DoEvents();
        int expected = (int)Math.Round(26 * TwDpi.DeviceDpi(chip) / 96f);
        Assert.Equal(expected, chip.GetPreferredSize(Size.Empty).Height);
        Assert.Equal(expected, chip.Height);
    });
}
