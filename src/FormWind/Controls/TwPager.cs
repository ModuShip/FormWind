using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Pager: ‹ 1 … 4 5 6 … 20 ›. The page count is computed from <see cref="TotalItems"/> and <see cref="PageSize"/>;
/// bind the data for the page in <see cref="PageChanged"/>. Styles: <c>text-*</c> (pages), <c>selected:bg-* selected:text-*</c>
/// (current page), <c>item:hover:bg-*</c>. Keyboard: ←/→ previous/next, Home/End first/last page.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(PageChanged))]
[Description("Stylable pager.")]
public class TwPager : Control
{
    private int _totalItems;
    private int _pageSize = 20;
    private int _page = 1;
    private int _hover = int.MinValue;
    private bool _focusVisible;
    private readonly List<(int Page, Rectangle Bounds)> _buttons = new List<(int, Rectangle)>();

    /// <summary>Page values reserved for the previous/next buttons and "…".</summary>
    public const int Previous = -1, Next = -2, Gap = 0;

    public TwPager()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        TwFocusVisible.EnsureInstalled();
        TabStop = true;
        BackColor = Color.Transparent;
    }

    public event EventHandler? PageChanged;

    [Category("Data"), DefaultValue(0)]
    public int TotalItems
    {
        get => _totalItems;
        set
        {
            _totalItems = Math.Max(0, value);
            Page = _page;
            Relayout();
        }
    }

    [Category("Data"), DefaultValue(20)]
    public int PageSize
    {
        get => _pageSize;
        set
        {
            _pageSize = Math.Max(1, value);
            Page = _page;
            Relayout();
        }
    }

    [Browsable(false)]
    public int PageCount => Math.Max(1, (_totalItems + _pageSize - 1) / _pageSize);

    /// <summary>The current page (starts at 1).</summary>
    [Category("Data"), DefaultValue(1)]
    public int Page
    {
        get => _page;
        set
        {
            value = Math.Max(1, Math.Min(PageCount, value));
            if (value == _page)
            {
                Relayout();
                return;
            }
            _page = value;
            Relayout();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            PageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Index of the current page's first item (from 0).</summary>
    [Browsable(false)]
    public int FirstItemIndex => (_page - 1) * _pageSize;

    /// <summary>Number of items on the current page.</summary>
    [Browsable(false)]
    public int ItemsOnPage => Math.Max(0, Math.Min(_pageSize, _totalItems - FirstItemIndex));

    /// <summary>Number of pages shown on each side of the current page.</summary>
    [Category("Behavior"), DefaultValue(1)]
    public int Siblings { get; set; } = 1;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectedBackColor { get; set; } = ColorTranslator.FromHtml("#2563eb");

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectedForeColor { get; set; } = Color.White;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverBackColor { get; set; } = ColorTranslator.FromHtml("#f3f4f6");

    /// <summary>The buttons shown: page numbers, <see cref="Gap"/> ("…"), <see cref="Previous"/>, <see cref="Next"/>.</summary>
    public IReadOnlyList<int> VisiblePages()
    {
        var pages = new List<int> { Previous };
        int count = PageCount, siblings = Math.Max(0, Siblings);
        int from = Math.Max(2, _page - siblings), to = Math.Min(count - 1, _page + siblings);
        // Uçlara yakınken boşluk yerine sayfa göster (1 2 3 4 … 20).
        if (from <= 3) from = 2;
        if (to >= count - 2) to = count - 1;
        pages.Add(1);
        if (from > 2) pages.Add(Gap);
        for (int p = from; p <= to; p++) pages.Add(p);
        if (to < count - 1) pages.Add(Gap);
        if (count > 1) pages.Add(count);
        pages.Add(Next);
        return pages;
    }

    public Rectangle GetButtonBounds(int page)
    {
        EnsureLayout();
        foreach (var (p, bounds) in _buttons) if (p == page) return bounds;
        return Rectangle.Empty;
    }

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    private int ButtonSize => Math.Max(Scale(32), Font.Height + Scale(12));

    public override Size GetPreferredSize(Size proposedSize)
    {
        EnsureLayout();
        int right = _buttons.Count > 0 ? _buttons[_buttons.Count - 1].Bounds.Right : 0;
        return new Size(right + Padding.Right, ButtonSize + Padding.Vertical);
    }

    private void Relayout()
    {
        _buttons.Clear();
        if (AutoSize) Parent?.PerformLayout(this, nameof(Page));
        Invalidate();
    }

    private void EnsureLayout()
    {
        if (_buttons.Count > 0) return;
        int x = Padding.Left, size = ButtonSize, gap = Scale(4);
        foreach (var page in VisiblePages())
        {
            int width = page > 0 ? Math.Max(size, TextRenderer.MeasureText(page.ToString(CultureInfo.CurrentCulture), Font).Width + Scale(12)) : size;
            _buttons.Add((page, new Rectangle(x, Padding.Top + (Height - Padding.Vertical - size) / 2, width, size)));
            x += width + gap;
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _buttons.Clear();
    }

    private int HitTest(Point point)
    {
        EnsureLayout();
        foreach (var (page, bounds) in _buttons)
            if (page != Gap && bounds.Contains(point)) return page;
        return int.MinValue;
    }

    private bool IsEnabled(int page) => page switch
    {
        Previous => _page > 1,
        Next => _page < PageCount,
        Gap => false,
        _ => true,
    };

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hover = HitTest(e.Location);
        if (hover != int.MinValue && !IsEnabled(hover)) hover = int.MinValue;
        Cursor = hover != int.MinValue ? Cursors.Hand : Cursors.Default;
        if (hover == _hover) return;
        _hover = hover;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = int.MinValue;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (CanFocus) Focus();
        _focusVisible = false;
        if (e.Button == MouseButtons.Left) Go(HitTest(e.Location));
    }

    /// <summary>As if a button was pressed: a page number, <see cref="Previous"/> or <see cref="Next"/>.</summary>
    public void Go(int button)
    {
        if (button == int.MinValue || !IsEnabled(button)) return;
        Page = button switch
        {
            Previous => _page - 1,
            Next => _page + 1,
            _ => button,
        };
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _focusVisible = true;
        switch (e.KeyCode)
        {
            case Keys.Left: Go(Previous); break;
            case Keys.Right: Go(Next); break;
            case Keys.Home: Page = 1; break;
            case Keys.End: Page = PageCount; break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _focusVisible = TwFocusVisible.LastInputWasKeyboard;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        EnsureLayout();
        float radius = Scale(6);
        const TextFormatFlags center = TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        foreach (var (page, bounds) in _buttons)
        {
            bool current = page == _page, enabled = IsEnabled(page), hover = page == _hover;
            if (current) TwPainter.FillRounded(g, bounds, radius, SelectedBackColor);
            else if (hover) TwPainter.FillRounded(g, bounds, radius, HoverBackColor);
            var color = current ? SelectedForeColor : enabled || page == Gap ? ForeColor : Color.FromArgb(90, ForeColor);
            if (page == Previous || page == Next)
                TwIconRenderer.DrawIcon(g, page == Previous ? "chevron-left" : "chevron-right", TwIconRenderer.EmPixels(Font.SizeInPoints, 96), bounds, color);
            else
                TextRenderer.DrawText(g, page == Gap ? "…" : page.ToString(CultureInfo.CurrentCulture), Font, bounds, color, center);
            if (current && Focused && _focusVisible) TwPainter.StrokeRounded(g, Rectangle.Inflate(bounds, 2, 2), radius + 2, 1, SelectedBackColor);
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new PagerAccessibleObject(this);

    private sealed class PagerAccessibleObject : ControlAccessibleObject
    {
        private readonly TwPager _pager;

        public PagerAccessibleObject(TwPager pager) : base(pager) => _pager = pager;

        public override AccessibleRole Role => _pager.AccessibleRole != AccessibleRole.Default ? _pager.AccessibleRole : AccessibleRole.ToolBar;

        public override string? Value
        {
            get => string.Format(System.Globalization.CultureInfo.CurrentCulture, TwTexts.Current.Page, _pager.Page, _pager.PageCount);
            set { }
        }
    }
}
