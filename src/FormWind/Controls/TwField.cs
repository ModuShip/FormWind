using System;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>
/// Form field: a label on top, a single editor below (TwTextBox, TwSelect, TwDatePicker …) and a hint or error text at the
/// bottom. The rules (<see cref="Required"/>, <see cref="Pattern"/>, <see cref="Validator"/>, mask completeness) are checked
/// with <see cref="TailwindProvider.Validate"/>; while there is an error the <c>invalid:</c> variant is applied to the editor.
/// Styles: <c>label:text-* label:font-*</c>, <c>hint:text-*</c>, <c>error:text-*</c>.
/// </summary>
[ToolboxItem(true)]
[Description("Labeled form field with validation.")]
public class TwField : TwPanel
{
    private readonly Label _caption;
    private readonly Label _message;
    private string _labelText = "";
    private string _hintText = "";
    private string? _errorText;
    private bool _required;
    private Color _captionColor = ColorTranslator.FromHtml("#374151");   // gray-700
    private Color _hintColor = ColorTranslator.FromHtml("#6b7280");      // gray-500
    private Color _errorColor = ColorTranslator.FromHtml("#dc2626");     // red-600
    private Font? _captionFont;
    private Font? _messageFont;

    public TwField()
    {
        _caption = new Label { AutoSize = true, BackColor = Color.Transparent, Visible = false };
        _message = new Label { AutoSize = false, BackColor = Color.Transparent, Visible = false };
        Controls.Add(_caption);
        Controls.Add(_message);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    /// <summary>When the error text changes.</summary>
    public event EventHandler? ErrorChanged;

    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value ?? "";
            UpdateCaption();
        }
    }

    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string HintText
    {
        get => _hintText;
        set
        {
            _hintText = value ?? "";
            UpdateMessage();
        }
    }

    /// <summary>The displayed error (<c>null</c> when none). Usually set with <see cref="TailwindProvider.SetError"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ErrorText
    {
        get => _errorText;
        set
        {
            var text = string.IsNullOrWhiteSpace(value) ? null : value;
            if (text == _errorText) return;
            _errorText = text;
            UpdateMessage();
            ErrorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Category("Validation"), DefaultValue(false)]
    public bool Required
    {
        get => _required;
        set
        {
            _required = value;
            UpdateCaption();
        }
    }

    private string? _requiredMessage, _patternMessage, _incompleteMessage;

    /// <summary>Shown when a required field is empty; <see cref="TwTexts.Required"/> when not set.</summary>
    [Category("Validation"), Localizable(true)]
    public string RequiredMessage
    {
        get => _requiredMessage ?? TwTexts.Current.Required;
        set => _requiredMessage = value;
    }

    private bool ShouldSerializeRequiredMessage() => _requiredMessage != null;

    private void ResetRequiredMessage() => _requiredMessage = null;

    /// <summary>Regular expression the value must match (an empty value is left to <see cref="Required"/>).</summary>
    [Category("Validation"), DefaultValue("")]
    public string Pattern { get; set; } = "";

    /// <summary>Shown when the value does not match <see cref="Pattern"/>; <see cref="TwTexts.InvalidFormat"/> when not set.</summary>
    [Category("Validation"), Localizable(true)]
    public string PatternMessage
    {
        get => _patternMessage ?? TwTexts.Current.InvalidFormat;
        set => _patternMessage = value;
    }

    private bool ShouldSerializePatternMessage() => _patternMessage != null;

    private void ResetPatternMessage() => _patternMessage = null;

    /// <summary>Shown when a mask is incomplete; <see cref="TwTexts.Incomplete"/> when not set.</summary>
    [Category("Validation"), Localizable(true)]
    public string IncompleteMessage
    {
        get => _incompleteMessage ?? TwTexts.Current.Incomplete;
        set => _incompleteMessage = value;
    }

    private bool ShouldSerializeIncompleteMessage() => _incompleteMessage != null;

    private void ResetIncompleteMessage() => _incompleteMessage = null;

    /// <summary>Custom rule: takes the value and returns an error text or <c>null</c> (e.g. <see cref="TwValidators.TcKimlikNo"/>).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, string?>? Validator { get; set; }

    /// <summary>The field's editor: the first child other than the label and the message.</summary>
    [Browsable(false)]
    public Control? Editor
    {
        get
        {
            foreach (Control child in Controls)
                if (child != _caption && child != _message) return child;
            return null;
        }
    }

    [Browsable(false)]
    public Label CaptionLabel => _caption;

    [Browsable(false)]
    public Label MessageLabel => _message;

    internal void SetPartStyles(Color? caption, Font? captionFont, Color? hint, Color? error, Font? messageFont)
    {
        _captionColor = caption ?? ForeColor;
        _hintColor = hint ?? ColorTranslator.FromHtml("#6b7280");
        _errorColor = error ?? ColorTranslator.FromHtml("#dc2626");
        _captionFont = captionFont;
        _messageFont = messageFont;
        UpdateCaption();
        UpdateMessage();
    }

    /// <summary>The editor's value (text box text, selected item, date …).</summary>
    public string EditorValue => Editor switch
    {
        TwDatePicker picker => picker.Value?.ToString("d", picker.Culture) ?? "",
        TwSelect select => select.SelectedItem ?? "",
        TwTextBox box => box.HasMask ? box.UnmaskedText : box.Text,
        CheckBox check => check.Checked ? check.Text : "",
        null => "",
        var other => other.Text,
    };

    /// <summary>Checks the rules; returns the error text, or <c>null</c> when valid. Does not change <see cref="ErrorText"/>.</summary>
    public string? Validate()
    {
        var value = EditorValue.Trim();
        if (value.Length == 0) return _required ? RequiredMessage : null;
        if (Editor is TwTextBox { HasMask: true } masked && !masked.MaskCompleted) return IncompleteMessage;
        if (Pattern.Length > 0 && !Regex.IsMatch(value, Pattern, RegexOptions.CultureInvariant)) return PatternMessage;
        return Validator?.Invoke(value);
    }

    public override LayoutEngine LayoutEngine => DefaultLayoutEngine;

    private static readonly LayoutEngine DefaultLayoutEngine = new Panel().LayoutEngine;

    private int Gap => (int)Math.Round(4 * TwDpi.DeviceDpi(this) / 96f);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var display = Padding;
        int width = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue / 2 ? proposedSize.Width : Width;
        int inner = Math.Max(0, width - display.Horizontal);
        int height = display.Vertical;
        if (_caption.Visible) height += _caption.PreferredHeight + Gap;
        if (Editor is Control editor) height += EditorHeight(editor, inner);
        if (_message.Visible) height += Gap + MessageHeight(inner);
        return new Size(width, height);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var rect = DisplayRectangle;
        int y = rect.Top;
        if (_caption.Visible)
        {
            _caption.Location = new Point(rect.Left, y);
            y += _caption.PreferredHeight + Gap;
        }
        if (Editor is Control editor)
        {
            int height = EditorHeight(editor, rect.Width);
            editor.Bounds = new Rectangle(rect.Left, y, rect.Width, height);
            y += height;
        }
        if (_message.Visible)
        {
            y += Gap;
            _message.Bounds = new Rectangle(rect.Left, y, rect.Width, MessageHeight(rect.Width));
        }
        if (AutoSize && Parent != null) Height = GetPreferredSize(new Size(Width, 0)).Height;
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control != _caption && e.Control != _message)
        {
            e.Control!.SizeChanged += (_, _) => PerformLayout();
            if (string.IsNullOrEmpty(e.Control.AccessibleName) && _labelText.Length > 0) e.Control.AccessibleName = _labelText;
        }
        PerformLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateCaption();
        UpdateMessage();
    }

    private static int EditorHeight(Control editor, int width) =>
        editor.AutoSize ? editor.GetPreferredSize(new Size(width, 0)).Height : editor.Height;

    private int MessageHeight(int width) =>
        TextRenderer.MeasureText(_message.Text, _message.Font, new Size(Math.Max(1, width), 0), TextFormatFlags.WordBreak).Height;

    private void UpdateCaption()
    {
        _caption.Text = _labelText + (_required && _labelText.Length > 0 ? " *" : "");
        _caption.Visible = _labelText.Length > 0;
        _caption.ForeColor = _captionColor;
        _caption.Font = _captionFont ?? Font;
        if (Editor is Control editor && string.IsNullOrEmpty(editor.AccessibleName)) editor.AccessibleName = _labelText;
        PerformLayout();
    }

    private void UpdateMessage()
    {
        bool error = _errorText != null;
        _message.Text = error ? _errorText! : _hintText;
        _message.ForeColor = error ? _errorColor : _hintColor;
        _message.Font = _messageFont ?? Font;
        _message.Visible = _message.Text.Length > 0;
        _message.AccessibleRole = error ? AccessibleRole.Alert : AccessibleRole.StaticText;
        if (Editor is Control editor) editor.AccessibleDescription = error ? _errorText : (_hintText.Length > 0 ? _hintText : null);
        PerformLayout();
        Parent?.PerformLayout(this, nameof(ErrorText));
    }
}
