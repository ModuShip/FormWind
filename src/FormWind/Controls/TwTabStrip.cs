using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
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
/// Navigate with ←/→, Home/End.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(SelectedIndexChanged))]
[DefaultProperty(nameof(Tabs))]
[Description("Tab strip: underline, pills or segmented.")]
public class TwTabStrip : Control, ITwChrome
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
    private int _gap = -1;

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
                _slide.Start(180);
            }
            _selectedIndex = index;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.Selection, index);
            if (Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, index);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
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

    /// <summary>A tab's area within the control (for tests and custom drawing).</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new TwTabStripAccessibleObject(this);

    public Rectangle GetTabBounds(int index)
    {
        EnsureLayout();
        return index >= 0 && index < _bounds.Count ? _bounds[index] : Rectangle.Empty;
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
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHover(HitTest(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(-1);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var index = HitTest(e.Location);
        if (index >= 0) SelectedIndex = index;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

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
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        TwPainter.FillSurface(g, ClientRectangle, _chrome.Radius, BackColor, _chrome.Gradient);
        EnsureLayout();

        if (_variant == TwTabVariant.Segmented && _bounds.Count > 0)
        {
            var track = Rectangle.FromLTRB(_bounds[0].Left - Scale(4), _bounds[0].Top - Scale(4),
                _bounds[_bounds.Count - 1].Right + Scale(4), _bounds[0].Bottom + Scale(4));
            TwPainter.FillRounded(g, track, Scale(8), _colors.Track);
        }

        TwPainter.StrokeChrome(g, ClientRectangle, _chrome);

        // Hover zeminleri göstergenin altında kalır.
        for (int i = 0; i < _tabs.Count && i < _bounds.Count; i++)
            if (i == _hoverIndex && i != _selectedIndex && !_colors.HoverBack.IsEmpty && _variant != TwTabVariant.Underline)
                TwPainter.FillRounded(g, _bounds[i], _variant == TwTabVariant.Pills ? 9999 : Scale(6), _colors.HoverBack);

        if (_selectedIndex >= 0 && _selectedIndex < _bounds.Count)
        {
            var target = _bounds[_selectedIndex];
            var indicator = _slide != null && _slide.Progress < 1f
                ? Rectangle.Round(TwMotion.Lerp(_slideFrom, target, _slide.Progress))
                : target;
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

        for (int i = 0; i < _tabs.Count && i < _bounds.Count; i++)
        {
            var bounds = _bounds[i];
            bool active = i == _selectedIndex, hover = i == _hoverIndex && !active;

            var color = active ? _colors.ActiveFore : hover ? _colors.HoverFore : _colors.Fore;
            if (!Enabled) color = Color.FromArgb(128, color);
            var text = new Rectangle(bounds.X, bounds.Y, bounds.Width,
                _variant == TwTabVariant.Underline ? bounds.Height - Scale(2) : bounds.Height);
            TextRenderer.DrawText(g, _tabs[i], FontFor(i), text, color,
                TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        if (Focused && _focusVisible && _selectedIndex < _bounds.Count)
            ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(_bounds[_selectedIndex], -2, -2));
    }

    private Font FontFor(int index)
    {
        if (index != _selectedIndex || _colors.ActiveWeight is not int weight) return Font;
        return TwFonts.Create(new TwStyle { FontWeight = weight }, Font, TwTheme.Current);
    }

    // Kalın etkin yazı genişliği değiştirmesin diye ölçü her zaman kalın fontla alınır.
    private Size MeasureTab(int index)
    {
        var font = _colors.ActiveWeight is int weight ? TwFonts.Create(new TwStyle { FontWeight = weight }, Font, TwTheme.Current) : Font;
        return TextRenderer.MeasureText(_tabs[index], font, Size.Empty, TextFormatFlags.NoPadding);
    }

    private void EnsureLayout()
    {
        if (_bounds.Count == _tabs.Count && _tabs.Count > 0) return;
        _bounds.Clear();
        var padding = ItemPadding;
        int inset = _variant == TwTabVariant.Segmented ? Scale(4) : 0;
        int x = Padding.Left + inset;
        int height = 0;
        for (int i = 0; i < _tabs.Count; i++) height = Math.Max(height, MeasureTab(i).Height + padding.Vertical);
        int y = _variant == TwTabVariant.Underline
            ? Math.Max(0, Height - height - Padding.Bottom)
            : Padding.Top + Math.Max(0, (Height - Padding.Vertical - height) / 2);

        for (int i = 0; i < _tabs.Count; i++)
        {
            int width = MeasureTab(i).Width + padding.Horizontal;
            _bounds.Add(new Rectangle(x, y, width, height));
            x += width + ItemGap;
        }
    }

    private int HitTest(Point point)
    {
        EnsureLayout();
        for (int i = 0; i < _bounds.Count; i++)
            if (_bounds[i].Contains(point)) return i;
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
        if (AutoSize) Parent?.PerformLayout(this, nameof(Tabs));
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _slide?.Dispose();
        base.Dispose(disposing);
    }
}
