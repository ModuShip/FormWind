using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A small info icon next to a term ("OEE", "MTBF") that opens a definition card: hovering shows it without taking the focus
/// and it stays while the pointer is in the card; a click, Enter or Space opens and closes it (with
/// <see cref="ClickOpensCard"/> = <c>false</c> a click and Enter raise <c>Click</c> instead). The card has a bold
/// <see cref="Title"/> and the wrapped <see cref="Description"/>, in the colors around the icon (dark on a dark page) until
/// <see cref="Card"/> is given classes or colors of its own.
/// <code>
/// var oee = new TwInfoTip { Title = "OEE", Description = "Availability × performance × quality." };
/// header.Controls.Add(oee);
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Description))]
[Description("Info icon that shows a definition card.")]
public class TwInfoTip : Control
{
    private const int CardPadding = 12;
    private const int DefaultCardWidth = 280;
    private const int HoverDelay = 300;

    private readonly Label _title;
    private readonly Label _description;
    private TwPopover? _popover;
    private string _iconName = "info";
    private string _titleText = "";
    private string _descriptionText = "";
    private int _maxCardWidth = DefaultCardWidth;
    private bool _hovered;
    private Font? _boldFont;
    // Uygulama ya da sınıflar kartın rengini verdiyse ikonun çevresinin renkleri artık atanmaz.
    private bool _customBack, _customFore, _assigning;

    public TwInfoTip()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        TabStop = true;
        Cursor = Cursors.Help;
        AccessibleRole = AccessibleRole.HelpBalloon;
        _title = new Label { AutoSize = true, Dock = DockStyle.Top, UseMnemonic = false, BackColor = Color.Transparent };
        _description = new Label { AutoSize = true, Dock = DockStyle.Top, UseMnemonic = false, BackColor = Color.Transparent };
        Card = new TwPanel { CornerRadius = 8, BorderWidth = 1, Padding = new Padding(CardPadding) };
        // Dock = Top sırası: sonra eklenen üstte durur.
        Card.Controls.Add(_description);
        Card.Controls.Add(_title);
        Card.BackColorChanged += (_, _) => _customBack |= !_assigning;
        Card.ForeColorChanged += (_, _) => _customFore |= !_assigning;
    }

    protected override Size DefaultSize => new Size(16, 16);

    /// <summary>The card's bold first line; empty = no title line.</summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string Title
    {
        get => _titleText;
        set
        {
            _titleText = value ?? "";
            AccessibleName = _titleText.Length > 0 ? _titleText : null;
        }
    }

    /// <summary>The definition, wrapped at <see cref="MaxCardWidth"/>.</summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string Description
    {
        get => _descriptionText;
        set
        {
            _descriptionText = value ?? "";
            AccessibleDescription = _descriptionText.Length > 0 ? _descriptionText : null;
        }
    }

    /// <summary>The icon drawn (a name of the icon set); default <c>info</c>, <c>help</c> for a question mark.</summary>
    [Category("Appearance"), DefaultValue("info")]
    public string IconName
    {
        get => _iconName;
        set
        {
            _iconName = string.IsNullOrEmpty(value) ? "info" : value;
            Invalidate();
        }
    }

    /// <summary>The widest the card gets before the description wraps (96 DPI pixels).</summary>
    [Category("Layout"), DefaultValue(DefaultCardWidth)]
    public int MaxCardWidth
    {
        get => _maxCardWidth;
        set => _maxCardWidth = Math.Max(80, value);
    }

    /// <summary>The card shown in the popover; give it classes (<c>card</c>, <c>bg-*</c>) or colors to style it.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwPanel Card { get; }

    /// <summary>Whether the card is open.</summary>
    [Browsable(false)]
    public bool IsOpen => _popover?.IsOpen == true;

    /// <summary>Opens the card.</summary>
    public void ShowCard()
    {
        if (!IsHandleCreated || !Visible) return;
        Popover.Show(this);
    }

    /// <summary>Closes the card.</summary>
    public void HideCard() => _popover?.Close();

    private TwPopover Popover
    {
        get
        {
            if (_popover != null) return _popover;
            _popover = new TwPopover { Content = Card, FocusContent = false, Placement = TwPopoverPlacement.BottomStart };
            _popover.Opening += (_, _) => PrepareCard();
            _popover.ShowOnHover(this, HoverDelay);
            return _popover;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _ = Popover;   // the hover opening starts with the icon on screen
    }

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    // Kart, açılışta metinlere ve ikonun çevresinin renklerine göre hazırlanır.
    private void PrepareCard()
    {
        _assigning = true;
        try
        {
            if (!_customBack) Card.BackColor = TwColorsOf.EffectiveBack(this);
            if (!_customFore) Card.ForeColor = ForeColor;
        }
        finally
        {
            _assigning = false;
        }
        Card.BorderColor = TwMotion.Blend(Card.BackColor, Card.ForeColor, 0.18f);
        Card.Font = Font;

        int padding = Scale(CardPadding), width = Scale(_maxCardWidth);
        Card.Padding = new Padding(padding);
        _title.Text = _titleText;
        _title.Visible = _titleText.Length > 0;
        if (_boldFont is null || !_boldFont.FontFamily.Equals(Font.FontFamily) || _boldFont.Size != Font.Size)
        {
            var old = _boldFont;
            _boldFont = new Font(Font, FontStyle.Bold);
            _title.Font = _boldFont;
            old?.Dispose();
        }
        _title.Margin = Padding.Empty;
        _description.Text = _descriptionText;
        _description.Visible = _descriptionText.Length > 0;
        _description.MaximumSize = new Size(width - 2 * padding, 0);

        int inner = width - 2 * padding;
        var title = _title.Visible ? TextRenderer.MeasureText(_titleText, _title.Font, new Size(inner, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix) : Size.Empty;
        var text = _description.Visible ? _description.GetPreferredSize(new Size(inner, 0)) : Size.Empty;
        int gap = _title.Visible && _description.Visible ? Scale(4) : 0;
        _title.Padding = new Padding(0, 0, 0, gap);
        Card.Size = new Size(Math.Min(width, Math.Max(title.Width, text.Width) + 2 * padding), title.Height + gap + text.Height + 2 * padding);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        TwPainter.PaintParentBackground(this, e.Graphics);
        var back = TwColorsOf.EffectiveBack(this);
        var color = Enabled
            ? _hovered || Focused || IsOpen ? ForeColor : TwMotion.Blend(back, ForeColor, 0.6f)
            : TwMotion.Blend(back, ForeColor, 0.35f);
        int size = Math.Min(Width, Height);
        TwIconRenderer.DrawIcon(e.Graphics, _iconName, size, new Rectangle((Width - size) / 2, (Height - size) / 2, size, size), color);
        if (Focused && ShowFocusCues)
            TwPainter.StrokeRounded(e.Graphics, ClientRectangle, size / 2f, 1f, ForeColor);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    /// <summary>
    /// Whether a click and Enter open and close the card (default). <c>false</c> = they raise <see cref="Control.Click"/> only
    /// (e.g. to open a glossary page) and close an open card; the card still opens on hover and Space toggles it for the keyboard.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool ClickOpensCard { get; set; } = true;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (ClickOpensCard) Toggle();
        else HideCard();
    }

    // Enter formun AcceptButton'una, Esc CancelButton'una gitmeden ikona gelir (Esc yalnızca kart açıkken).
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Enter or Keys.Space || keyData == Keys.Escape && IsOpen || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Enter && !ClickOpensCard)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            Toggle();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape && IsOpen)
        {
            HideCard();
            e.Handled = true;
        }
    }

    private void Toggle()
    {
        if (IsOpen && !Popover.IsOpenFromHover) HideCard();
        else ShowCard();
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Açılır pencere kartın sahibidir; hiç açılmadıysa kart burada atılır.
            if (_popover != null) _popover.Dispose();
            else Card.Dispose();
            _boldFont?.Dispose();
        }
        base.Dispose(disposing);
    }
}
