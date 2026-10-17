using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Veri listesi renkleri; FormWind class'lardan doldurur.</summary>
internal sealed class TwDataListColors
{
    public Color Back { get; set; } = Color.White;
    public Color Fore { get; set; } = ColorTranslator.FromHtml("#111827");          // gray-900
    public Color Muted { get; set; } = ColorTranslator.FromHtml("#6b7280");         // gray-500
    public Color Hover { get; set; } = ColorTranslator.FromHtml("#f3f4f6");         // gray-100
    public Color Selected { get; set; } = ColorTranslator.FromHtml("#eff6ff");      // blue-50
    public Color SelectedFore { get; set; } = ColorTranslator.FromHtml("#1e3a8a");  // blue-900
    public Color Divider { get; set; } = Color.Empty;
    public Color Thumb { get; set; } = ColorTranslator.FromHtml("#d1d5db");         // gray-300
    public Color GroupBack { get; set; } = Color.Empty;                             // boşsa Back
    public Color GroupFore { get; set; } = ColorTranslator.FromHtml("#6b7280");     // gray-500
    public Color Skeleton { get; set; } = ColorTranslator.FromHtml("#e5e7eb");      // gray-200
}

public enum TwListSelectionMode
{
    /// <summary>Single row.</summary>
    One,

    /// <summary>Ctrl+click adds/removes, Shift+click and Shift+arrow select a range, Ctrl+A selects all.</summary>
    MultiExtended,
}

/// <summary>The range requested when unloaded rows become visible.</summary>
public sealed class TwItemsNeededEventArgs : EventArgs
{
    public TwItemsNeededEventArgs(int first, int last)
    {
        First = first;
        Last = last;
    }

    public int First { get; }

    public int Last { get; }
}

/// <summary>Selection set; stores the "all selected" state without keeping millions of indices.</summary>
internal sealed class TwListSelection
{
    private readonly HashSet<int> _set = new HashSet<int>();
    private bool _all;     // true: _set dışındakiler seçili

    public bool Contains(int index) => _all ? !_set.Contains(index) : _set.Contains(index);

    public int Count(int itemCount) => _all ? itemCount - _set.Count(i => i < itemCount) : _set.Count(i => i < itemCount);

    public void Clear()
    {
        _all = false;
        _set.Clear();
    }

    public void SelectAll()
    {
        _all = true;
        _set.Clear();
    }

    public void Set(int index, bool selected)
    {
        if (selected == !_all) _set.Add(index);
        else _set.Remove(index);
    }

    public void SetRange(int from, int to, bool selected)
    {
        for (int i = Math.Min(from, to); i <= Math.Max(from, to); i++) Set(i, selected);
    }

    public IEnumerable<int> Enumerate(int itemCount)
    {
        if (!_all)
        {
            foreach (var i in _set.Where(i => i < itemCount).OrderBy(i => i)) yield return i;
            yield break;
        }
        for (int i = 0; i < itemCount; i++)
            if (!_set.Contains(i)) yield return i;
    }
}

/// <summary>Drawing of a row; the row's colors and common drawing helpers are provided.</summary>
public sealed class TwDataListDrawEventArgs : EventArgs
{
    private readonly TwDataList _owner;

    internal TwDataListDrawEventArgs(TwDataList owner, Graphics graphics, int index, Rectangle bounds, bool selected, bool hovered)
    {
        _owner = owner;
        Graphics = graphics;
        Index = index;
        Bounds = bounds;
        Selected = selected;
        Hovered = hovered;
    }

    public Graphics Graphics { get; }

    public int Index { get; }

    /// <summary>The whole row (including horizontal padding).</summary>
    public Rectangle Bounds { get; }

    /// <summary>The row's content area with <see cref="Control.Padding"/> applied.</summary>
    public Rectangle ContentBounds
    {
        get
        {
            var padding = _owner.ScaledItemPadding;
            return new Rectangle(Bounds.X + padding.Left, Bounds.Y + padding.Top, Math.Max(0, Bounds.Width - padding.Horizontal), Math.Max(0, Bounds.Height - padding.Vertical));
        }
    }

    public bool Selected { get; }

    public bool Hovered { get; }

    /// <summary>The row's primary text color (the selected text color when selected).</summary>
    public Color ForeColor => Selected ? _owner.Colors.SelectedFore : _owner.Colors.Fore;

    /// <summary>Secondary text color.</summary>
    public Color MutedColor => Selected ? Blend(_owner.Colors.SelectedFore, 170) : _owner.Colors.Muted;

    /// <summary>The row's background color (for text anti-aliasing).</summary>
    public Color BackColor => Selected ? _owner.Colors.Selected : Hovered ? _owner.Colors.Hover : _owner.Colors.Back;

    public Font Font => _owner.Font;

    /// <summary>Row font in bold (or another weight); comes from the cache and is not disposed.</summary>
    public Font FontWithWeight(int weight) => TwFonts.Create(new TwStyle { FontWeight = weight }, _owner.Font, TwTheme.Current);

    /// <summary>Single-line text truncated with "…".</summary>
    public void DrawText(string? text, Rectangle bounds, Color? color = null, Font? font = null,
                         ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        if (string.IsNullOrEmpty(text)) return;
        var flags = TwPainter.ToTextFormatFlags(alignment) | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(Graphics, text, font ?? Font, bounds, color ?? ForeColor, BackColor, flags);
    }

    /// <summary>A named icon (SVG or icon font glyph; see <see cref="TwIconRenderer.DrawIcon"/>).</summary>
    public void DrawIcon(string name, Rectangle bounds, Color? color = null)
    {
        TwIconRenderer.DrawIcon(Graphics, name, TwIconRenderer.EmPixels(Font.SizeInPoints * 1.15f, TwDpi.DeviceDpi(_owner)), bounds, color ?? MutedColor);
    }

    /// <summary>Rounded badge sized to its text, with its right edge at <paramref name="right"/>. Returns the badge's left edge.</summary>
    public int DrawBadge(string text, int right, Color back, Color fore)
    {
        var font = FontWithWeight(500);
        var size = TextRenderer.MeasureText(Graphics, text, font, Size.Empty, TextFormatFlags.NoPadding);
        int padX = _owner.ScalePixels(8), height = size.Height + _owner.ScalePixels(4);
        var rect = new Rectangle(right - size.Width - padX * 2, Bounds.Y + (Bounds.Height - height) / 2, size.Width + padX * 2, height);
        TwPainter.FillRounded(Graphics, rect, height / 2f, back);
        TextRenderer.DrawText(Graphics, text, font, rect, fore, back,
            TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        return rect.X;
    }

    private Color Blend(Color color, int alpha)
    {
        var back = BackColor;
        int Mix(int fore, int under) => (fore * alpha + under * (255 - alpha)) / 255;
        return Color.FromArgb(Mix(color.R, back.R), Mix(color.G, back.G), Mix(color.B, back.B));
    }
}

/// <summary>
/// Virtual data list: only visible rows are drawn, so millions of rows are fine.
/// Rows are drawn with <see cref="DrawItem"/> (otherwise <see cref="ItemText"/> as a single line of text). The mouse wheel
/// scrolls smoothly; the thin scrollbar is owner-drawn too (draggable) and needs no extra setup in dark mode.
/// Keyboard: ↑/↓, PageUp/PageDown, Home/End; Enter and double-click raise <see cref="ItemActivate"/>.
/// Styles: <c>bg-* text-*</c> (list), <c>item:hover:bg-*</c>, <c>selected:bg-* selected:text-*</c>, <c>item:rounded-*</c>,
/// <c>row:border-*</c> (row separator), <c>fill-*</c> (scrollbar).
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(DrawItem))]
[Description("Stillenebilir sanal veri listesi.")]
public class TwDataList : Control
{
    private int _itemCount;
    private int _itemHeight = 40;
    private Func<int, int>? _itemHeights;
    // ItemHeights verildiğinde satırların içerikteki başlangıçları (ölçeklenmiş, grup başlıkları hariç); uzunluk ItemCount + 1.
    private int[]? _offsets;
    private int _offsetsDpi;
    private int _selectedIndex = -1;
    private int _hoverIndex = -1;
    private float _offset;
    private float _target;
    private int _itemRadius = 6;
    private Padding _itemPadding = new Padding(12, 0, 12, 0);
    private TwDataListColors _colors = new TwDataListColors();
    private readonly Timer _scrollTimer = new Timer { Interval = 15 };
    private bool _draggingThumb;
    private int _dragStartY;
    private float _dragStartOffset;
    private bool _thumbHover;
    private TwListSelectionMode _selectionMode;
    private readonly TwListSelection _selection = new TwListSelection();
    private int _anchor = -1;
    private int[] _groupStarts = Array.Empty<int>();
    private string[] _groupTitles = Array.Empty<string>();
    private int _groupHeaderHeight = 28;
    private int _requestedFirst = -1, _requestedLast = -1;
    private bool _itemsNeededPosted;
    private TwTween? _hoverFade;
    private Timer? _pulse;

    // Odak halkası yalnızca odak klavyeyle geldiyse çizilir (bkz. TwFocusVisible).
    private bool _focusVisible;

    public TwDataList()
    {
        TwFocusVisible.EnsureInstalled();
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Padding = new Padding(4);
        _scrollTimer.Tick += (_, _) => StepScroll();
    }

    /// <summary>Drawing of a visible row.</summary>
    public event EventHandler<TwDataListDrawEventArgs>? DrawItem;

    public event EventHandler? SelectedIndexChanged;

    /// <summary>When the selected rows change (also with multi-selection).</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>For visible but unloaded rows (see <see cref="IsItemLoaded"/>); call <see cref="Control.Invalidate()"/> when the data arrives.</summary>
    public event EventHandler<TwItemsNeededEventArgs>? ItemsNeeded;

    /// <summary>Drawing of a group header; the header text is drawn when not handled.</summary>
    public event EventHandler<TwDataListDrawEventArgs>? DrawGroupHeader;

    /// <summary>Enter or double-click.</summary>
    public event EventHandler? ItemActivate;

    /// <summary>Number of rows; the data is not stored in the list, rows are requested by index while drawing.</summary>
    [Category("Data"), DefaultValue(0)]
    public int ItemCount
    {
        get => _itemCount;
        set
        {
            _itemCount = Math.Max(0, value);
            _offsets = null;
            if (_selectedIndex >= _itemCount) SelectedIndex = _itemCount - 1;
            _requestedFirst = _requestedLast = -1;
            _hoverIndex = -1;
            ScrollTo(_target, animate: false);
        }
    }

    /// <summary>Row height in 96 DPI pixels.</summary>
    [Category("Appearance"), DefaultValue(40)]
    public int ItemHeight
    {
        get => _itemHeight;
        set
        {
            _itemHeight = Math.Max(8, value);
            _offsets = null;
            ScrollTo(_target, animate: false);
        }
    }

    /// <summary>
    /// Height per row (96 DPI pixels); when not set, all rows are <see cref="ItemHeight"/> tall. Row offsets are computed
    /// once and cached; call <see cref="InvalidateItemHeights"/> when the data changes and the heights differ. Because a
    /// height is computed for every row, keep it cheap for very large virtual lists (<see cref="ItemsNeeded"/>).
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<int, int>? ItemHeights
    {
        get => _itemHeights;
        set
        {
            _itemHeights = value;
            InvalidateItemHeights();
        }
    }

    /// <summary>The heights returned by <see cref="ItemHeights"/> changed; rows are laid out again.</summary>
    public void InvalidateItemHeights()
    {
        _offsets = null;
        _hoverIndex = -1;
        ScrollTo(_target, animate: false);
        Invalidate();
    }

    // İçerik koordinatında satırın başlangıcı (grup başlıkları hariç).
    private int Offset(int index)
    {
        index = Math.Max(0, Math.Min(index, _itemCount));
        if (_itemHeights is null) return index * ScaledItemHeight;
        int dpi = TwDpi.DeviceDpi(this);
        if (_offsets is null || _offsets.Length != _itemCount + 1 || _offsetsDpi != dpi)
        {
            var offsets = new int[_itemCount + 1];
            for (int i = 0; i < _itemCount; i++)
                offsets[i + 1] = offsets[i] + Math.Max(1, ScalePixels(Math.Max(1, _itemHeights(i))));
            _offsets = offsets;
            _offsetsDpi = dpi;
        }
        return _offsets[index];
    }

    private int HeightOf(int index) => _itemHeights is null ? ScaledItemHeight : Offset(index + 1) - Offset(index);

    // from'dan başlayarak y piksel aşağıdaki satır (limit'e kadar; aşılırsa limit).
    private int IndexAtOffset(int y, int from, int limit)
    {
        if (y < 0) return from;
        if (_itemHeights is null) return Math.Min(limit, from + y / ScaledItemHeight);
        int target = Offset(from) + y, lo = from, hi = limit - 1, found = limit;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (Offset(mid + 1) > target)
            {
                found = mid;
                hi = mid - 1;
            }
            else lo = mid + 1;
        }
        return found;
    }

    /// <summary>Row corner radius (96 DPI); also set with <c>item:rounded-*</c>.</summary>
    [Category("Appearance"), DefaultValue(6)]
    public int ItemRadius
    {
        get => _itemRadius;
        set
        {
            _itemRadius = Math.Max(0, value);
            Invalidate();
        }
    }

    /// <summary>Row padding (96 DPI); determines <see cref="TwDataListDrawEventArgs.ContentBounds"/>.</summary>
    [Category("Layout")]
    public Padding ItemPadding
    {
        get => _itemPadding;
        set
        {
            _itemPadding = value;
            Invalidate();
        }
    }

    private bool ShouldSerializeItemPadding() => _itemPadding != new Padding(12, 0, 12, 0);

    private void ResetItemPadding() => ItemPadding = new Padding(12, 0, 12, 0);

    /// <summary>Row text when there is no <see cref="DrawItem"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<int, string>? ItemText { get; set; }

    /// <summary>
    /// Row name read by screen readers. When not set, <see cref="ItemText"/> is used, otherwise "Item N";
    /// recommended for lists drawn with <see cref="DrawItem"/>.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<int, string>? AccessibleItemText { get; set; }

    [Category("Behavior"), DefaultValue(true)]
    public bool SmoothScrolling { get; set; } = true;

    [Category("Behavior"), DefaultValue(TwListSelectionMode.One)]
    public TwListSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (_selectionMode == value) return;
            _selectionMode = value;
            _selection.Clear();
            if (_selectedIndex >= 0) _selection.Set(_selectedIndex, true);
            _anchor = _selectedIndex;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Whether a row is loaded (when virtual data arrives page by page). Unloaded rows are drawn as skeletons and
    /// <see cref="ItemsNeeded"/> is raised with the visible range.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<int, bool>? IsItemLoaded { get; set; }

    /// <summary>Group header height (96 DPI); styled with <c>header:bg-* header:text-*</c>.</summary>
    [Category("Appearance"), DefaultValue(28)]
    public int GroupHeaderHeight
    {
        get => _groupHeaderHeight;
        set
        {
            _groupHeaderHeight = Math.Max(8, value);
            ScrollTo(_target, animate: false);
        }
    }

    [Browsable(false)]
    public int GroupCount => _groupStarts.Length;

    /// <summary>
    /// Groups the rows: each group with the row index it starts at and its title (in ascending order). The header of the
    /// group at the top sticks while scrolling.
    /// </summary>
    public void SetGroups(IEnumerable<(int Start, string Title)> groups)
    {
        var list = (groups ?? Enumerable.Empty<(int, string)>()).OrderBy(g => g.Item1).ToList();
        _groupStarts = list.Select(g => Math.Max(0, g.Item1)).ToArray();
        _groupTitles = list.Select(g => g.Item2 ?? "").ToArray();
        ScrollTo(_target, animate: false);
    }

    public void ClearGroups() => SetGroups(Array.Empty<(int, string)>());

    public string GetGroupTitle(int group) => _groupTitles[group];

    /// <summary>The row's group (-1 when there are no groups or the row is before the first group).</summary>
    public int GroupOf(int index)
    {
        int lo = 0, hi = _groupStarts.Length - 1, found = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (_groupStarts[mid] <= index)
            {
                found = mid;
                lo = mid + 1;
            }
            else hi = mid - 1;
        }
        return found;
    }

    /// <summary>The group header's position in client coordinates (its place in the flow, not the sticky position).</summary>
    public Rectangle GetGroupHeaderBounds(int group) =>
        new Rectangle(Padding.Left, HeaderTop(group) - ScrollOffset, RowWidth, ScaledGroupHeaderHeight);

    public bool IsSelected(int index) =>
        _selectionMode == TwListSelectionMode.One ? index == _selectedIndex : index >= 0 && index < _itemCount && _selection.Contains(index);

    /// <summary>Selected rows (in ascending order).</summary>
    [Browsable(false)]
    public IReadOnlyList<int> SelectedIndices =>
        _selectionMode == TwListSelectionMode.One
            ? (_selectedIndex >= 0 ? new[] { _selectedIndex } : Array.Empty<int>())
            : _selection.Enumerate(_itemCount).ToList();

    [Browsable(false)]
    public int SelectedCount => _selectionMode == TwListSelectionMode.One ? (_selectedIndex >= 0 ? 1 : 0) : _selection.Count(_itemCount);

    public void SetSelected(int index, bool selected)
    {
        if (index < 0 || index >= _itemCount) return;
        if (_selectionMode == TwListSelectionMode.One)
        {
            if (selected) SelectedIndex = index;
            else if (index == _selectedIndex) SelectedIndex = -1;
            return;
        }
        _selection.Set(index, selected);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAll()
    {
        if (_selectionMode == TwListSelectionMode.One) return;
        _selection.SelectAll();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        _selection.Clear();
        _anchor = -1;
        if (_selectionMode == TwListSelectionMode.One) SelectedIndex = -1;
        else
        {
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal TwDataListColors Colors
    {
        get => _colors;
        set
        {
            _colors = value ?? new TwDataListColors();
            Invalidate();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            value = Math.Max(-1, Math.Min(value, _itemCount - 1));
            if (_selectionMode == TwListSelectionMode.MultiExtended)
            {
                _selection.Clear();
                if (value >= 0) _selection.Set(value, true);
                _anchor = value;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            SetCaret(value);
        }
    }

    // İmleç satırı (tek seçimde seçili satır); çoklu seçim kümesini değiştirmez.
    private void SetCaret(int value)
    {
        if (value == _selectedIndex)
        {
            Invalidate();
            return;
        }
        _selectedIndex = value;
        if (_selectionMode == TwListSelectionMode.One) SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
        if (value >= 0)
        {
            AccessibilityNotifyClients(AccessibleEvents.Selection, value);
            if (Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, value);
        }
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Scroll position (pixels); the momentary value during animation.</summary>
    [Browsable(false)]
    public int ScrollOffset => (int)Math.Round(_offset);

    [Browsable(false)]
    public int MaxScrollOffset => Math.Max(0, Offset(_itemCount) + VisibleGroupCount * ScaledGroupHeaderHeight + Padding.Vertical - ClientSize.Height);

    /// <summary>The row visible at the top (even partially).</summary>
    [Browsable(false)]
    public int TopIndex
    {
        get
        {
            if (_itemCount == 0) return -1;
            int y = ScrollOffset + Padding.Top;
            for (int i = 0; i < 3; i++, y += ScaledGroupHeaderHeight)
            {
                int index = RowAt(y);
                if (index >= 0) return index;
            }
            return Math.Min(_itemCount - 1, IndexAtOffset(ScrollOffset - Padding.Top, 0, _itemCount));
        }
    }

    internal int ScaledGroupHeaderHeight => ScalePixels(_groupHeaderHeight);

    // Satırı olan gruplar (başlangıcı ItemCount'tan küçük).
    private int VisibleGroupCount
    {
        get
        {
            int count = 0;
            foreach (var start in _groupStarts) if (start < _itemCount) count++;
            return count;
        }
    }

    private int RowWidth => Math.Max(0, ClientSize.Width - Padding.Horizontal - (MaxScrollOffset > 0 ? ScalePixels(10) : 0));

    // İçerik koordinatı (kaydırmasız): grup başlığı g'nin üstü.
    private int HeaderTop(int group) => Padding.Top + Offset(_groupStarts[group]) + group * ScaledGroupHeaderHeight;

    // İçerik koordinatı: satırın üstü.
    private int RowTop(int index) => Padding.Top + Offset(index) + (GroupOf(index) + 1) * ScaledGroupHeaderHeight;

    // İçerik koordinatındaki satır; grup başlığının üzerindeyse -2 - grup, boşsa -1.
    private int RowAt(int contentY)
    {
        int lo = 0, hi = _groupStarts.Length - 1, group = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (HeaderTop(mid) <= contentY)
            {
                group = mid;
                lo = mid + 1;
            }
            else hi = mid - 1;
        }

        int baseY, baseIndex, limit;
        if (group < 0)
        {
            baseY = Padding.Top;
            baseIndex = 0;
            limit = _groupStarts.Length > 0 ? Math.Min(_itemCount, _groupStarts[0]) : _itemCount;
        }
        else
        {
            if (contentY < HeaderTop(group) + ScaledGroupHeaderHeight) return -2 - group;
            baseY = HeaderTop(group) + ScaledGroupHeaderHeight;
            baseIndex = _groupStarts[group];
            limit = group + 1 < _groupStarts.Length ? Math.Min(_itemCount, _groupStarts[group + 1]) : _itemCount;
        }
        if (contentY < baseY) return -1;
        int index = IndexAtOffset(contentY - baseY, baseIndex, limit);
        return index < limit ? index : -1;
    }

    internal int ScaledItemHeight => ScalePixels(_itemHeight);

    internal Padding ScaledItemPadding => new Padding(ScalePixels(_itemPadding.Left), ScalePixels(_itemPadding.Top), ScalePixels(_itemPadding.Right), ScalePixels(_itemPadding.Bottom));

    internal int ScalePixels(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    private int ScrollbarWidth => ScalePixels(_draggingThumb || _thumbHover ? 8 : 6);

    /// <summary>The row's position in client coordinates (computed even when not visible).</summary>
    public Rectangle GetItemBounds(int index)
    {
        return new Rectangle(Padding.Left, RowTop(index) - ScrollOffset, RowWidth, HeightOf(index));
    }

    /// <summary>The row at a point; -1 when none.</summary>
    public int IndexFromPoint(Point point)
    {
        if (_itemCount == 0 || point.X < Padding.Left || point.X >= ClientSize.Width - Padding.Right) return -1;
        if (point.Y < Padding.Top || StickyHeaderBounds(out _).Contains(point)) return -1;
        int index = RowAt(point.Y + ScrollOffset);
        return index >= 0 ? index : -1;
    }

    /// <summary>Scrolls until the row is fully visible.</summary>
    public void EnsureVisible(int index, bool animate = true)
    {
        if (index < 0 || index >= _itemCount) return;
        int top = RowTop(index), bottom = top + HeightOf(index);
        // Yapışık başlık satırın üstünü örtmesin.
        int stickyHeight = GroupOf(index) >= 0 ? ScaledGroupHeaderHeight : 0;
        float target = _target;
        if (top - stickyHeight < target + Padding.Top) target = top - stickyHeight - Padding.Top;
        else if (bottom > target + ClientSize.Height - Padding.Bottom) target = bottom - ClientSize.Height + Padding.Bottom;
        ScrollTo(target, animate);
    }

    /// <summary>Scrolls to the given position (clamped); smoothly if <see cref="SmoothScrolling"/> is on.</summary>
    public void ScrollTo(float offset, bool animate = true)
    {
        _target = Math.Max(0, Math.Min(offset, MaxScrollOffset));
        if (animate && SmoothScrolling && IsHandleCreated && Visible)
        {
            _scrollTimer.Start();
        }
        else
        {
            _scrollTimer.Stop();
            _offset = _target;
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ScrollTo(_target, animate: false);
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        ScrollTo(_target, animate: false);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int lines = SystemInformation.MouseWheelScrollLines;
        float step = lines < 0 ? ClientSize.Height : Math.Max(1, lines) * ScaledItemHeight;
        ScrollTo(_target - e.Delta / 120f * step);
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_draggingThumb)
        {
            var track = TrackBounds();
            var thumb = ThumbBounds();
            float range = track.Height - thumb.Height;
            if (range > 0) ScrollTo(_dragStartOffset + (e.Y - _dragStartY) / range * MaxScrollOffset, animate: false);
            return;
        }

        bool overThumb = MaxScrollOffset > 0 && HotTrackBounds().Contains(e.Location);
        int index = overThumb ? -1 : IndexFromPoint(e.Location);
        if (index != _hoverIndex || overThumb != _thumbHover)
        {
            // Yeni satırın hover zemini kısa sürede belirir.
            if (index >= 0 && index != _hoverIndex)
            {
                _hoverFade ??= new TwTween(this);
                _hoverFade.Start(120);
            }
            _hoverIndex = index;
            _thumbHover = overThumb;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex == -1 && !_thumbHover) return;
        _hoverIndex = -1;
        _thumbHover = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (CanFocus) Focus();
        if (e.Button != MouseButtons.Left) return;

        if (MaxScrollOffset > 0 && HotTrackBounds().Contains(e.Location))
        {
            var thumb = ThumbBounds();
            if (!thumb.Contains(new Point(thumb.X, e.Y)))
            {
                // Çubuğa tıklama: bir sayfa kaydır.
                ScrollTo(_target + (e.Y < thumb.Y ? -1 : 1) * (ClientSize.Height - ScaledItemHeight));
                return;
            }
            _draggingThumb = true;
            _dragStartY = e.Y;
            _dragStartOffset = _offset;
            Capture = true;
            Invalidate();
            return;
        }

        int index = IndexFromPoint(e.Location);
        if (index >= 0)
        {
            SelectWithModifiers(index, ModifierKeys);
            EnsureVisible(index);
        }
    }

    // Çoklu seçimde Ctrl ekler/çıkarır, Shift çapadan aralık seçer; değiştiricisiz tık tek seçim yapar.
    private void SelectWithModifiers(int index, Keys modifiers)
    {
        if (_selectionMode == TwListSelectionMode.One)
        {
            SelectedIndex = index;
            return;
        }
        bool ctrl = (modifiers & Keys.Control) != 0, shift = (modifiers & Keys.Shift) != 0;
        if (shift && _anchor >= 0)
        {
            if (!ctrl) _selection.Clear();
            _selection.SetRange(_anchor, index, true);
        }
        else if (ctrl)
        {
            _selection.Set(index, !_selection.Contains(index));
            _anchor = index;
        }
        else
        {
            _selection.Clear();
            _selection.Set(index, true);
            _anchor = index;
        }
        SetCaret(index);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_draggingThumb) return;
        _draggingThumb = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && IndexFromPoint(e.Location) is int index && index >= 0 && index == _selectedIndex && (ModifierKeys & (Keys.Control | Keys.Shift)) == 0)
            Activate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _focusVisible = true;
        if (_itemCount == 0) return;
        int page = Math.Max(1, (ClientSize.Height - Padding.Vertical) / ScaledItemHeight - 1);
        int current = _selectedIndex;
        int next = e.KeyCode switch
        {
            Keys.Up => Math.Max(0, current - 1),
            Keys.Down => Math.Min(_itemCount - 1, current + 1),
            Keys.PageUp => Math.Max(0, current - page),
            Keys.PageDown => Math.Min(_itemCount - 1, Math.Max(0, current) + page),
            Keys.Home => 0,
            Keys.End => _itemCount - 1,
            _ => current,
        };
        if (e.KeyCode == Keys.Enter && current >= 0)
        {
            Activate();
            e.Handled = true;
            return;
        }
        if (_selectionMode == TwListSelectionMode.MultiExtended)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                SelectAll();
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.Space && current >= 0)
            {
                SelectWithModifiers(current, Keys.Control);
                e.Handled = true;
                return;
            }
        }
        if (next == current) return;
        if (_selectionMode == TwListSelectionMode.MultiExtended && e.Control && !e.Shift) SetCaret(next);   // yalnızca imleç
        else if (_selectionMode == TwListSelectionMode.MultiExtended) SelectWithModifiers(next, e.Shift ? Keys.Shift : Keys.None);
        else SelectedIndex = next;
        EnsureVisible(next);
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _focusVisible = TwFocusVisible.LastInputWasKeyboard;
        Invalidate();
        if (_selectedIndex >= 0) AccessibilityNotifyClients(AccessibleEvents.Focus, _selectedIndex);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new TwDataListAccessibleObject(this);

    internal string GetAccessibleItemName(int index) =>
        AccessibleItemText?.Invoke(index) ?? ItemText?.Invoke(index) ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, TwTexts.Current.Item, index + 1);

    internal void Activate() => ItemActivate?.Invoke(this, EventArgs.Empty);

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var back = new SolidBrush(_colors.Back))
            g.FillRectangle(back, ClientRectangle);

        if (_itemCount > 0)
        {
            var clip = Rectangle.Intersect(e.ClipRectangle, new Rectangle(0, Padding.Top, ClientSize.Width, Math.Max(0, ClientSize.Height - Padding.Vertical)));
            var state = g.Save();
            g.SetClip(clip);
            float radius = ScalePixels(_itemRadius);
            int unloadedFirst = -1, unloadedLast = -1;

            // İlk görünen satır: üst kenardaki satır, başlık üzerindeyse o grubun ilk satırı.
            int top = clip.Top + ScrollOffset;
            int first = RowAt(top);
            if (first <= -2) first = _groupStarts[-2 - first];
            else if (first < 0) first = Math.Max(0, Math.Min(_itemCount - 1, IndexAtOffset(top - Padding.Top, 0, _itemCount)));
            first = Math.Max(0, first);

            for (int i = first; i < _itemCount; i++)
            {
                int group = GroupOf(i);
                if (group >= 0 && _groupStarts[group] == i) PaintGroupHeader(g, group, GetGroupHeaderBounds(group));

                var bounds = GetItemBounds(i);
                if (bounds.Top >= clip.Bottom) break;
                if (bounds.Bottom <= clip.Top) continue;
                bool selected = IsSelected(i), hovered = i == _hoverIndex;
                if (selected) TwPainter.FillRounded(g, bounds, radius, _colors.Selected);
                else if (hovered) TwPainter.FillRounded(g, bounds, radius, TwMotion.Blend(_colors.Back, _colors.Hover, _hoverFade?.Progress ?? 1f));
                else if (!_colors.Divider.IsEmpty && i < _itemCount - 1 && !IsSelected(i + 1) && GroupOf(i + 1) == group)
                {
                    using var pen = new Pen(_colors.Divider);
                    int inset = ScaledItemPadding.Left;
                    g.DrawLine(pen, bounds.Left + inset, bounds.Bottom - 1, bounds.Right - inset, bounds.Bottom - 1);
                }

                var args = new TwDataListDrawEventArgs(this, g, i, bounds, selected, hovered);
                if (IsItemLoaded != null && !IsItemLoaded(i))
                {
                    PaintSkeleton(g, args);
                    if (unloadedFirst < 0) unloadedFirst = i;
                    unloadedLast = i;
                }
                else OnDrawItem(args);

                if (i == _selectedIndex && Focused && _focusVisible)
                    TwPainter.StrokeRounded(g, bounds, radius, 1, Color.FromArgb(110, _colors.SelectedFore));
            }

            // Yapışık başlık: üstteki grubun başlığı, bir sonraki başlık gelince yukarı itilir.
            var sticky = StickyHeaderBounds(out var stickyGroup);
            if (stickyGroup >= 0) PaintGroupHeader(g, stickyGroup, sticky);
            g.Restore(state);

            RequestItems(unloadedFirst, unloadedLast);
            UpdatePulse(unloadedFirst >= 0);
        }

        if (MaxScrollOffset > 0)
        {
            var thumb = ThumbBounds();
            var color = _draggingThumb || _thumbHover ? _colors.Thumb : Color.FromArgb(200, _colors.Thumb);
            TwPainter.FillRounded(g, thumb, thumb.Width / 2f, color);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scrollTimer.Dispose();
            _hoverFade?.Dispose();
            _pulse?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>Draws a row: raises <see cref="DrawItem"/> if handled, otherwise writes <see cref="ItemText"/>.</summary>
    protected virtual void OnDrawItem(TwDataListDrawEventArgs e)
    {
        if (DrawItem != null) DrawItem(this, e);
        else e.DrawText(ItemText?.Invoke(e.Index) ?? e.Index.ToString(System.Globalization.CultureInfo.CurrentCulture), e.ContentBounds);
    }

    // Akıştaki başlığı yukarı kaydırılmış grubun başlığı, liste üstüne yapışık.
    private Rectangle StickyHeaderBounds(out int group)
    {
        group = -1;
        if (_groupStarts.Length == 0) return Rectangle.Empty;
        int contentTop = ScrollOffset + Padding.Top;
        int lo = 0, hi = _groupStarts.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (HeaderTop(mid) < contentTop)
            {
                group = mid;
                lo = mid + 1;
            }
            else hi = mid - 1;
        }
        if (group < 0 || _groupStarts[group] >= _itemCount) return Rectangle.Empty;
        int y = Padding.Top;
        if (group + 1 < _groupStarts.Length) y = Math.Min(y, HeaderTop(group + 1) - ScrollOffset - ScaledGroupHeaderHeight);
        return new Rectangle(Padding.Left, y, RowWidth, ScaledGroupHeaderHeight);
    }

    private void PaintGroupHeader(Graphics g, int group, Rectangle bounds)
    {
        var back = _colors.GroupBack.IsEmpty ? _colors.Back : _colors.GroupBack;
        using (var brush = new SolidBrush(back))
            g.FillRectangle(brush, bounds);
        var args = new TwDataListDrawEventArgs(this, g, group, bounds, false, false);
        if (DrawGroupHeader != null)
        {
            DrawGroupHeader(this, args);
            return;
        }
        var padding = ScaledItemPadding;
        var text = new Rectangle(bounds.X + padding.Left, bounds.Y, Math.Max(0, bounds.Width - padding.Horizontal), bounds.Height);
        TextRenderer.DrawText(g, _groupTitles[group], args.FontWithWeight(600), text, _colors.GroupFore, back,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    /// <summary>Pulse factor of skeleton rows (0.55–1); 1 when animations are off.</summary>
    internal static float PulseFactor(Control control) =>
        TwMotion.CanAnimate(control) ? 0.55f + 0.45f * (0.5f + 0.5f * (float)Math.Sin(Environment.TickCount / 1200.0 * 2 * Math.PI)) : 1f;

    // İskelet satır görünürken yavaşça nabız atar; kalmayınca zamanlayıcı durur.
    private void UpdatePulse(bool skeletons)
    {
        if (skeletons && TwMotion.CanAnimate(this))
        {
            if (_pulse is null)
            {
                _pulse = new Timer { Interval = 50 };
                _pulse.Tick += (_, _) => Invalidate();
            }
            _pulse.Start();
        }
        else _pulse?.Stop();
    }

    // Yüklenmemiş satır: iki gri çubuk.
    private void PaintSkeleton(Graphics g, TwDataListDrawEventArgs args)
    {
        var skeleton = TwMotion.Blend(_colors.Back, _colors.Skeleton, PulseFactor(this));
        var content = args.ContentBounds;
        int bar = Math.Max(4, ScalePixels(8));
        float ratio = 0.35f + (args.Index * 37 % 30) / 100f;
        var first = new Rectangle(content.X, content.Y + content.Height / 2 - bar - ScalePixels(2), (int)(content.Width * ratio), bar);
        var second = new Rectangle(content.X, content.Y + content.Height / 2 + ScalePixels(2), (int)(content.Width * ratio * 0.6f), bar);
        TwPainter.FillRounded(g, first, bar / 2f, skeleton);
        TwPainter.FillRounded(g, second, bar / 2f, TwMotion.Blend(_colors.Back, skeleton, 0.65f));
    }

    // Çizim sırasında olay tetiklenmez; aralık mesaj kuyruğuna bırakılır, aynı aralık tekrar istenmez.
    private void RequestItems(int first, int last)
    {
        if (first < 0 || ItemsNeeded is null || (first == _requestedFirst && last == _requestedLast)) return;
        _requestedFirst = first;
        _requestedLast = last;
        if (_itemsNeededPosted || !IsHandleCreated) return;
        _itemsNeededPosted = true;
        BeginInvoke((Action)(() =>
        {
            _itemsNeededPosted = false;
            ItemsNeeded?.Invoke(this, new TwItemsNeededEventArgs(_requestedFirst, _requestedLast));
        }));
    }

    private Rectangle TrackBounds()
    {
        int margin = ScalePixels(3);
        return new Rectangle(ClientSize.Width - margin - ScrollbarWidth, margin, ScrollbarWidth, Math.Max(0, ClientSize.Height - margin * 2));
    }

    // Fare için daha geniş hedef alan.
    private Rectangle HotTrackBounds() => new Rectangle(ClientSize.Width - ScalePixels(12), 0, ScalePixels(12), ClientSize.Height);

    private Rectangle ThumbBounds()
    {
        var track = TrackBounds();
        float content = Offset(_itemCount) + VisibleGroupCount * ScaledGroupHeaderHeight + Padding.Vertical;
        int height = Math.Max(ScalePixels(24), (int)(track.Height * ClientSize.Height / Math.Max(1f, content)));
        height = Math.Min(height, track.Height);
        int y = track.Y + (int)((track.Height - height) * (MaxScrollOffset == 0 ? 0 : _offset / MaxScrollOffset));
        return new Rectangle(track.X, y, track.Width, height);
    }

    private void StepScroll()
    {
        float delta = _target - _offset;
        if (Math.Abs(delta) < 0.5f)
        {
            _offset = _target;
            _scrollTimer.Stop();
        }
        else
        {
            _offset += delta * 0.25f;
        }
        Invalidate();
    }
}
