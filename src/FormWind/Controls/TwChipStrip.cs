using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>
/// A single row of chips (or any controls, in <c>Controls</c> order, at their preferred size) that scrolls sideways without a
/// scroll bar: the mouse wheel over the strip or a chip moves it, ← → move it while it has focus, and the ‹ › buttons appear
/// at the ends only while chips are hidden there. Styled like a <see cref="TwPanel"/>; <see cref="PreviousButton"/> and
/// <see cref="NextButton"/> take their own classes. With <c>AutoSize</c> the strip is as wide as its chips (within
/// <c>MaximumSize</c>, past which it scrolls) and as tall as the tallest; hidden chips take no room.
/// <code>
/// var strip = new TwChipStrip { Dock = DockStyle.Top, Height = 40 };
/// foreach (var line in lines) strip.Controls.Add(new TwChip { Text = line.Name });
/// strip.EnsureVisible(selectedChip);
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ScrollChanged))]
[Description("Row of chips that scrolls sideways without a scroll bar.")]
public class TwChipStrip : TwPanel
{
    private const int WheelStep = 48;
    private int _gap = 8;
    private int _offset;
    private int _contentWidth;
    private bool _arranging;
    private TwAlign _chipAlignment = TwAlign.Center;

    public TwChipStrip()
    {
        PreviousButton = new TwButton { IconName = "chevron-left", AutoSize = true, AccessibleName = TwTexts.Current.Previous, TabStop = false, Visible = false };
        NextButton = new TwButton { IconName = "chevron-right", AutoSize = true, AccessibleName = TwTexts.Current.Next, TabStop = false, Visible = false };
        PreviousButton.Click += (_, _) => ScrollBy(-PageStep);
        NextButton.Click += (_, _) => ScrollBy(PageStep);
        Controls.Add(PreviousButton);
        Controls.Add(NextButton);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    protected override Size DefaultSize => new Size(480, 40);

    /// <summary>Raised when <see cref="ScrollOffset"/> changes.</summary>
    public event EventHandler? ScrollChanged;

    /// <summary>Space between chips (96 DPI pixels).</summary>
    [Category("Layout"), DefaultValue(8)]
    public int Gap
    {
        get => _gap;
        set
        {
            value = Math.Max(0, value);
            if (_gap == value) return;
            _gap = value;
            PerformLayout();
        }
    }

    /// <summary>How far the chips are scrolled (device pixels, 0 = the first chip at the start); kept within <see cref="MaxScrollOffset"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ScrollOffset
    {
        get => _offset;
        set
        {
            value = Math.Max(0, Math.Min(MaxScrollOffset, value));
            if (_offset == value) return;
            _offset = value;
            Arrange();
            ScrollChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The largest <see cref="ScrollOffset"/>: how much wider the chips are than the strip (0 when they all fit).</summary>
    [Browsable(false)]
    public int MaxScrollOffset => Math.Max(0, _contentWidth - Viewport.Width);

    /// <summary>The chips: the children other than the scroll buttons, in <c>Controls</c> order.</summary>
    [Browsable(false)]
    public Control[] Chips => Controls.Cast<Control>().Where(c => c != PreviousButton && c != NextButton).ToArray();

    [Browsable(false)]
    public TwButton PreviousButton { get; }

    [Browsable(false)]
    public TwButton NextButton { get; }

    /// <summary>Scrolls by <paramref name="pixels"/> (device pixels; negative = toward the start).</summary>
    public void ScrollBy(int pixels) => ScrollOffset = _offset + pixels;

    /// <summary>Scrolls just enough to show <paramref name="chip"/> whole (as far as it fits).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="chip"/> is <c>null</c>.</exception>
    public void EnsureVisible(Control chip)
    {
        if (chip is null) throw new ArgumentNullException(nameof(chip));
        if (chip.Parent != this || chip == PreviousButton || chip == NextButton) return;
        int left = ContentLeft(chip), right = left + chip.Width, width = Viewport.Width;
        if (left < _offset) ScrollOffset = left;
        else if (right > _offset + width) ScrollOffset = right - width;
    }

    private int PageStep => Math.Max(Scale(WheelStep), Viewport.Width * 4 / 5);

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    // DisplayRectangle already leaves out the padding (ScrollableControl) and the border (TwPanel).
    private Rectangle Area => DisplayRectangle;

    /// <summary>
    /// The chips that take part in the layout: hidden ones take no room. Before the strip is shown every child reports
    /// <c>Visible = false</c>, so then all count.
    /// </summary>
    private Control[] LaidOutChips => Chips.Where(c => c.Visible || !Visible).ToArray();

    private static Size ChipSize(Control chip) => chip.AutoSize ? chip.GetPreferredSize(Size.Empty) : chip.Size;

    private int ContentWidthOf(Control[] chips) =>
        chips.Length == 0 ? 0 : chips.Sum(c => ChipSize(c).Width) + Scale(_gap) * (chips.Length - 1);

    /// <summary>Width: the chips side by side plus padding and border (kept within <c>MaximumSize</c>, where the strip scrolls); height: the tallest chip.</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var chips = LaidOutChips;
        var border = BorderInsets();
        int height = chips.Length == 0 ? 0 : chips.Max(c => ChipSize(c).Height);
        return new Size(ContentWidthOf(chips) + Padding.Horizontal + border.Horizontal, height + Padding.Vertical + border.Vertical);
    }

    /// <summary>Where the chips sit in the strip's height (<see cref="TwAlign.Stretch"/> = as tall as the strip).</summary>
    [Category("Layout"), DefaultValue(TwAlign.Center)]
    public TwAlign ChipAlignment
    {
        get => _chipAlignment;
        set
        {
            if (_chipAlignment == value) return;
            _chipAlignment = value;
            PerformLayout();
        }
    }

    // Çiplerin görünen kısmı: taşma varsa uçlardaki düğmelerin arası.
    private Rectangle Viewport
    {
        get
        {
            var area = Area;
            if (_contentWidth <= area.Width) return area;
            int prev = PreviousButton.GetPreferredSize(Size.Empty).Width, next = NextButton.GetPreferredSize(Size.Empty).Width, gap = Scale(4);
            return Rectangle.FromLTRB(area.Left + prev + gap, area.Top, Math.Max(area.Left + prev + gap + 1, area.Right - next - gap), area.Bottom);
        }
    }

    // Çipin kaydırılmamış içerikteki başlangıcı.
    private int ContentLeft(Control chip)
    {
        int x = 0, gap = Scale(_gap);
        foreach (var c in LaidOutChips)
        {
            if (c == chip) return x;
            x += c.Width + gap;
        }
        return x;
    }

    public override LayoutEngine LayoutEngine => ChipLayout.Instance;

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is { } chip && chip != PreviousButton && chip != NextButton) chip.MouseWheel += OnChipWheel;
        PerformLayout();
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        if (e.Control is { } chip) chip.MouseWheel -= OnChipWheel;
        PerformLayout();
    }

    // Tekerlek çipin üstündeyken de şeridi kaydırır; dikey tekerlek yatay kaydırmaya çevrilir.
    // Çipin işlemediği tekerlek mesajı üst pencereye de gider: işlendi sayılmazsa şerit iki kez kayar.
    private void OnChipWheel(object? sender, MouseEventArgs e)
    {
        Wheel(e.Delta);
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Wheel(e.Delta);
    }

    private void Wheel(int delta)
    {
        if (MaxScrollOffset == 0 || delta == 0) return;
        ScrollBy(-Math.Sign(delta) * Scale(WheelStep) * Math.Max(1, Math.Abs(delta) / 120));
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int step = TwRtl.NeedsMirroring(this) ? -Scale(WheelStep) : Scale(WheelStep);
        if (e.KeyCode == Keys.Left) ScrollBy(-step);
        else if (e.KeyCode == Keys.Right) ScrollBy(step);
        else return;
        e.Handled = true;
    }

    internal void Arrange()
    {
        if (_arranging) return;
        _arranging = true;
        try
        {
            var area = Area;
            var chips = LaidOutChips;
            int gap = Scale(_gap);
            var sizes = chips.Select(ChipSize).ToArray();
            _contentWidth = ContentWidthOf(chips);
            _offset = Math.Max(0, Math.Min(MaxScrollOffset, _offset));

            var view = Viewport;
            bool overflow = _contentWidth > area.Width;
            bool rtl = TwRtl.NeedsMirroring(this);
            int x = view.Left - _offset;
            for (int i = 0; i < chips.Length; i++)
            {
                var size = sizes[i];
                if (_chipAlignment == TwAlign.Stretch) size.Height = Math.Max(1, area.Height);
                int left = rtl ? view.Left + view.Right - x - size.Width : x;
                chips[i].SetBounds(left, ChipTop(area, size.Height), size.Width, size.Height);
                x += size.Width + gap;
            }

            // Uçlardaki düğmeler yalnızca o yönde gizli çip varken görünür.
            bool before = overflow && _offset > 0, after = overflow && _offset < MaxScrollOffset;
            PlaceButton(PreviousButton, area, atLeft: !rtl, show: before);
            PlaceButton(NextButton, area, atLeft: rtl, show: after);
        }
        finally
        {
            _arranging = false;
        }
    }

    // A chip taller than the strip keeps its size and starts at the top: the strip clips the rest.
    private int ChipTop(Rectangle area, int height)
    {
        int free = area.Height - height;
        if (free <= 0) return area.Top;
        return _chipAlignment switch
        {
            TwAlign.End => area.Top + free,
            TwAlign.Center => area.Top + free / 2,
            _ => area.Top,
        };
    }

    private static void PlaceButton(TwButton button, Rectangle area, bool atLeft, bool show)
    {
        var size = button.GetPreferredSize(Size.Empty);
        size.Height = Math.Min(size.Height, Math.Max(1, area.Height));
        button.SetBounds(atLeft ? area.Left : area.Right - size.Width, area.Top + (area.Height - size.Height) / 2, size.Width, size.Height);
        if (button.Visible != show) button.Visible = show;
        // Düğme kaydırılan çiplerin üstünde kalır.
        if (show && button.Parent is { } parent && parent.Controls.GetChildIndex(button) > 1) button.BringToFront();
    }

    private sealed class ChipLayout : LayoutEngine
    {
        public static readonly ChipLayout Instance = new ChipLayout();

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            if (container is TwChipStrip strip) strip.Arrange();
            return false;
        }
    }
}
