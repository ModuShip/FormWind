using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>A Tw control with an on/off state that <c>checked:</c> classes follow.</summary>
internal interface ITwCheckable
{
    bool Checked { get; }

    event EventHandler? CheckedChanged;
}

/// <summary>
/// A chip: a small pill with a text, an optional icon and an optional ✕. It shows a status or a value ("Line 2", "High risk"),
/// is a filter you click (<see cref="Clickable"/>) or turn on and off (<see cref="Checkable"/>, styled with <c>checked:</c>),
/// and with <see cref="Removable"/> a ✕ raises <see cref="RemoveClick"/> (also Delete or Backspace while focused).
/// Styled with base classes: <c>bg-* text-* border rounded-* px-* py-* text-xs font-*</c>, <c>hover:</c> when clickable;
/// the theme's <c>chip</c> component and its tones (<c>chip-primary</c>, <c>chip-success</c>, <c>chip-warning</c>,
/// <c>chip-danger</c>) give the default look.
/// <code>
/// var chip = new TwChip { Text = "Line 2", Removable = true };
/// chip.RemoveClick += (s, e) =&gt; filterBar.Controls.Remove(chip);
/// tw.SetClasses(chip, "chip-primary");
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Text))]
[Description("Chip: a pill with a text, an icon and an optional ✕.")]
public class TwChip : Control, ITwChrome, ITwCheckable
{
    private TwChrome _chrome = TwChrome.None;
    private string? _iconName;
    private Color _dotColor = Color.Empty;
    private bool _removable;
    private bool _clickable;
    private bool _checkable;
    private bool _checked;
    private bool _hoverRemove;
    private bool _pressedRemove;
    private bool _focusVisible;

    public TwChip()
    {
        TwFocusVisible.EnsureInstalled();
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = false;
        AutoSize = true;
        BackColor = Color.Transparent;
    }

    /// <summary>The ✕ was clicked (or Delete/Backspace pressed); remove the chip or the filter it stands for here.</summary>
    public event EventHandler? RemoveClick;

    /// <summary>When <see cref="Checked"/> changes.</summary>
    public event EventHandler? CheckedChanged;

    protected override Size DefaultSize => new Size(72, 22);

    protected override Padding DefaultPadding => new Padding(8, 2, 8, 2);

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DefaultValue(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set => base.AutoSize = value;
    }

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => base.Text;
        set => base.Text = value;
    }

    /// <summary>Icon before the text (<see cref="TwIconRenderer.DrawIcon"/>); <c>null</c> = none.</summary>
    [Category("Appearance"), DefaultValue(null)]
    public string? IconName
    {
        get => _iconName;
        set
        {
            _iconName = string.IsNullOrWhiteSpace(value) ? null : value;
            Relayout();
        }
    }

    /// <summary>A status dot before the text (empty = none); drawn when there is no icon.</summary>
    [Category("Appearance")]
    public Color DotColor
    {
        get => _dotColor;
        set
        {
            _dotColor = value;
            Relayout();
        }
    }

    private bool ShouldSerializeDotColor() => !_dotColor.IsEmpty;

    private void ResetDotColor() => DotColor = Color.Empty;

    /// <summary>A ✕ after the text raises <see cref="RemoveClick"/>.</summary>
    [Category("Behavior"), DefaultValue(false)]
    public bool Removable
    {
        get => _removable;
        set
        {
            _removable = value;
            UpdateTabStop();
            Relayout();
        }
    }

    /// <summary>The chip is a button: hand cursor, <c>hover:</c>, a tab stop, Enter and Space click it.</summary>
    [Category("Behavior"), DefaultValue(false)]
    public bool Clickable
    {
        get => _clickable;
        set
        {
            _clickable = value;
            UpdateTabStop();
            Invalidate();
        }
    }

    /// <summary>A click turns <see cref="Checked"/> on and off (a filter chip); a check mark is drawn when there is no icon.</summary>
    [Category("Behavior"), DefaultValue(false)]
    public bool Checkable
    {
        get => _checkable;
        set
        {
            _checkable = value;
            UpdateTabStop();
            Relayout();
        }
    }

    [Category("Behavior"), DefaultValue(false), Bindable(true)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Relayout();
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool Interactive => _clickable || _checkable;

    // Etkileşimli çip sekme durağıdır; salt gösterim çipleri (KPI, durum) odak almaz.
    private void UpdateTabStop() => TabStop = Interactive || _removable;

    void ITwChrome.SetChrome(TwChrome chrome)
    {
        if (_chrome.Equals(chrome)) return;
        _chrome = chrome;
        Invalidate();
    }

    private float DpiScale => TwDpi.DeviceDpi(this) / 96f;

    private int ScalePixels(float pixels) => (int)Math.Round(pixels * DpiScale);

    private const TextFormatFlags TextFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private int IconEm => TwIconRenderer.EmPixels(Font.SizeInPoints * 1.1f, TwDpi.DeviceDpi(this));

    // Başta ne çizilir: simge, işaretliyken onay işareti ya da durum noktası.
    private string? LeadingIcon => _iconName ?? (_checkable && _checked ? "check" : null);

    private int LeadingWidth => LeadingIcon != null ? IconEm : !_dotColor.IsEmpty ? ScalePixels(6) : 0;

    private int RemoveSize => Math.Max(ScalePixels(12), (int)(Font.Height * 0.9f));

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text.Length > 0 ? Text : " ", Font, Size.Empty, TextFlags);
        int gap = ScalePixels(4);
        int width = Padding.Horizontal + (Text.Length > 0 ? text.Width : 0);
        int leading = LeadingWidth;
        if (leading > 0) width += leading + (Text.Length > 0 ? gap + (LeadingIcon is null ? ScalePixels(1) : 0) : 0);
        if (_removable) width += RemoveSize + (Text.Length > 0 || leading > 0 ? gap : 0);
        int height = Math.Max(text.Height, Math.Max(leading, _removable ? RemoveSize : 0)) + Padding.Vertical;
        return new Size(Math.Max(width, height), height);
    }

    /// <summary>The ✕'s area in client coordinates (empty when not <see cref="Removable"/>).</summary>
    [Browsable(false)]
    public Rectangle RemoveButtonBounds
    {
        get
        {
            if (!_removable) return Rectangle.Empty;
            int size = RemoveSize;
            bool rtl = TwRtl.NeedsMirroring(this);
            // ✕ sağ kenara yakın durur; dolgu yarıya iner ki çipin ucu dar kalsın.
            int x = rtl ? Math.Max(0, Padding.Right / 2) : Width - Math.Max(0, Padding.Right / 2) - size;
            return new Rectangle(x, (Height - size) / 2, size, size);
        }
    }

    private void Relayout()
    {
        if (AutoSize) Parent?.PerformLayout(this, nameof(AutoSize));
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Relayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        Relayout();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool over = RemoveButtonBounds.Contains(e.Location);
        if (over == _hoverRemove) return;
        _hoverRemove = over;
        Invalidate();
    }

    // El imleci: tıklanabilir çipte ve ✕'in üstünde; cursor-* class'ı (Cursor özelliği) önce gelir.
    protected override Cursor DefaultCursor => _hoverRemove || Interactive ? Cursors.Hand : Cursors.Default;

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_hoverRemove) return;
        _hoverRemove = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressedRemove = e.Button == MouseButtons.Left && RemoveButtonBounds.Contains(e.Location);
        if ((Interactive || _pressedRemove) && CanFocus) Focus();
        base.OnMouseDown(e);
    }

    // ✕'e tıklama çipin Click'ini tetiklemez, yalnızca RemoveClick'i.
    protected override void OnClick(EventArgs e)
    {
        if (_pressedRemove)
        {
            _pressedRemove = false;
            if (e is MouseEventArgs mouse && RemoveButtonBounds.Contains(mouse.Location)) PerformRemove();
            return;
        }
        if (_checkable) Checked = !_checked;
        base.OnClick(e);
    }

    /// <summary>Raises <see cref="RemoveClick"/> as if the ✕ were clicked.</summary>
    public void PerformRemove()
    {
        if (_removable && Enabled) RemoveClick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clicks the chip (toggles <see cref="Checked"/> when checkable).</summary>
    /// <summary>Draws attention to the chip: <paramref name="count"/> rings spread out from it and fade (see <see cref="TwAttention.Pulse"/>).</summary>
    public void Pulse(int count = 2) => TwAttention.Pulse(this, count);

    /// <summary>Corner radius the chip is drawn with (a pill without <c>rounded-*</c>).</summary>
    internal float DrawnRadius => _chrome.Equals(TwChrome.None) ? 9999 : _chrome.Radius;

    public void PerformClick()
    {
        if (Enabled && Interactive) OnClick(EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Delete or Keys.Back || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _focusVisible = true;
        switch (e.KeyCode)
        {
            case Keys.Delete or Keys.Back when _removable:
                PerformRemove();
                e.Handled = true;
                break;
            case Keys.Enter or Keys.Space when Interactive:
                PerformClick();
                e.Handled = true;
                break;
        }
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
        var bounds = ClientRectangle;
        // Class verilmemişse hap biçimi.
        float radius = _chrome.Equals(TwChrome.None) ? 9999 : _chrome.Radius;
        TwPainter.FillSurface(g, bounds, radius, BackColor, _chrome.Gradient);
        TwPainter.StrokeChrome(g, bounds, _chrome);

        var fore = Enabled ? ForeColor : Color.FromArgb(128, ForeColor);
        bool rtl = TwRtl.NeedsMirroring(this);
        int gap = ScalePixels(4);
        int left = Padding.Left, right = Width - Padding.Right;
        var remove = RemoveButtonBounds;
        if (!remove.IsEmpty)
        {
            if (rtl) left = remove.Right + gap;
            else right = remove.Left - gap;
        }

        int leading = LeadingWidth;
        if (leading > 0)
        {
            var area = new Rectangle(rtl ? right - leading : left, 0, leading, Height);
            if (LeadingIcon is string icon) TwIconRenderer.DrawIcon(g, icon, IconEm, area, fore);
            else
            {
                var dot = new Rectangle(area.X, (Height - leading) / 2, leading, leading);
                TwPainter.FillRounded(g, dot, leading / 2f, Enabled ? _dotColor : Color.FromArgb(128, _dotColor));
            }
            int shift = leading + gap + (LeadingIcon is null ? ScalePixels(1) : 0);
            if (rtl) right -= shift;
            else left += shift;
        }

        if (Text.Length > 0)
        {
            var flags = TextFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : 0);
            TextRenderer.DrawText(g, Text, Font, Rectangle.FromLTRB(left, 0, Math.Max(left, right), Height), fore, flags);
        }

        if (!remove.IsEmpty)
        {
            if (_hoverRemove && Enabled) TwPainter.FillRounded(g, remove, remove.Width / 2f, Color.FromArgb(40, ForeColor));
            TwIconRenderer.DrawIcon(g, "x", (int)(remove.Width * 0.75f), remove, _hoverRemove ? ForeColor : Color.FromArgb(Enabled ? 190 : 110, ForeColor));
        }

        if (Focused && _focusVisible && ShowFocusCues)
            TwPainter.StrokeRounded(g, Rectangle.Inflate(bounds, -1, -1), Math.Max(0, radius - 1), Math.Max(1f, DpiScale), Color.FromArgb(160, ForeColor));
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new TwChipAccessibleObject(this);

    private sealed class TwChipAccessibleObject : ControlAccessibleObject
    {
        private readonly TwChip _chip;

        public TwChipAccessibleObject(TwChip chip) : base(chip) => _chip = chip;

        public override AccessibleRole Role => _chip.AccessibleRole != AccessibleRole.Default ? _chip.AccessibleRole
            : _chip._checkable ? AccessibleRole.CheckButton
            : _chip._clickable ? AccessibleRole.PushButton
            : AccessibleRole.StaticText;

        public override string? Name
        {
            get => _chip.AccessibleName ?? _chip.Text;
            set { }
        }

        public override AccessibleStates State
        {
            get
            {
                var state = base.State;
                if (_chip._checkable && _chip._checked) state |= AccessibleStates.Checked;
                return state;
            }
        }

        public override string? DefaultAction => _chip.Interactive ? (_chip._checkable ? TwTexts.Current.Select : TwTexts.Current.Open) : null;

        public override void DoDefaultAction()
        {
            if (_chip.Interactive) _chip.PerformClick();
        }

        public override int GetChildCount() => _chip._removable ? 1 : 0;

        public override AccessibleObject? GetChild(int index) => index == 0 && _chip._removable ? new RemoveButton(_chip, this) : null;
    }

    // ✕ ekran okuyucuya ayrı bir "Kaldır" düğmesi olarak görünür.
    private sealed class RemoveButton : AccessibleObject
    {
        private readonly TwChip _chip;
        private readonly AccessibleObject _parent;

        public RemoveButton(TwChip chip, AccessibleObject parent)
        {
            _chip = chip;
            _parent = parent;
        }

        public override string Name => TwTexts.Current.Remove + (_chip.Text.Length > 0 ? " " + _chip.Text : "");

        public override AccessibleRole Role => AccessibleRole.PushButton;

        public override AccessibleObject Parent => _parent;

        public override Rectangle Bounds => _chip.IsHandleCreated ? _chip.RectangleToScreen(_chip.RemoveButtonBounds) : Rectangle.Empty;

        public override AccessibleStates State => _chip.Enabled ? AccessibleStates.Focusable : AccessibleStates.Unavailable;

        public override string DefaultAction => TwTexts.Current.Remove;

        public override void DoDefaultAction() => _chip.PerformRemove();
    }
}
