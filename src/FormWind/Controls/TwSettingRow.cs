using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>
/// A settings row (System Settings style): a label and a muted description on the left, the editor (the child control: a
/// text box, number box, switch, select …) and a unit ("min", "%") on the right. Rows stacked in a panel form a group: each
/// row after another one draws a divider at its top (<c>divide-*</c>), so a <c>card p-0 flex flex-col</c> panel of rows reads
/// as one grouped list. Narrow rows put the editor under the text. Styled with <c>label:</c> (the label), <c>hint:</c> (the
/// description and unit) and the <c>setting</c> component.
/// <code>
/// var row = new TwSettingRow { LabelText = "Micro-stop limit", Description = "Shorter stops are counted as speed loss", UnitText = "min" };
/// row.Controls.Add(new TwNumberBox { Value = 5 });
/// group.Controls.Add(row);
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(LabelText))]
[Description("Settings row: label, description, editor and unit.")]
public class TwSettingRow : TwPanel
{
    private readonly Label _label;
    private readonly Label _description;
    private readonly Label _unit;
    private int _editorWidth = 160;
    private Color _labelColor = Color.Empty;
    private Color _hintColor = Color.Empty;
    private Color _dividerColor = Color.Empty;
    private Font? _labelFont;
    private Font? _hintFont;

    public TwSettingRow()
    {
        _label = new Label { AutoSize = false, BackColor = Color.Transparent, AutoEllipsis = true, UseMnemonic = false };
        _description = new Label { AutoSize = false, BackColor = Color.Transparent, Visible = false, UseMnemonic = false };
        _unit = new Label { AutoSize = true, BackColor = Color.Transparent, Visible = false, UseMnemonic = false };
        Controls.Add(_label);
        Controls.Add(_description);
        Controls.Add(_unit);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ApplyPartStyles();
    }

    protected override Size DefaultSize => new Size(480, 56);

    protected override Padding DefaultPadding => new Padding(16, 12, 16, 12);

    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string LabelText
    {
        get => _label.Text;
        set
        {
            _label.Text = value ?? "";
            PerformLayout();
        }
    }

    /// <summary>Muted text under the label (wraps); empty = none.</summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string Description
    {
        get => _description.Text;
        set
        {
            _description.Text = value ?? "";
            _description.Visible = _description.Text.Length > 0;
            PerformLayout();
        }
    }

    /// <summary>Unit after the editor ("min", "%"); empty = none.</summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string UnitText
    {
        get => _unit.Text;
        set
        {
            _unit.Text = value ?? "";
            _unit.Visible = _unit.Text.Length > 0;
            PerformLayout();
        }
    }

    /// <summary>Width of the editor (96 DPI pixels); 0 = the editor's own width.</summary>
    [Category("Layout"), DefaultValue(160)]
    public int EditorWidth
    {
        get => _editorWidth;
        set
        {
            _editorWidth = Math.Max(0, value);
            PerformLayout();
        }
    }

    /// <summary>The row's editor: the first child other than its labels.</summary>
    [Browsable(false)]
    public Control? Editor => Controls.Cast<Control>().FirstOrDefault(c => c != _label && c != _description && c != _unit);

    [Browsable(false)]
    public Label CaptionLabel => _label;

    [Browsable(false)]
    public Label DescriptionLabel => _description;

    [Browsable(false)]
    public Label UnitLabel => _unit;

    internal void SetPartStyles(Color? label, Font? labelFont, Color? hint, Font? hintFont, Color? divider)
    {
        _labelColor = label ?? Color.Empty;
        _hintColor = hint ?? Color.Empty;
        _labelFont = labelFont;
        _hintFont = hintFont;
        _dividerColor = divider ?? Color.Empty;
        ApplyPartStyles();
        PerformLayout();
        Invalidate();
    }

    private void ApplyPartStyles()
    {
        _label.ForeColor = _labelColor.IsEmpty ? ForeColor : _labelColor;
        var hint = _hintColor.IsEmpty ? TwCharts.Blend(ForeColor, TwCharts.Surface(this), 0.4f) : _hintColor;
        _description.ForeColor = hint;
        _unit.ForeColor = hint;
        _label.Font = _labelFont ?? Font;
        _description.Font = _hintFont ?? Font;
        _unit.Font = _hintFont ?? Font;
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        ApplyPartStyles();
    }

    // Soluk açıklama rengi zeminden türetilir; zemin değişince yeniden hesaplanır.
    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        ApplyPartStyles();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ApplyPartStyles();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyPartStyles();
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        PerformLayout();
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        PerformLayout();
    }

    public override LayoutEngine LayoutEngine => RowLayout.Instance;

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    // Yerleşimin ölçüleri: sol metin bloğu ve sağ blok (düzenleyici ve birim); dar satırda düzenleyici metnin altına iner.
    private (Rectangle Label, Rectangle Description, Rectangle Editor, Rectangle Unit, int Height) Measure(int width)
    {
        var border = BorderInsets();
        int left = border.Left + Padding.Left, right = width - border.Right - Padding.Right, top = border.Top + Padding.Top;
        int inner = Math.Max(0, right - left);
        var editor = Editor;
        var editorSize = editor is null ? Size.Empty : editor.AutoSize ? editor.GetPreferredSize(Size.Empty) : editor.Size;
        if (editor != null && _editorWidth > 0) editorSize.Width = Scale(_editorWidth);
        int gap = Scale(8), between = Scale(16);
        var unitSize = _unit.Visible ? TextRenderer.MeasureText(_unit.Text, _unit.Font, Size.Empty, Flags | TextFormatFlags.SingleLine) : Size.Empty;
        int block = editor is null ? 0 : editorSize.Width + (_unit.Visible ? gap + unitSize.Width : 0);
        int textWidth = inner - (block > 0 ? block + between : 0);
        bool stacked = block > 0 && textWidth < Scale(140);
        if (stacked) textWidth = inner;

        int labelHeight = TextRenderer.MeasureText("Ag", _label.Font, Size.Empty, Flags | TextFormatFlags.SingleLine).Height;
        int descriptionHeight = _description.Visible
            ? TextRenderer.MeasureText(_description.Text, _description.Font, new Size(Math.Max(1, textWidth), 0), Flags | TextFormatFlags.WordBreak).Height
            : 0;
        int textHeight = labelHeight + (descriptionHeight > 0 ? Scale(2) + descriptionHeight : 0);

        Rectangle label, description, editorBounds = Rectangle.Empty, unit = Rectangle.Empty;
        int height;
        if (stacked)
        {
            label = new Rectangle(left, top, textWidth, labelHeight);
            description = new Rectangle(left, label.Bottom + Scale(2), textWidth, descriptionHeight);
            int y = top + textHeight + gap;
            editorBounds = new Rectangle(left, y, Math.Min(editorSize.Width, inner - (_unit.Visible ? gap + unitSize.Width : 0)), editorSize.Height);
            if (_unit.Visible) unit = new Rectangle(editorBounds.Right + gap, y + (editorSize.Height - unitSize.Height) / 2, unitSize.Width, unitSize.Height);
            height = y + editorSize.Height - top;
        }
        else
        {
            height = Math.Max(textHeight, editorSize.Height);
            int textTop = top + (height - textHeight) / 2;
            label = new Rectangle(left, textTop, Math.Max(0, textWidth), labelHeight);
            description = new Rectangle(left, label.Bottom + Scale(2), Math.Max(0, textWidth), descriptionHeight);
            if (editor != null)
            {
                int x = right - block;
                editorBounds = new Rectangle(x, top + (height - editorSize.Height) / 2, editorSize.Width, editorSize.Height);
                if (_unit.Visible) unit = new Rectangle(editorBounds.Right + gap, top + (height - unitSize.Height) / 2, unitSize.Width, unitSize.Height);
            }
        }
        return (label, description, editorBounds, unit, height + Padding.Vertical + border.Vertical);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue / 2 ? proposedSize.Width : Width;
        return new Size(width, Measure(width).Height);
    }

    private void Arrange()
    {
        var (label, description, editor, unit, height) = Measure(Width);
        bool rtl = TwRtl.NeedsMirroring(this);
        Rectangle Mirror(Rectangle r) => rtl ? new Rectangle(Width - r.Right, r.Y, r.Width, r.Height) : r;
        _label.Bounds = Mirror(label);
        _label.TextAlign = rtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
        _description.Bounds = Mirror(description);
        _description.TextAlign = rtl ? ContentAlignment.TopRight : ContentAlignment.TopLeft;
        if (Editor is Control control) control.Bounds = Mirror(editor);
        if (_unit.Visible) _unit.Bounds = Mirror(unit);
        if (AutoSize && Height != height && Dock is not (DockStyle.Fill or DockStyle.Left or DockStyle.Right)) Height = height;
    }

    /// <summary>Whether a divider is drawn at the top: another row ends right above this one.</summary>
    internal bool HasDividerAbove => Parent != null && Parent.Controls.OfType<TwSettingRow>()
        .Any(r => r != this && r.Visible && Math.Abs(r.Bottom - Top) <= Scale(2) && r.Left < Right && r.Right > Left);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!HasDividerAbove) return;
        // Ayırıcı metin hizasından başlar (System Settings gibi), sağ kenara kadar gider.
        var color = !_dividerColor.IsEmpty ? _dividerColor : TwCharts.Blend(TwCharts.Surface(this), ForeColor, 0.12f);
        using var pen = new Pen(color, 1);
        int inset = BorderInsets().Left + Padding.Left;
        if (TwRtl.NeedsMirroring(this)) e.Graphics.DrawLine(pen, 0, 0, Width - inset, 0);
        else e.Graphics.DrawLine(pen, inset, 0, Width, 0);
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        Invalidate();
    }

    private sealed class RowLayout : LayoutEngine
    {
        public static readonly RowLayout Instance = new RowLayout();

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            if (container is TwSettingRow row) row.Arrange();
            return row_AutoSize(container);
        }

        private static bool row_AutoSize(object container) => container is Control { AutoSize: true };
    }
}
