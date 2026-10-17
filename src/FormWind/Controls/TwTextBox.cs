using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Places a borderless <see cref="TextBox"/> inside a panel with rounded corners. Because the native TextBox border
/// cannot be styled, <c>input</c>-style fields are built with this wrapper.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(TextChanged))]
[DefaultProperty(nameof(Text))]
[Description("Stylable text box with rounded corners.")]
public class TwTextBox : TwPanel
{
    private readonly PlaceholderTextBox _inner;
    private bool _fontSyncPending;
    private Form? _dpiForm;
    private MaskedTextProvider? _mask;

    public TwTextBox()
    {
        _inner = new PlaceholderTextBox { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill };
        _inner.TextChanged += (_, e) => OnTextChanged(e);
        _inner.KeyPress += OnMaskKeyPress;
        _inner.KeyDown += OnMaskKeyDown;
        _inner.Enter += (_, _) => OnMaskFocus();
        _inner.Leave += (_, _) => SyncMaskText();
        _inner.PasteHandler = PasteIntoMask;
        Controls.Add(_inner);

        BackColor = SystemColors.Window;
        Padding = new Padding(8, 6, 8, 6);
        Cursor = Cursors.IBeam;
        UpdateHeight();
    }

    /// <summary>The inner TextBox, for properties and events not exposed here.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextBox InnerTextBox => _inner;

#pragma warning disable CS8765 // Control.Text .NET 8'de [AllowNull] ile işaretli.
    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), Bindable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => _mask != null && _mask.AssignedEditPositionCount == 0 ? "" : _inner.Text;
        set
        {
            if (_mask is null)
            {
                _inner.Text = value;
                return;
            }
            SetMasked(value ?? "");
        }
    }
#pragma warning restore CS8765

    /// <summary>Hint text shown while the box is empty. Its color is set with <c>placeholder:text-*</c> (gray-400 by default).</summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string PlaceholderText
    {
        get => _inner.Placeholder;
        set
        {
            _inner.Placeholder = value ?? "";
            _inner.Invalidate();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? PlaceholderColor
    {
        get => _inner.PlaceholderColor;
        set
        {
            _inner.PlaceholderColor = value;
            _inner.Invalidate();
        }
    }

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }

    [Category("Behavior"), DefaultValue(false)]
    public bool ReadOnly
    {
        get => _inner.ReadOnly;
        set => _inner.ReadOnly = value;
    }

    [Category("Behavior"), DefaultValue(false)]
    public bool UseSystemPasswordChar
    {
        get => _inner.UseSystemPasswordChar;
        set => _inner.UseSystemPasswordChar = value;
    }

    [Category("Behavior"), DefaultValue(32767)]
    public int MaxLength
    {
        get => _inner.MaxLength;
        set => _inner.MaxLength = value;
    }

    /// <summary>
    /// Input mask (<see cref="MaskedTextProvider"/> syntax): <c>0</c> required digit, <c>9</c> optional digit,
    /// <c>L</c> letter, <c>A</c> letter/digit; other characters are literals. Ready-made masks: <see cref="TwMasks"/>.
    /// While focused, empty positions are shown as <c>_</c>; when empty and unfocused the hint text shows.
    /// </summary>
    [Category("Behavior"), DefaultValue("")]
    public string Mask
    {
        get => _mask?.Mask ?? "";
        set
        {
            var previous = UnmaskedText;
            _mask = string.IsNullOrEmpty(value) ? null : new MaskedTextProvider(value, CultureInfo.CurrentCulture, false, '_', '\0', false);
            if (_mask != null) SetMasked(previous);
            else _inner.Text = previous;
        }
    }

    [Browsable(false)]
    public bool HasMask => _mask != null;

    /// <summary>Whether all required mask positions are filled (<c>true</c> without a mask).</summary>
    [Browsable(false)]
    public bool MaskCompleted => _mask?.MaskCompleted ?? true;

    /// <summary>The entered value without the mask's literals and blanks (<see cref="Text"/> without a mask).</summary>
    [Browsable(false)]
    public string UnmaskedText => _mask != null ? _mask.ToString(false, false) : _inner.Text;

    [Category("Behavior"), DefaultValue(false)]
    public bool Multiline
    {
        get => _inner.Multiline;
        set
        {
            _inner.Multiline = value;
            UpdateHeight();
        }
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        _inner.BackColor = BackColor.A == 255 ? BackColor : (Parent?.BackColor ?? SystemColors.Window);
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        _inner.ForeColor = ForeColor;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateHeight();
        ScheduleFontSync();
    }

    // .NET Framework'te (PerMonitorV2) ekran değişince WinForms fontları olay tetiklemeden değiştirir (ne FontChanged ne
    // DpiChangedAfterParent gelir); iç TextBox'a WM_SETFONT gitmez, native kutu silinmiş eski font handle'ını tutar ve
    // yazıyı sistem fontuyla küçük çizer. Formun DpiChanged olayı her durumda geldiği için ona abone olunur.
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var form = FindForm();
        if (form == _dpiForm) return;
        if (_dpiForm != null) _dpiForm.DpiChanged -= OnFormDpiChanged;
        _dpiForm = form;
        if (form != null) form.DpiChanged += OnFormDpiChanged;
    }

    private void OnFormDpiChanged(object? sender, DpiChangedEventArgs e) => ScheduleFontSync();

    protected override void Dispose(bool disposing)
    {
        if (disposing && _dpiForm != null)
        {
            _dpiForm.DpiChanged -= OnFormDpiChanged;
            _dpiForm = null;
        }
        base.Dispose(disposing);
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        UpdateHeight();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _inner.Focus();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _inner.Focus();
    }

    private void SetMasked(string value)
    {
        if (_mask is null) return;
        _mask.Clear();
        if (!_mask.Set(value))
        {
            // Sabit karakterleri içermeyen değer ("5321234567"): girilebilen karakterler sırayla yerleştirilir.
            _mask.Clear();
            int position = 0;
            foreach (var c in value)
            {
                if (char.IsWhiteSpace(c)) continue;
                position = _mask.FindEditPositionFrom(position, true);
                if (position < 0) break;
                if (_mask.Replace(c, position, out var test, out _)) position = test + 1;
            }
        }
        SyncMaskText();
    }

    private void SyncMaskText()
    {
        if (_mask is null) return;
        bool show = _inner.Focused || _mask.AssignedEditPositionCount > 0;
        var text = show ? _mask.ToDisplayString() : "";
        if (_inner.Text != text) _inner.Text = text;
    }

    private void OnMaskFocus()
    {
        if (_mask is null) return;
        SyncMaskText();
        int first = _mask.FindUnassignedEditPositionFrom(0, true);
        BeginInvoke((Action)(() => _inner.Select(first < 0 ? _mask?.Length ?? 0 : first, 0)));
    }

    private void OnMaskKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_mask is null || _inner.ReadOnly || char.IsControl(e.KeyChar)) return;
        e.Handled = true;
        int start = _inner.SelectionStart, length = _inner.SelectionLength;
        if (length > 0) _mask.RemoveAt(start, Math.Min(_mask.Length - 1, start + length - 1));
        int position = _mask.FindEditPositionFrom(start, true);
        if (position < 0 || !_mask.Replace(e.KeyChar, position, out var test, out _))
        {
            SyncMaskText();
            return;
        }
        SyncMaskText();
        int next = _mask.FindEditPositionFrom(test + 1, true);
        _inner.Select(next < 0 ? _mask.Length : next, 0);
    }

    private void OnMaskKeyDown(object? sender, KeyEventArgs e)
    {
        if (_mask is null || _inner.ReadOnly || (e.KeyCode != Keys.Back && e.KeyCode != Keys.Delete)) return;
        e.Handled = e.SuppressKeyPress = true;
        int start = _inner.SelectionStart, length = _inner.SelectionLength;
        int caret = start;
        if (length > 0)
        {
            _mask.RemoveAt(start, Math.Min(_mask.Length - 1, start + length - 1));
        }
        else if (e.KeyCode == Keys.Back)
        {
            int position = start > 0 ? _mask.FindEditPositionFrom(start - 1, false) : -1;
            if (position < 0) return;
            _mask.RemoveAt(position);
            caret = position;
        }
        else
        {
            int position = _mask.FindEditPositionFrom(start, true);
            if (position < 0) return;
            _mask.RemoveAt(position);
        }
        SyncMaskText();
        _inner.Select(caret, 0);
    }

    // Yapıştırılan metin imleçten itibaren girilebilen yerlere yerleştirilir.
    private bool PasteIntoMask(string text)
    {
        if (_mask is null) return false;
        if (_inner.ReadOnly) return true;
        int start = _inner.SelectionStart, length = _inner.SelectionLength;
        if (length > 0) _mask.RemoveAt(start, Math.Min(_mask.Length - 1, start + length - 1));
        int position = start;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
            int target = _mask.FindEditPositionFrom(position, true);
            if (target < 0) break;
            if (_mask.Replace(c, target, out var test, out _)) position = test + 1;
        }
        SyncMaskText();
        _inner.Select(Math.Min(position, _mask.Length), 0);
        return true;
    }

    // DPI değişiminde WinForms yüksekliği de oranla büyütür; yükseklik fonttan ve iç boşluktan yeniden hesaplanır.
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, specified);
        UpdateHeight();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateHeight();
        // İç TextBox'ın native fontu ekran değişince her zaman yenilenmiyor (yazı eski boyutta kalıyor); ölçekleme
        // bittikten sonra font yeniden verilir.
        ScheduleFontSync();
    }

    private void ScheduleFontSync()
    {
        if (!IsHandleCreated || _fontSyncPending) return;
        _fontSyncPending = true;
        BeginInvoke((Action)SyncInnerFont);
    }

    private void SyncInnerFont()
    {
        _fontSyncPending = false;
        if (IsDisposed) return;
        // Geçici olarak ayrı bir font verip mirasa dönmek, native fontun yeniden gönderilmesini zorlar.
        using (var refresh = new Font(Font, Font.Style))
        {
            _inner.Font = refresh;
            _inner.ResetFont();
        }
        UpdateHeight();
    }

    private void UpdateHeight()
    {
        if (!_inner.Multiline)
            Height = _inner.PreferredHeight + Padding.Vertical;
    }

    /// <summary>
    /// A TextBox that draws the hint text over its own painting while empty. The native hint (EM_SETCUEBANNER)
    /// color cannot be changed, so it is drawn after WM_PAINT.
    /// </summary>
    private sealed class PlaceholderTextBox : TextBox
    {
        private const int WmPaint = 0x000F;
        private const int WmPrint = 0x0317;
        private const int WmPrintClient = 0x0318;
        private static readonly Color DefaultColor = ColorTranslator.FromHtml("#9ca3af"); // gray-400

        public string Placeholder { get; set; } = "";

        public Color? PlaceholderColor { get; set; }

        /// <summary>Takes over pasting (mask); returning <c>true</c> skips the default paste.</summary>
        public Func<string, bool>? PasteHandler { get; set; }

        // Odak içteki TextBox'ta olduğundan ekran okuyucu onu okur: ad ve açıklama sarmalayıcıdan gelir
        // (AccessibleName yoksa ipucu metni).
        protected override AccessibleObject CreateAccessibilityInstance() => new InnerAccessibleObject(this);

        private sealed class InnerAccessibleObject : ControlAccessibleObject
        {
            private readonly PlaceholderTextBox _box;

            public InnerAccessibleObject(PlaceholderTextBox box) : base(box) => _box = box;

            public override string? Name
            {
                get => _box.AccessibleName ?? _box.Parent?.AccessibleName ?? base.Name
                       ?? (_box.Placeholder.Length > 0 ? _box.Placeholder : null);
                set => base.Name = value;
            }

            public override string? Description
            {
                get => _box.AccessibleDescription ?? _box.Parent?.AccessibleDescription ?? base.Description;
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (TextLength <= 1) Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            const int WmPaste = 0x0302;
            if (m.Msg == WmPaste && PasteHandler != null && Clipboard.ContainsText() && PasteHandler(Clipboard.GetText())) return;
            base.WndProc(ref m);
            // Önce mesaj türü: TextLength kendisi WM_GETTEXTLENGTH gönderir ve buraya geri döner.
            if (m.Msg != WmPaint && m.Msg != WmPrint && m.Msg != WmPrintClient) return;
            if (Placeholder.Length == 0 || TextLength > 0) return;

            // WM_PRINT/WM_PRINTCLIENT: DrawToBitmap ve yazdırma; çizim verilen HDC'ye yapılır.
            if (m.Msg == WmPaint)
            {
                using var graphics = Graphics.FromHwnd(Handle);
                DrawPlaceholder(graphics);
            }
            else if ((m.Msg == WmPrint || m.Msg == WmPrintClient) && m.WParam != IntPtr.Zero)
            {
                using var graphics = Graphics.FromHdc(m.WParam);
                DrawPlaceholder(graphics);
            }
        }

        private void DrawPlaceholder(Graphics graphics)
        {
            // Native TextBox metni küçük bir sol kenar boşluğuyla çizer; ipucu aynı konuma hizalanır.
            var bounds = new Rectangle(1, 0, ClientSize.Width - 1, ClientSize.Height);
            TextRenderer.DrawText(graphics, Placeholder, Font, bounds, PlaceholderColor ?? DefaultColor, BackColor,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
