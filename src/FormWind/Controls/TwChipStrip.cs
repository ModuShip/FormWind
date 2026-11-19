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
/// <c>MaximumSize</c>, past which it scrolls) and as tall as the tallest; hidden chips take no room, and a chip whose size
/// changes later (classes applied, text changed) resizes the strip. The ‹ › buttons are square, as tall as the chip row, follow
/// <see cref="ChipAlignment"/> and are laid over the chips only while shown: a hidden button takes no room.
/// <para>The scroll buttons are children too: walk <see cref="Chips"/>, not <c>Controls</c>, to rebuild the chips. A scroll
/// button that is removed or disposed anyway is put back (a new one when disposed).</para>
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
    private const int ButtonGap = 4;
    private const int MaxButtonWidth = 32;
    private const int ButtonCornerRadius = 6;
    private TwButton _previousButton;
    private TwButton _nextButton;
    private bool _restoreButtonsQueued;
    private bool _requestingParentLayout;
    private Size _lastPreferredSize;

    public TwChipStrip()
    {
        _previousButton = CreateScrollButton(previous: true);
        _nextButton = CreateScrollButton(previous: false);
        Controls.Add(_previousButton);
        Controls.Add(_nextButton);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    // Kaydırma düğmeleri kare ve elle boyutlanır (AutoSize Button 75×23'ün altına inmez); renkleri şeritten gelir.
    private TwButton CreateScrollButton(bool previous)
    {
        var button = new TwButton
        {
            IconName = previous ? "chevron-left" : "chevron-right",
            AutoSize = false,
            CornerRadius = ButtonCornerRadius,
            AccessibleName = previous ? TwTexts.Current.Previous : TwTexts.Current.Next,
            TabStop = false,
            Visible = false,
        };
        button.Click += (_, _) => ScrollBy(previous ? -PageStep : PageStep);
        return button;
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
    public int MaxScrollOffset => Math.Max(0, _contentWidth - Area.Width);

    /// <summary>The chips: the children other than the scroll buttons, in <c>Controls</c> order.</summary>
    [Browsable(false)]
    public Control[] Chips => Controls.Cast<Control>().Where(c => c != PreviousButton && c != NextButton).ToArray();

    /// <summary>The ‹ button (square, as tall as the chip row unless it is given <c>AutoSize</c>); style it with classes.</summary>
    [Browsable(false)]
    public TwButton PreviousButton => _previousButton;

    /// <summary>The › button (square, as tall as the chip row unless it is given <c>AutoSize</c>); style it with classes.</summary>
    [Browsable(false)]
    public TwButton NextButton => _nextButton;

    /// <summary>Scrolls by <paramref name="pixels"/> (device pixels; negative = toward the start).</summary>
    public void ScrollBy(int pixels) => ScrollOffset = _offset + pixels;

    /// <summary>Scrolls just enough to show <paramref name="chip"/> whole (as far as it fits).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="chip"/> is <c>null</c>.</exception>
    public void EnsureVisible(Control chip)
    {
        if (chip is null) throw new ArgumentNullException(nameof(chip));
        if (chip.Parent != this || chip == PreviousButton || chip == NextButton) return;
        int left = ContentLeft(chip), right = left + chip.Width;
        var area = Area;
        var view = Viewport;
        // Görünen düğmeler çiplerin üstüne biner: çip düğmelerin arasında kalacak kadar kaydırılır.
        if (left < _offset + (view.Left - area.Left)) ScrollOffset = Math.Max(0, left - ButtonReserve(PreviousButton, area));
        else if (right > _offset + (view.Right - area.Left)) ScrollOffset = right - area.Width + ButtonReserve(NextButton, area);
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
        return new Size(ContentWidthOf(chips) + Padding.Horizontal + border.Horizontal, RowHeightOf(chips) + Padding.Vertical + border.Vertical);
    }

    private static int RowHeightOf(Control[] chips) => chips.Length == 0 ? 0 : chips.Max(c => ChipSize(c).Height);

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

    // Çiplerin düğmelerle örtülmeyen kısmı, içerik sırasıyla (sol = ‹ tarafı; sağdan sola düzende de aynanmaz): yalnızca
    // görünen (o yönde gizli çip olan) düğme yer kaplar.
    private Rectangle Viewport
    {
        get
        {
            var area = Area;
            int max = MaxScrollOffset;
            if (max == 0) return area;
            int start = _offset > 0 ? ButtonReserve(PreviousButton, area) : 0;
            int end = _offset < max ? ButtonReserve(NextButton, area) : 0;
            return Rectangle.FromLTRB(area.Left + start, area.Top, Math.Max(area.Left + start + 1, area.Right - end), area.Bottom);
        }
    }

    private int ButtonReserve(TwButton button, Rectangle area) => ButtonSize(button, area).Width + Scale(ButtonGap);

    // Çip satırının yüksekliği: Stretch'te şeridin, yoksa en uzun çipin yüksekliği (şeride sığdığı kadar).
    private int RowHeight(Rectangle area)
    {
        int height = _chipAlignment == TwAlign.Stretch ? area.Height : RowHeightOf(LaidOutChips);
        return Math.Max(1, Math.Min(height, area.Height));
    }

    // AutoSize verilmiş düğme kendi boyutunu alır; yoksa çip satırı kadar uzun bir kare (genişliği sınırlı).
    private Size ButtonSize(TwButton button, Rectangle area)
    {
        if (button.AutoSize)
        {
            var preferred = button.GetPreferredSize(Size.Empty);
            return new Size(preferred.Width, Math.Max(1, Math.Min(preferred.Height, area.Height)));
        }
        int height = RowHeight(area);
        return new Size(Math.Min(height, Scale(MaxButtonWidth)), height);
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
        if (e.Control == _previousButton || e.Control == _nextButton) QueueRestoreButtons();
        PerformLayout();
    }

    // "Ekle düğmesi dışındaki her şeyi sil" kodu Controls'u dolaşıp kaydırma düğmelerini de silebilir. Düğmeler hemen geri
    // eklenmez (Controls'u boşaltan bir döngü hiç bitmezdi); ileti döngüsünde sırası gelince geri konur.
    private void QueueRestoreButtons()
    {
        if (_restoreButtonsQueued || IsDisposed || Disposing || !IsHandleCreated) return;   // tutamaç yoksa OnHandleCreated geri koyar
        _restoreButtonsQueued = true;
        BeginInvoke((Action)RestoreButtons);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RestoreButtons();
    }

    internal void RestoreButtons()
    {
        _restoreButtonsQueued = false;
        if (IsDisposed || Disposing) return;
        if (_previousButton.IsDisposed || _previousButton.Disposing) _previousButton = CreateScrollButton(previous: true);
        if (_nextButton.IsDisposed || _nextButton.Disposing) _nextButton = CreateScrollButton(previous: false);
        if (_previousButton.Parent != this) Controls.Add(_previousButton);
        if (_nextButton.Parent != this) Controls.Add(_nextButton);
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

            // Çipler düğmelere yer ayırmadan dizilir; görünen düğme onların üstüne biner.
            bool rtl = TwRtl.NeedsMirroring(this);
            int x = area.Left - _offset;
            for (int i = 0; i < chips.Length; i++)
            {
                var size = sizes[i];
                if (_chipAlignment == TwAlign.Stretch) size.Height = Math.Max(1, area.Height);
                int left = rtl ? area.Left + area.Right - x - size.Width : x;
                chips[i].SetBounds(left, ChipTop(area, size.Height), size.Width, size.Height);
                x += size.Width + gap;
            }

            // Uçlardaki düğmeler yalnızca o yönde gizli çip varken görünür.
            int max = MaxScrollOffset;
            PlaceButton(PreviousButton, area, atLeft: !rtl, show: max > 0 && _offset > 0);
            PlaceButton(NextButton, area, atLeft: rtl, show: max > 0 && _offset < max);
        }
        finally
        {
            _arranging = false;
        }
        RequestParentLayoutIfResized();
    }

    // Bir çipin boyutu sonradan değişirse (sınıflar uygulanıp yazı büyüyünce) AutoSize şeridin üst denetimi yeniden
    // yerleşir; WinForms'un tercih boyutu değişen AutoSize denetimler için yaptığı gibi. Tercih boyutu değişmedikçe
    // istek yinelenmez: üst denetim o boyutu veremese de döngüye girilmez.
    private void RequestParentLayoutIfResized()
    {
        if (!AutoSize || Parent is not { } parent || _requestingParentLayout) return;
        var preferred = GetPreferredSize(Size.Empty);
        if (preferred == _lastPreferredSize) return;
        _lastPreferredSize = preferred;
        if (!NeedsResize(preferred)) return;
        var before = Size;
        _requestingParentLayout = true;
        try
        {
            parent.PerformLayout(this, "PreferredSize");
        }
        finally
        {
            _requestingParentLayout = false;
        }
        // Şerit kendi yerleşiminin içindeyken yeni boyutuyla yerleşmesi ertelenir (WinForms); çipler ve düğmeler hemen dizilir.
        if (Size != before) Arrange();
    }

    private bool NeedsResize(Size preferred)
    {
        var target = Constrain(preferred);
        bool width = Dock is not (DockStyle.Top or DockStyle.Bottom or DockStyle.Fill);
        bool height = Dock is not (DockStyle.Left or DockStyle.Right or DockStyle.Fill);
        bool growOnly = AutoSizeMode == AutoSizeMode.GrowOnly;
        return width && (growOnly ? target.Width > Width : target.Width != Width)
            || height && (growOnly ? target.Height > Height : target.Height != Height);
    }

    private Size Constrain(Size size)
    {
        int width = Math.Max(size.Width, MinimumSize.Width), height = Math.Max(size.Height, MinimumSize.Height);
        if (MaximumSize.Width > 0) width = Math.Min(width, MaximumSize.Width);
        if (MaximumSize.Height > 0) height = Math.Min(height, MaximumSize.Height);
        return new Size(width, height);
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

    // Düğme çip satırıyla aynı hizada durur (ChipAlignment); Stretch'te satır şerit kadar uzundur.
    private void PlaceButton(TwButton button, Rectangle area, bool atLeft, bool show)
    {
        var size = ButtonSize(button, area);
        int rowHeight = RowHeight(area);
        int top = ChipTop(area, rowHeight) + (rowHeight - size.Height) / 2;
        button.SetBounds(atLeft ? area.Left : area.Right - size.Width, top, size.Width, size.Height);
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
