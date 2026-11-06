using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

public enum TwTabVariant
{
    /// <summary>An accent line under the active tab; a thin border under the strip.</summary>
    Underline,

    /// <summary>The active tab is a rounded, filled pill.</summary>
    Pills,

    /// <summary>A white, shadowed active segment on a gray track (iOS/macOS style).</summary>
    Segmented,
}

/// <summary>What a <see cref="TwTabStrip"/> does with tabs that do not fit its width.</summary>
public enum TwTabOverflow
{
    /// <summary>They move to a "more" button at the end that lists them in a menu; the selected tab always stays visible.</summary>
    Menu,

    /// <summary>
    /// The strip scrolls: ‹ › buttons at the edges, faded edges where more tabs follow, Shift+wheel or a tilt wheel; the
    /// selected tab is scrolled into view. Tabs keep their order and none is hidden in a menu.
    /// </summary>
    Scroll,

    /// <summary>Like <see cref="Scroll"/>, plus a "more" button at the end that lists every tab (the selected one checked).</summary>
    ScrollAndMenu,

    /// <summary>They are clipped at the edge.</summary>
    Clip,
}

/// <summary>Sekme renkleri; FormWind class'lardan doldurur.</summary>
internal sealed class TwTabColors
{
    public Color Fore { get; set; } = ColorTranslator.FromHtml("#6b7280");          // gray-500
    public Color ActiveFore { get; set; } = ColorTranslator.FromHtml("#111827");    // gray-900
    public Color ActiveBack { get; set; } = Color.White;
    public Color Accent { get; set; } = ColorTranslator.FromHtml("#2563eb");        // blue-600
    public Color HoverFore { get; set; } = ColorTranslator.FromHtml("#374151");     // gray-700
    public Color HoverBack { get; set; } = Color.Empty;
    public Color Track { get; set; } = ColorTranslator.FromHtml("#f3f4f6");         // gray-100
    public int? ActiveWeight { get; set; } = 600;
}

/// <summary>
/// Tab strip (headers only): switch the content in <see cref="SelectedIndexChanged"/>.
/// Inactive text <c>text-*</c>, active tab <c>selected:text-* selected:bg-* selected:border-*</c> (accent),
/// hover <c>item:hover:text-* item:hover:bg-*</c>, segmented track <c>fill-*</c>, spacing <c>gap-*</c>.
/// Navigate with ←/→, Home/End. Tabs that do not fit go to a "more" button at the end (↓ opens it), whose menu takes
/// <c>menu:</c> and <c>item:</c> classes; with <see cref="Overflow"/> = <see cref="TwTabOverflow.Scroll"/> the strip scrolls
/// instead. <c>&amp;</c> in a tab's text marks its Alt shortcut, as on a
/// Label; set <see cref="UseMnemonic"/> to <c>false</c> to draw it as text ("Parts &amp; cost").
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(SelectedIndexChanged))]
[DefaultProperty(nameof(Tabs))]
[Description("Tab strip: underline, pills or segmented.")]
public class TwTabStrip : Control, ITwChrome, ITwTimed
{
    private readonly List<string> _tabs = new List<string>();
    private readonly List<Rectangle> _bounds = new List<Rectangle>();
    private TwChrome _chrome = TwChrome.None;
    private TwTabColors _colors = new TwTabColors();
    private TwTabVariant _variant = TwTabVariant.Underline;
    private int _selectedIndex;
    private int _hoverIndex = -1;
    private TwTween? _slide;
    private Rectangle _slideFrom;
    // Göstergenin kayışı (180 ms); duration-*, delay-*, ease-* ve transition-none ile değişir.
    private TwTimingClasses _timing;
    private int _gap = -1;
    private bool _useMnemonic = true;
    private TwTabOverflow _overflow = TwTabOverflow.Menu;
    private Rectangle _overflowButton;
    private bool _hoverOverflow;
    private ContextMenuStrip? _overflowDropDown;
    // Kaydırma: sekmeler _bounds'ta içerik koordinatındadır (öteleme 0); çizim ve isabet VisualOffset kadar sola kaydırır.
    private Rectangle _scrollBack, _scrollForward, _viewport;
    private int _contentWidth;
    private int _scrollOffset;
    private float _scrollFrom;
    private TwTween? _scroll;
    private int _hoverArrow;
    private bool _revealSelected = true;

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _focusVisible = TwFocusVisible.LastInputWasKeyboard;
        Invalidate();
    }

    // Odak halkası yalnızca odak klavyeyle geldiyse çizilir (bkz. TwFocusVisible).
    private bool _focusVisible;

    public TwTabStrip()
    {
        TwFocusVisible.EnsureInstalled();
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        TabStop = true;
    }

    public event EventHandler? SelectedIndexChanged;

    [Category("Data"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    [Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Design", typeof(UITypeEditor))]
    public List<string> Tabs => _tabs;

    [Category("Appearance"), DefaultValue(TwTabVariant.Underline)]
    public TwTabVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            Relayout();
        }
    }

    [Category("Behavior"), DefaultValue(0)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var index = _tabs.Count == 0 ? 0 : Math.Max(0, Math.Min(_tabs.Count - 1, value));
            if (index == _selectedIndex) return;
            // Gösterge (alt çizgi / hap / bölme) eski sekmeden yenisine kayar.
            if (_selectedIndex >= 0 && _selectedIndex < _bounds.Count)
            {
                _slideFrom = _bounds[_selectedIndex];
                _slide ??= new TwTween(this);
                _slide.Start(_timing.For(180));
            }
            _selectedIndex = index;
            // Taşma menüsünden seçilen sekme görünür sekmelerin arasına alınır; kayan şerit onu görünür alana kaydırır.
            if (_overflow == TwTabOverflow.Menu && !_overflowButton.IsEmpty) _bounds.Clear();
            else if (_bounds.Count > 0 && !_viewport.IsEmpty) ScrollTo(RevealOffset(index), true);
            else _revealSelected = true;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.Selection, index);
            if (Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, index);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// <c>&amp;</c> in a tab's text marks the letter that selects it with Alt (<c>&amp;&amp;</c> draws one <c>&amp;</c>), as on a
    /// Label. <c>false</c>: <c>&amp;</c> is drawn as text.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool UseMnemonic
    {
        get => _useMnemonic;
        set
        {
            if (_useMnemonic == value) return;
            _useMnemonic = value;
            Relayout();
        }
    }

    /// <summary>
    /// What happens to tabs that do not fit: a "more" menu (default), a scrolling strip with ‹ › buttons, both, or clipping.
    /// </summary>
    [Category("Behavior"), DefaultValue(TwTabOverflow.Menu)]
    public TwTabOverflow Overflow
    {
        get => _overflow;
        set
        {
            if (_overflow == value) return;
            _overflow = value;
            _scrollOffset = 0;
            _scroll?.Stop();
            Relayout();
        }
    }

    /// <summary>
    /// Whether the strip has a "more" menu (<see cref="Overflow"/> is <see cref="TwTabOverflow.Menu"/> or
    /// <see cref="TwTabOverflow.ScrollAndMenu"/>). <c>false</c> turns the menu off: <see cref="TwTabOverflow.Menu"/> becomes
    /// <see cref="TwTabOverflow.Clip"/>, <see cref="TwTabOverflow.ScrollAndMenu"/> becomes <see cref="TwTabOverflow.Scroll"/>.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never)]
    public bool OverflowMenu
    {
        get => _overflow is TwTabOverflow.Menu or TwTabOverflow.ScrollAndMenu;
        set => Overflow = value
            ? _overflow switch { TwTabOverflow.Clip => TwTabOverflow.Menu, TwTabOverflow.Scroll => TwTabOverflow.ScrollAndMenu, _ => _overflow }
            : _overflow switch { TwTabOverflow.Menu => TwTabOverflow.Clip, TwTabOverflow.ScrollAndMenu => TwTabOverflow.Scroll, _ => _overflow };
    }

    /// <summary>Whether the strip is scrolling right now: <see cref="Overflow"/> scrolls and the tabs do not fit.</summary>
    [Browsable(false)]
    public bool IsScrollable
    {
        get
        {
            EnsureLayout();
            return !_viewport.IsEmpty;
        }
    }

    /// <summary>How far the tabs are scrolled (pixels, 0 … <see cref="MaxScrollOffset"/>); setting it jumps there.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ScrollOffset
    {
        get
        {
            EnsureLayout();
            return _scrollOffset;
        }
        set => ScrollTo(value, false);
    }

    /// <summary>The largest <see cref="ScrollOffset"/> (0 when the strip does not scroll).</summary>
    [Browsable(false)]
    public int MaxScrollOffset
    {
        get
        {
            EnsureLayout();
            return MaxOffset;
        }
    }

    private int MaxOffset => _viewport.IsEmpty ? 0 : Math.Max(0, _contentWidth - _viewport.Width);

    /// <summary>The ‹ button's area (empty when the strip does not scroll).</summary>
    [Browsable(false)]
    public Rectangle ScrollBackButtonBounds
    {
        get
        {
            EnsureLayout();
            return _scrollBack;
        }
    }

    /// <summary>The › button's area (empty when the strip does not scroll).</summary>
    [Browsable(false)]
    public Rectangle ScrollForwardButtonBounds
    {
        get
        {
            EnsureLayout();
            return _scrollForward;
        }
    }

    /// <summary>Scrolls the strip (animated) so that the tab is fully visible; does nothing when the strip does not scroll.</summary>
    public void ScrollTabIntoView(int index)
    {
        if (index < 0 || index >= _tabs.Count || !IsScrollable) return;
        ScrollTo(RevealOffset(index), true);
    }

    /// <summary>Indices of the tabs moved to the overflow menu (empty when all fit, and always empty while scrolling).</summary>
    [Browsable(false)]
    public IReadOnlyList<int> OverflowTabs
    {
        get
        {
            EnsureLayout();
            var hidden = new List<int>();
            for (int i = 0; i < _bounds.Count; i++)
                if (_bounds[i].IsEmpty) hidden.Add(i);
            return hidden;
        }
    }

    /// <summary>The "more" button's area (empty when all tabs fit).</summary>
    [Browsable(false)]
    public Rectangle OverflowButtonBounds
    {
        get
        {
            EnsureLayout();
            return _overflowButton;
        }
    }

    /// <summary>The last opened overflow menu (<c>null</c> if it was never opened).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ContextMenuStrip? OverflowDropDown => _overflowDropDown;

    /// <summary>Overflow menu colors; FormWind fills them from the <c>menu:</c> / <c>item:</c> classes.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal TwMenuColors MenuColors { get; set; } = new TwMenuColors();

    /// <summary>
    /// Opens the "more" menu under its button: the tabs that do not fit, or with <see cref="TwTabOverflow.ScrollAndMenu"/>
    /// every tab, the selected one checked.
    /// </summary>
    public void ShowOverflow()
    {
        EnsureLayout();
        if (_overflowButton.IsEmpty) return;
        bool all = _overflow == TwTabOverflow.ScrollAndMenu;
        var entries = all ? Enumerable.Range(0, _tabs.Count).ToList() : OverflowTabs.ToList();
        if (entries.Count == 0) return;
        _overflowDropDown?.Dispose();
        _overflowDropDown = new ContextMenuStrip
        {
            Renderer = new TwToolStripRenderer(MenuColors), ShowImageMargin = false, ShowCheckMargin = all, Font = Font,
        };
        foreach (var index in entries)
        {
            int tab = index;
            // Menü öğesi de & işaretini kısayol sayar; kısayol kapalıysa && ile kaçırılır.
            var item = new ToolStripMenuItem(_useMnemonic ? _tabs[index] : _tabs[index].Replace("&", "&&"))
            {
                Checked = all && index == _selectedIndex,
            };
            item.Click += (_, _) => SelectedIndex = tab;
            _overflowDropDown.Items.Add(item);
        }
        bool rtl = TwRtl.NeedsMirroring(this);
        var button = _overflowButton;
        _overflowDropDown.Show(this, new Point(rtl ? button.Left : button.Right, button.Bottom + Scale(2)),
            rtl ? ToolStripDropDownDirection.BelowRight : ToolStripDropDownDirection.BelowLeft);
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal TwTabColors Colors
    {
        get => _colors;
        set
        {
            _colors = value ?? new TwTabColors();
            Relayout();
        }
    }

    /// <summary>Space between tabs (pixels); -1 = the default for the style.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Gap
    {
        get => _gap;
        set
        {
            _gap = value;
            Relayout();
        }
    }

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DefaultValue(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set => base.AutoSize = value;
    }

    protected override Size DefaultSize => new Size(300, 40);

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    private int ItemGap => _gap >= 0 ? _gap : Scale(_variant == TwTabVariant.Underline ? 16 : 4);

    private Padding ItemPadding => _variant == TwTabVariant.Underline
        ? new Padding(Scale(2), Scale(8), Scale(2), Scale(10))
        : new Padding(Scale(12), Scale(6), Scale(12), Scale(6));

    void ITwChrome.SetChrome(TwChrome chrome)
    {
        _chrome = chrome;
        Invalidate();
    }

    void ITwTimed.SetTiming(TwTimingClasses timing) => _timing = timing;

    protected override AccessibleObject CreateAccessibilityInstance() => new TwTabStripAccessibleObject(this);

    /// <summary>
    /// A tab's area within the control (for tests and custom drawing). While the strip scrolls it moves with
    /// <see cref="ScrollOffset"/> and may lie partly or wholly outside the visible area (see <see cref="GetVisibleTabBounds"/>).
    /// </summary>
    public Rectangle GetTabBounds(int index)
    {
        EnsureLayout();
        return index >= 0 && index < _bounds.Count ? Shift(_bounds[index]) : Rectangle.Empty;
    }

    /// <summary>The visible part of a tab (empty when it is scrolled out of view or moved to the menu).</summary>
    public Rectangle GetVisibleTabBounds(int index)
    {
        var bounds = GetTabBounds(index);
        if (bounds.IsEmpty || _viewport.IsEmpty) return bounds;
        var visible = Rectangle.Intersect(bounds, new Rectangle(_viewport.Left, bounds.Top, _viewport.Width, bounds.Height));
        return visible.Width > 0 ? visible : Rectangle.Empty;
    }

    /// <summary>
    /// The narrowest useful width of a scrolling strip: the ‹ › buttons, the widest tab and the "more" button (never more
    /// than the preferred width). Equals the preferred width when <see cref="Overflow"/> does not scroll. A
    /// <see cref="TwToolbar"/> narrows a scrolling strip down to this before it moves the strip to its menu.
    /// </summary>
    [Browsable(false)]
    public int MinimumScrollWidth
    {
        get
        {
            var preferred = GetPreferredSize(Size.Empty).Width;
            if (_overflow is not (TwTabOverflow.Scroll or TwTabOverflow.ScrollAndMenu) || _tabs.Count < 2) return preferred;
            var padding = ItemPadding;
            int widest = 0, height = 0;
            for (int i = 0; i < _tabs.Count; i++)
            {
                var text = MeasureTab(i);
                widest = Math.Max(widest, text.Width + padding.Horizontal);
                height = Math.Max(height, text.Height + padding.Vertical);
            }
            int inset = _variant == TwTabVariant.Segmented ? Scale(4) : 0;
            int chrome = Padding.Horizontal + inset * 2 + 2 * (ArrowWidth + Scale(2))
                         + (_overflow == TwTabOverflow.ScrollAndMenu ? Math.Max(height, Scale(32)) + Scale(2) : 0);
            return Math.Min(preferred, chrome + widest);
        }
    }

    private int ArrowWidth => Scale(24);

    private int ScrollStep => Math.Max(Scale(40), _viewport.Width * 3 / 4);

    // İçerik koordinatındaki dikdörtgeni o anki kaydırmaya göre ekrana taşır.
    private Rectangle Shift(Rectangle rect) =>
        rect.IsEmpty || _viewport.IsEmpty ? rect : new Rectangle(rect.X - VisualOffset, rect.Y, rect.Width, rect.Height);

    private int VisualOffset => _scroll != null && _scroll.Progress < 1f
        ? (int)Math.Round(_scrollFrom + (_scrollOffset - _scrollFrom) * _scroll.Progress)
        : _scrollOffset;

    private void ScrollTo(int target, bool animate)
    {
        EnsureLayout();
        target = Math.Max(0, Math.Min(MaxOffset, target));
        if (target == _scrollOffset) return;
        if (animate)
        {
            _scrollFrom = VisualOffset;
            _scroll ??= new TwTween(this);
            _scroll.Start(_timing.For(200));
        }
        else
        {
            _scroll?.Stop();
        }
        _scrollOffset = target;
        Invalidate();
    }

    // Sekmeyi tümüyle gösteren en yakın kaydırma; komşu sekmeden bir parça da görünür kalır ki devamı olduğu anlaşılsın.
    private int RevealOffset(int index)
    {
        if (_viewport.IsEmpty || index < 0 || index >= _bounds.Count) return _scrollOffset;
        var tab = _bounds[index];
        int left = tab.Left - _viewport.Left, right = tab.Right - _viewport.Left;
        int peek = Math.Min(Scale(24), Math.Max(0, (_viewport.Width - tab.Width) / 2));
        int target = _scrollOffset;
        if (right + peek > target + _viewport.Width) target = right + peek - _viewport.Width;
        if (left - peek < target) target = left - peek;
        return Math.Max(0, Math.Min(MaxOffset, target));
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var padding = ItemPadding;
        int width = 0, height = 0;
        for (int i = 0; i < _tabs.Count; i++)
        {
            var text = MeasureTab(i);
            width += text.Width + padding.Horizontal + (i > 0 ? ItemGap : 0);
            height = Math.Max(height, text.Height + padding.Vertical);
        }
        int inset = _variant == TwTabVariant.Segmented ? Scale(4) : 0;
        return new Size(width + Padding.Horizontal + inset * 2, height + Padding.Vertical + inset * 2);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        _bounds.Clear();
        // Boyut değişince seçili sekme görünür alanda kalır.
        _revealSelected = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHover(HitTest(e.Location));
        bool overflow = OverflowButtonBounds.Contains(e.Location);
        int arrow = _scrollBack.Contains(e.Location) ? -1 : _scrollForward.Contains(e.Location) ? 1 : 0;
        if (overflow == _hoverOverflow && arrow == _hoverArrow) return;
        _hoverOverflow = overflow;
        _hoverArrow = arrow;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(-1);
        if (!_hoverOverflow && _hoverArrow == 0) return;
        _hoverOverflow = false;
        _hoverArrow = 0;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left && OverflowButtonBounds.Contains(e.Location))
        {
            ShowOverflow();
            return;
        }
        // Oklar seçimi değiştirmez, yalnızca kaydırır.
        if (e.Button == MouseButtons.Left && _scrollBack.Contains(e.Location))
        {
            ScrollTo(_scrollOffset - ScrollStep, true);
            return;
        }
        if (e.Button == MouseButtons.Left && _scrollForward.Contains(e.Location))
        {
            ScrollTo(_scrollOffset + ScrollStep, true);
            return;
        }
        var index = HitTest(e.Location);
        if (index >= 0) SelectedIndex = index;
    }

    // Dikey tekerlek sayfayı kaydırmaya devam eder (şerit onu yutmaz); şerit Shift+tekerlek ve yatay tekerlekle kayar.
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if ((ModifierKeys & Keys.Shift) == 0 || !IsScrollable) return;
        ScrollTo(_scrollOffset - e.Delta * Scale(48) / 120, true);
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_MOUSEHWHEEL = 0x020E;
        if (m.Msg == WM_MOUSEHWHEEL && IsScrollable)
        {
            int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
            ScrollTo(_scrollOffset + delta * Scale(48) / 120, true);
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scroll?.Dispose();
            _slide?.Dispose();
            _overflowDropDown?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override bool ProcessMnemonic(char charCode)
    {
        if (_useMnemonic && CanSelect)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (!IsMnemonic(charCode, _tabs[i])) continue;
                Focus();
                SelectedIndex = i;
                return true;
            }
        }
        return base.ProcessMnemonic(charCode);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _focusVisible = true;
        switch (e.KeyCode)
        {
            case Keys.Left: SelectedIndex = _selectedIndex - 1; break;
            case Keys.Right: SelectedIndex = _selectedIndex + 1; break;
            case Keys.Home: SelectedIndex = 0; break;
            case Keys.End: SelectedIndex = _tabs.Count - 1; break;
            case Keys.Down when !OverflowButtonBounds.IsEmpty: ShowOverflow(); break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        TwPainter.FillSurface(g, ClientRectangle, _chrome.Radius, BackColor, _chrome.Gradient);
        EnsureLayout();
        bool scrolling = !_viewport.IsEmpty;
        int radius = _variant == TwTabVariant.Pills ? 9999 : Scale(6);

        if (_variant == TwTabVariant.Segmented && _bounds.Count > 0)
        {
            // Ray görünen sekmeleri, okları ve taşma düğmesini kapsar.
            var shown = Rectangle.Empty;
            if (scrolling) shown = Rectangle.Union(_scrollBack, _scrollForward);
            else
                foreach (var tab in _bounds)
                    if (!tab.IsEmpty) shown = shown.IsEmpty ? tab : Rectangle.Union(shown, tab);
            if (!_overflowButton.IsEmpty) shown = Rectangle.Union(shown, _overflowButton);
            TwPainter.FillRounded(g, Rectangle.Inflate(shown, Scale(4), Scale(4)), Scale(8), _colors.Track);
        }

        TwPainter.StrokeChrome(g, ClientRectangle, _chrome);

        // Kayan şeritte sekmeler, gösterge ve odak çerçevesi okların arasındaki görünür alana kırpılır.
        var saved = g.Save();
        if (scrolling) g.SetClip(new Rectangle(_viewport.Left, 0, _viewport.Width, Height), CombineMode.Intersect);

        // Hover zeminleri göstergenin altında kalır.
        for (int i = 0; i < _tabs.Count && i < _bounds.Count; i++)
            if (i == _hoverIndex && i != _selectedIndex && !_colors.HoverBack.IsEmpty && _variant != TwTabVariant.Underline)
                TwPainter.FillRounded(g, Shift(_bounds[i]), radius, _colors.HoverBack);

        if (_selectedIndex >= 0 && _selectedIndex < _bounds.Count)
        {
            var target = _bounds[_selectedIndex];
            var indicator = Shift(_slide != null && _slide.Progress < 1f && !_slideFrom.IsEmpty
                ? Rectangle.Round(TwMotion.Lerp(_slideFrom, target, _slide.Progress))
                : target);
            switch (_variant)
            {
                case TwTabVariant.Pills:
                    TwPainter.FillRounded(g, indicator, 9999, _colors.ActiveBack);
                    break;
                case TwTabVariant.Segmented:
                    TwShadowRenderer.Draw(g, indicator, Scale(6), new TwShadowLayer(Scale(1), Scale(2), 0, Color.FromArgb(25, 0, 0, 0)));
                    TwPainter.FillRounded(g, indicator, Scale(6), _colors.ActiveBack);
                    break;
                case TwTabVariant.Underline:
                    using (var brush = new SolidBrush(_colors.Accent))
                        g.FillRectangle(brush, indicator.X, target.Bottom - Scale(2), indicator.Width, Scale(2));
                    break;
            }
        }

        var flags = TextFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                    | (scrolling ? TextFormatFlags.PreserveGraphicsClipping : 0);
        for (int i = 0; i < _tabs.Count && i < _bounds.Count; i++)
        {
            var bounds = Shift(_bounds[i]);
            if (bounds.IsEmpty || scrolling && (bounds.Right <= _viewport.Left || bounds.Left >= _viewport.Right)) continue;
            bool active = i == _selectedIndex, hover = i == _hoverIndex && !active;

            var color = active ? _colors.ActiveFore : hover ? _colors.HoverFore : _colors.Fore;
            if (!Enabled) color = Color.FromArgb(128, color);
            var text = new Rectangle(bounds.X, bounds.Y, bounds.Width,
                _variant == TwTabVariant.Underline ? bounds.Height - Scale(2) : bounds.Height);
            TextRenderer.DrawText(g, _tabs[i], FontFor(i), text, color, flags);
        }

        if (Focused && _focusVisible && _selectedIndex < _bounds.Count)
            ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(Shift(_bounds[_selectedIndex]), -2, -2));

        if (scrolling) PaintFades(g);
        g.Restore(saved);

        if (scrolling)
        {
            PaintButton(g, _scrollBack, "chevron-left", _hoverArrow == -1, _scrollOffset > 0, radius);
            PaintButton(g, _scrollForward, "chevron-right", _hoverArrow == 1, _scrollOffset < MaxOffset, radius);
        }
        if (!_overflowButton.IsEmpty) PaintButton(g, _overflowButton, "more", _hoverOverflow, true, radius);
    }

    // Ok ve "daha fazla" düğmeleri; uçtaki ok soluk çizilir ama yerinde kalır.
    private void PaintButton(Graphics g, Rectangle bounds, string icon, bool hover, bool enabled, int radius)
    {
        enabled &= Enabled;
        if (hover && enabled && !_colors.HoverBack.IsEmpty) TwPainter.FillRounded(g, bounds, radius, _colors.HoverBack);
        var color = hover && enabled ? _colors.HoverFore : _colors.Fore;
        TwIconRenderer.DrawIcon(g, icon, TwIconRenderer.EmPixels(Font.SizeInPoints * 1.1f, TwDpi.DeviceDpi(this)), bounds,
            enabled ? color : Color.FromArgb(Enabled ? 90 : 128, color));
    }

    // Kaydırılabilen yönde görünür alanın kenarı zemine doğru solar: devamı olduğunu oklardan önce o söyler.
    private void PaintFades(Graphics g)
    {
        if (_chrome.Gradient != null) return;
        int width = Math.Min(Scale(20), _viewport.Width / 4);
        if (width <= 0) return;
        var back = SurfaceColor();
        int offset = VisualOffset;
        // Solma yalnızca sekme satırını kaplar; şeridin alt kenarlığı (border-b) altında kalmaz.
        if (offset > 0) Fade(new Rectangle(_viewport.Left, _viewport.Top, width, _viewport.Height), back, Color.FromArgb(0, back));
        if (offset < MaxOffset) Fade(new Rectangle(_viewport.Right - width, _viewport.Top, width, _viewport.Height), Color.FromArgb(0, back), back);

        void Fade(Rectangle area, Color from, Color to)
        {
            // Fırça bir piksel geniş tutulur; yoksa GDI+ degradeyi kenarda başa sarar.
            using var brush = new LinearGradientBrush(Rectangle.Inflate(area, 1, 0), from, to, LinearGradientMode.Horizontal);
            g.FillRectangle(brush, area);
        }
    }

    // Solmanın boyanacağı zemin: bölmeli şeritte ray, diğerlerinde ilk opak zemin (kendisi ya da bir üst kontrol).
    private Color SurfaceColor()
    {
        if (_variant == TwTabVariant.Segmented) return _colors.Track;
        for (Control? control = this; control != null; control = control.Parent)
            if (control.BackColor.A == 255) return control.BackColor;
        return SystemColors.Control;
    }

    private Font FontFor(int index)
    {
        if (index != _selectedIndex || _colors.ActiveWeight is not int weight) return Font;
        return TwFonts.Create(new TwStyle { FontWeight = weight }, Font, TwTheme.Current);
    }

    /// <summary>A tab's text as shown, without the shortcut marker (for screen readers and menus).</summary>
    internal string GetDisplayText(int index)
    {
        var text = index >= 0 && index < _tabs.Count ? _tabs[index] : "";
        if (!_useMnemonic || text.IndexOf('&') < 0) return text;
        var result = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '&' && i + 1 < text.Length) i++;
            else if (text[i] == '&') continue;
            result.Append(text[i]);
        }
        return result.ToString();
    }

    // Kısayol işareti kapalıyken & düz metindir; açıkken Windows kısayol ipuçlarını gizliyorsa alt çizgi çizilmez.
    private TextFormatFlags TextFlags => TextFormatFlags.NoPadding
        | (!_useMnemonic ? TextFormatFlags.NoPrefix : ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix);

    // Kalın etkin yazı genişliği değiştirmesin diye ölçü her zaman kalın fontla alınır.
    private Size MeasureTab(int index)
    {
        var font = _colors.ActiveWeight is int weight ? TwFonts.Create(new TwStyle { FontWeight = weight }, Font, TwTheme.Current) : Font;
        return TextRenderer.MeasureText(_tabs[index], font, Size.Empty, TextFlags);
    }

    private void EnsureLayout()
    {
        if (_bounds.Count == _tabs.Count && _tabs.Count > 0) return;
        _bounds.Clear();
        _overflowButton = _scrollBack = _scrollForward = _viewport = Rectangle.Empty;
        _contentWidth = 0;
        var padding = ItemPadding;
        int inset = _variant == TwTabVariant.Segmented ? Scale(4) : 0;
        int x = Padding.Left + inset;
        int height = 0;
        for (int i = 0; i < _tabs.Count; i++) height = Math.Max(height, MeasureTab(i).Height + padding.Vertical);
        int y = _variant == TwTabVariant.Underline
            ? Math.Max(0, Height - height - Padding.Bottom)
            : Padding.Top + Math.Max(0, (Height - Padding.Vertical - height) / 2);

        var widths = new int[_tabs.Count];
        int total = 0;
        for (int i = 0; i < _tabs.Count; i++)
        {
            widths[i] = MeasureTab(i).Width + padding.Horizontal;
            total += widths[i] + (i > 0 ? ItemGap : 0);
        }

        int button = Math.Max(height, Scale(32));
        bool fits = x + total <= Width - Padding.Right - inset;
        if (_overflow is TwTabOverflow.Scroll or TwTabOverflow.ScrollAndMenu && _tabs.Count > 1 && Width > 0 && !fits)
        {
            LayoutScrolling(x, y, height, inset, button, widths, total);
            return;
        }
        _scrollOffset = 0;
        _scroll?.Stop();

        // Sığmayan sekmeler "daha fazla" düğmesine taşınır: önce seçili sekmeye yer ayrılır, kalan yer soldan doldurulur.
        var shown = new bool[_tabs.Count];
        bool overflow = _overflow == TwTabOverflow.Menu && _tabs.Count > 1 && Width > 0 && !fits;
        if (overflow)
        {
            int room = Width - Padding.Right - inset - x - button;
            int selected = Math.Max(0, Math.Min(_selectedIndex, _tabs.Count - 1));
            shown[selected] = true;
            room -= widths[selected] + ItemGap;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (i == selected || widths[i] + ItemGap > room) continue;
                shown[i] = true;
                room -= widths[i] + ItemGap;
            }
        }

        for (int i = 0; i < _tabs.Count; i++)
        {
            if (overflow && !shown[i])
            {
                _bounds.Add(Rectangle.Empty);
                continue;
            }
            _bounds.Add(new Rectangle(x, y, widths[i], height));
            x += widths[i] + ItemGap;
        }
        if (overflow) _overflowButton = new Rectangle(x, y, button, height);
    }

    // Kayan şerit: solda ‹, sağda › (ScrollAndMenu'da en sonda "daha fazla"); sekmeler aradaki görünür alanda, sırası
    // bozulmadan dizilir. Oklar uçta soluklaşır ama yerinde kalır ki kaydırırken sekmeler zıplamasın.
    private void LayoutScrolling(int x, int y, int height, int inset, int button, int[] widths, int total)
    {
        int arrow = ArrowWidth, space = Scale(2);
        int right = Width - Padding.Right - inset;
        if (_overflow == TwTabOverflow.ScrollAndMenu)
        {
            _overflowButton = new Rectangle(right - button, y, button, height);
            right = _overflowButton.Left - space;
        }
        _scrollBack = new Rectangle(x, y, arrow, height);
        _scrollForward = new Rectangle(Math.Max(_scrollBack.Right + space, right - arrow), y, arrow, height);
        int left = _scrollBack.Right + space;
        _viewport = new Rectangle(left, y, Math.Max(1, _scrollForward.Left - space - left), height);
        int cx = _viewport.Left;
        for (int i = 0; i < _tabs.Count; i++)
        {
            _bounds.Add(new Rectangle(cx, y, widths[i], height));
            cx += widths[i] + ItemGap;
        }
        _contentWidth = total;
        if (_scrollOffset > MaxOffset) _scrollOffset = MaxOffset;
        if (!_revealSelected) return;
        _revealSelected = false;
        _scroll?.Stop();
        _scrollOffset = RevealOffset(Math.Max(0, Math.Min(_selectedIndex, _tabs.Count - 1)));
    }

    private int HitTest(Point point)
    {
        EnsureLayout();
        if (!_viewport.IsEmpty && (point.X < _viewport.Left || point.X >= _viewport.Right)) return -1;
        for (int i = 0; i < _bounds.Count; i++)
            if (Shift(_bounds[i]).Contains(point)) return i;
        return -1;
    }

    private void SetHover(int index)
    {
        if (index == _hoverIndex) return;
        _hoverIndex = index;
        Invalidate();
    }

    private void Relayout()
    {
        _bounds.Clear();
        _overflowButton = Rectangle.Empty;
        _revealSelected = true;
        if (AutoSize) Parent?.PerformLayout(this, nameof(Tabs));
        Invalidate();
    }
}
