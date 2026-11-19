using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// The 0.1.10-preview.2 user feedback report: chip strip sizing after a chip grows, scroll button room, size and alignment,
/// lost scroll buttons, info tip hover from icon to icon, locked select cells and the info tip footer.
/// </summary>
[Collection(UiCollection.Name)]
public class Feedback0110Preview2Tests
{
    private static Form HostForm(int width = 640, int height = 200) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private static Button Chip(int width = 80, int height = 26) => new Button { AutoSize = false, Size = new Size(width, height) };

    private static TwChipStrip FixedStrip(Form form, int width, int chips, int height = 40)
    {
        var strip = new TwChipStrip { Size = new Size(width, height), Gap = 6 };
        for (int i = 0; i < chips; i++) strip.Controls.Add(Chip());
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();
        return strip;
    }

    // ---- A: AutoSize strip follows a chip that grows later ----

    [Fact]
    public void AutoSize_strip_grows_when_a_chip_grows_after_it_was_added() => Sta.Run(() =>
    {
        using var form = HostForm();
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var strip = new TwChipStrip { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Gap = 6 };
        for (int i = 0; i < 3; i++)
            strip.Controls.Add(new Button { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Text = "A" });
        flow.Controls.Add(strip);
        form.Controls.Add(flow);
        form.Show();
        Application.DoEvents();
        var before = strip.Size;

        foreach (var chip in strip.Chips) chip.Text = "Much longer chip text";
        Application.DoEvents();

        var preferred = strip.GetPreferredSize(Size.Empty);
        Assert.True(preferred.Width > before.Width);
        Assert.Equal(preferred, strip.Size);
        Assert.Equal(0, strip.MaxScrollOffset);
        Assert.False(strip.NextButton.Visible);
        Assert.Equal(0, strip.Chips[0].Left);
    });

    // ---- B: a hidden button takes no room; a shown one lies over the chips ----

    [Fact]
    public void Overflowing_strip_starts_its_first_chip_at_the_start_edge() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3);
        Assert.True(strip.MaxScrollOffset > 0);
        Assert.False(strip.PreviousButton.Visible);
        Assert.True(strip.NextButton.Visible);
        Assert.Equal(0, strip.Chips[0].Left);

        strip.ScrollOffset = strip.MaxScrollOffset;
        Assert.False(strip.NextButton.Visible);
        Assert.True(strip.PreviousButton.Visible);
        Assert.Equal(strip.ClientSize.Width, strip.Chips.Last().Right);
        Assert.Equal(3 * 80 + 2 * 6 - 200, strip.MaxScrollOffset);
    });

    [Fact]
    public void EnsureVisible_keeps_a_chip_clear_of_the_shown_buttons() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 6);
        var middle = strip.Chips[3];
        strip.EnsureVisible(middle);
        Assert.True(strip.NextButton.Visible);
        Assert.True(middle.Right <= strip.NextButton.Left);

        strip.ScrollOffset = strip.MaxScrollOffset;
        var second = strip.Chips[1];
        strip.EnsureVisible(second);
        Assert.True(strip.PreviousButton.Visible);
        Assert.True(second.Left >= strip.PreviousButton.Right);
    });

    [Fact]
    public void EnsureVisible_keeps_a_chip_clear_of_the_shown_buttons_right_to_left() => Sta.Run(() =>
    {
        using var form = HostForm();
        // Gap 20: the second chip ends (180) under › at offset 0; the fifth starts (400) under ‹ at the end (offset 380).
        var strip = new TwChipStrip { Size = new Size(200, 40), Gap = 20, RightToLeft = RightToLeft.Yes };
        for (int i = 0; i < 6; i++) strip.Controls.Add(Chip());
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();

        // Right to left: the first chip and ‹ sit at the right edge, › at the left.
        Assert.Equal(strip.ClientSize.Width, strip.Chips[0].Right);
        var second = strip.Chips[1];
        strip.EnsureVisible(second);
        Assert.True(strip.NextButton.Visible);
        Assert.True(second.Left >= strip.NextButton.Right);

        strip.ScrollOffset = strip.MaxScrollOffset;
        var fifth = strip.Chips[4];
        strip.EnsureVisible(fifth);
        Assert.True(strip.PreviousButton.Visible);
        Assert.True(fifth.Right <= strip.PreviousButton.Left);
    });

    [Fact]
    public void A_shown_scroll_button_covers_the_chip_under_it() => Sta.Run(() =>
    {
        using var form = HostForm();
        form.BackColor = Color.White;
        var strip = new TwChipStrip { Size = new Size(200, 40), Gap = 6, BackColor = Color.White };
        for (int i = 0; i < 3; i++)
            strip.Controls.Add(new Button { AutoSize = false, Size = new Size(80, 26), FlatStyle = FlatStyle.Flat, BackColor = Color.Red });
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();
        Assert.True(strip.NextButton.Visible);

        using var picture = TwOffscreen.Render(form);
        var button = strip.RectangleToScreen(strip.NextButton.Bounds);
        var client = form.RectangleToScreen(form.ClientRectangle);
        var probe = picture.GetPixel(button.Left - client.Left + 2, button.Top - client.Top + button.Height / 2);
        Assert.False(probe.R > 200 && probe.G < 60 && probe.B < 60, $"chip shows through the button: {probe}");
    });

    // ---- C: square buttons as tall as the chip row ----

    [Fact]
    public void Scroll_buttons_are_square_and_as_tall_as_the_chips() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3);
        Assert.Equal(new Size(26, 26), strip.NextButton.Size);

        // AutoSize given by the application: the button keeps its own size.
        strip.NextButton.AutoSize = true;
        strip.PerformLayout();
        Assert.Equal(strip.NextButton.GetPreferredSize(Size.Empty).Width, strip.NextButton.Width);
    });

    // ---- D: buttons follow ChipAlignment ----

    [Theory]
    [InlineData(TwAlign.Start)]
    [InlineData(TwAlign.Center)]
    [InlineData(TwAlign.End)]
    public void Scroll_buttons_line_up_with_the_chips(TwAlign alignment) => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3, height: 44);
        strip.Padding = new Padding(0, 6, 0, 0);
        strip.ChipAlignment = alignment;
        Assert.Equal(strip.Chips[0].Top, strip.NextButton.Top);
        Assert.Equal(strip.Chips[0].Height, strip.NextButton.Height);
    });

    [Fact]
    public void Stretched_chips_get_buttons_as_tall_as_the_strip() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3, height: 30);
        strip.ChipAlignment = TwAlign.Stretch;
        Assert.Equal(strip.ClientSize.Height, strip.NextButton.Height);
        Assert.Equal(strip.Chips[0].Top, strip.NextButton.Top);
    });

    // ---- Chips / Controls: lost scroll buttons come back ----

    [Fact]
    public void Disposed_scroll_buttons_are_put_back() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3);
        var oldPrevious = strip.PreviousButton;
        var oldNext = strip.NextButton;

        foreach (var child in strip.Controls.Cast<Control>().ToArray()) child.Dispose();
        Application.DoEvents();

        Assert.NotSame(oldPrevious, strip.PreviousButton);
        Assert.NotSame(oldNext, strip.NextButton);
        Assert.Same(strip, strip.PreviousButton.Parent);
        Assert.Same(strip, strip.NextButton.Parent);
        Assert.Empty(strip.Chips);

        for (int i = 0; i < 3; i++) strip.Controls.Add(Chip());
        Assert.True(strip.NextButton.Visible);
    });

    [Fact]
    public void Clearing_Controls_keeps_the_scroll_buttons() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3);
        var next = strip.NextButton;
        strip.Controls.Clear();
        Application.DoEvents();
        Assert.Same(next, strip.NextButton);
        Assert.Same(strip, next.Parent);
    });

    // ---- E: hover from one anchor to another while a card is open ----

    [Fact]
    public void Hover_anchor_under_the_pointer_starts_its_delay_when_another_card_closes() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(40, 40), ClientSize = new Size(300, 120) };
        var a = new TwIcon { IconName = "info", Location = new Point(20, 20), Size = new Size(24, 24) };
        var b = new TwIcon { IconName = "info", Location = new Point(60, 20), Size = new Size(24, 24) };
        form.Controls.AddRange(new Control[] { a, b });
        using var cardA = new TwPopover { Content = new Label { Text = "A", Size = new Size(160, 60) } };
        using var cardB = new TwPopover { Content = new Label { Text = "B", Size = new Size(160, 60) } };
        cardA.ShowOnHover(a, 300);
        cardB.ShowOnHover(b, 300);
        form.Show();
        Application.DoEvents();

        var t0 = DateTime.UtcNow;
        var insideA = a.PointToScreen(new Point(5, 5));
        var insideB = b.PointToScreen(new Point(5, 5));
        cardA.BeginHover(a, t0);
        cardA.HoverTick(insideA, t0.AddMilliseconds(320));
        Assert.True(cardA.IsOpenFromHover);

        // The pointer moves onto B while A's card is open: B never gets MouseEnter (swallowed), A closes after its delay.
        cardA.HoverTick(insideB, t0.AddMilliseconds(400));
        cardA.HoverTick(insideB, t0.AddMilliseconds(710));
        Assert.False(cardA.IsOpen);

        cardB.HoverTick(insideB, t0.AddMilliseconds(800));
        Assert.False(cardB.IsOpen);                                                    // the delay counts from A's close
        cardB.HoverTick(insideB, t0.AddMilliseconds(1020));
        Assert.True(cardB.IsOpenFromHover);
        cardB.Close();
    });

    [Fact]
    public void A_closed_card_does_not_reopen_for_its_own_anchor() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(40, 40), ClientSize = new Size(300, 120) };
        var a = new TwIcon { IconName = "info", Location = new Point(20, 20), Size = new Size(24, 24) };
        form.Controls.Add(a);
        using var card = new TwPopover { Content = new Label { Text = "A", Size = new Size(160, 60) } };
        card.ShowOnHover(a, 300);
        form.Show();
        Application.DoEvents();

        var t0 = DateTime.UtcNow;
        var inside = a.PointToScreen(new Point(5, 5));
        card.BeginHover(a, t0);
        card.HoverTick(inside, t0.AddMilliseconds(320));
        Assert.True(card.IsOpen);
        card.Close();                                                                  // Esc / click elsewhere
        TwPopover.RearmHoverUnder(inside, t0.AddMilliseconds(400), card);
        card.HoverTick(inside, t0.AddMilliseconds(800));
        Assert.False(card.IsOpen);
    });

    // ---- Requests: locked select rows, info tip footer ----

    [Fact]
    public void Locked_select_rows_do_not_open_the_list() => Sta.Run(() =>
    {
        using var form = HostForm();
        var table = new DataTable();
        table.Columns.Add("Line", typeof(string));
        table.Rows.Add("P1");
        table.Rows.Add(DBNull.Value);
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false };
        var line = new TwSelectColumn { DataPropertyName = "Line", Width = 200, LockedFor = row => row == 1 };
        line.Items.AddRange(new object[] { "P1", "P2" });
        grid.Columns.Add(line);
        grid.DataSource = table;
        form.Controls.Add(grid);
        form.Show();
        form.Activate();
        Application.DoEvents();

        ((TwSelectCell)grid[0, 1]).ShowDropDown(1);
        Assert.Null(TwSelectCell.OpenMenu);
        ((TwSelectCell)grid[0, 0]).ShowDropDown(0);
        Assert.NotNull(TwSelectCell.OpenMenu);
        TwSelectCell.OpenMenu!.Close();
        Application.DoEvents();

        var clone = (TwSelectColumn)line.Clone();
        Assert.Same(line.LockedFor, clone.LockedFor);
    });

    [Fact]
    public void Info_tip_footer_is_a_faint_smaller_last_line() => Sta.Run(() =>
    {
        using var form = HostForm();
        var tip = new TwInfoTip { Title = "OEE", Description = "Kullanılabilirlik × performans × kalite." };
        form.Controls.Add(tip);
        form.Show();
        Application.DoEvents();

        tip.ShowCard();
        int plain = tip.Card.Height;
        tip.HideCard();
        tip.ShowCard();                                                                // reopened: measured the same
        Assert.Equal(plain, tip.Card.Height);
        tip.HideCard();
        tip.Footer = "Ayrıntı için tıklayın";
        tip.ShowCard();

        var labels = tip.Card.Controls.OfType<Label>().ToArray();
        var footer = labels.Single(l => l.Text == "Ayrıntı için tıklayın");
        Assert.True(footer.Visible);
        Assert.True(tip.Card.Height > plain);
        Assert.True(footer.Font.Size < tip.Font.Size);
        Assert.NotEqual(tip.Card.ForeColor, footer.ForeColor);
        Assert.All(labels.Where(l => l != footer && l.Visible), l => Assert.True(footer.Top > l.Top));
        tip.HideCard();
    });
}
