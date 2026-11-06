using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A two-line title: <see cref="Control.Text"/> on the first line and a muted <see cref="Subtitle"/> under it
/// ("Kayıt Arşivi" / "Tümü · 740 kayıt"). Both lines end with an ellipsis when the control is narrower than its text, so it can
/// be the title of a <see cref="TwToolbar"/> with <see cref="TwToolbarOverflow.Shrink"/>. The title is styled with the
/// control's own classes (<c>text-[15px] font-semibold</c>), the subtitle with the <c>hint:</c> part
/// (<c>hint:text-xs hint:text-surface-500</c>). <see cref="TwPageHeader"/> and <see cref="TwCard"/> use it for their titles.
/// </summary>
[ToolboxItem(true)]
[Description("Two-line title: a title and a muted subtitle, both ending with an ellipsis.")]
[DefaultProperty(nameof(Text))]
public class TwHeading : Control
{
    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

    private string _subtitle = "";
    private Color _subtitleColor = Color.Empty;
    private Font? _subtitleFont;
    private Font? _derivedFont;
    private Font? _derivedFrom;
    private int _lineGap = 2;

    public TwHeading()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = false;
        AutoSize = true;
    }

    /// <summary>Muted second line; empty = a one-line title.</summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string Subtitle
    {
        get => _subtitle;
        set
        {
            value ??= "";
            if (_subtitle == value) return;
            _subtitle = value;
            ContentChanged();
        }
    }

    /// <summary>Color of the subtitle; empty = the text color blended toward the background (set with <c>hint:text-*</c>).</summary>
    [Category("Appearance")]
    public Color SubtitleColor
    {
        get => _subtitleColor;
        set
        {
            _subtitleColor = value;
            Invalidate();
        }
    }

    private bool ShouldSerializeSubtitleColor() => !_subtitleColor.IsEmpty;

    private void ResetSubtitleColor() => SubtitleColor = Color.Empty;

    /// <summary>Font of the subtitle; <c>null</c> = the control's font, regular and slightly smaller (set with <c>hint:text-*</c>).</summary>
    [Category("Appearance"), DefaultValue(null)]
    public Font? SubtitleFont
    {
        get => _subtitleFont;
        set
        {
            _subtitleFont = value;
            ContentChanged();
        }
    }

    /// <summary>Space between the two lines (96 DPI pixels); default 2.</summary>
    [Category("Layout"), DefaultValue(2)]
    public int LineGap
    {
        get => _lineGap;
        set
        {
            value = Math.Max(0, value);
            if (_lineGap == value) return;
            _lineGap = value;
            ContentChanged();
        }
    }

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(true)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set
        {
            base.AutoSize = value;
            if (value) FitToContent();
        }
    }

    /// <summary>The font the subtitle is drawn with.</summary>
    [Browsable(false)]
    public Font EffectiveSubtitleFont
    {
        get
        {
            if (_subtitleFont != null) return _subtitleFont;
            if (_derivedFont is null || !ReferenceEquals(_derivedFrom, Font))
            {
                _derivedFrom = Font;
                _derivedFont = TwFonts.Get(Font.FontFamily.Name, Math.Max(6f, Font.SizeInPoints * 0.86f), FontStyle.Regular);
            }
            return _derivedFont;
        }
    }

    /// <summary>The color the subtitle is drawn with.</summary>
    [Browsable(false)]
    public Color EffectiveSubtitleColor =>
        !_subtitleColor.IsEmpty ? _subtitleColor : TwCharts.Blend(ForeColor, TwCharts.Surface(this), 0.4f);

    private bool HasSubtitle => _subtitle.Length > 0;

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    private Size TitleSize => TextRenderer.MeasureText(Text.Length > 0 ? Text : " ", Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis);

    private Size SubtitleSize => HasSubtitle ? TextRenderer.MeasureText(_subtitle, EffectiveSubtitleFont, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis) : Size.Empty;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var title = TitleSize;
        var subtitle = SubtitleSize;
        int width = Math.Max(title.Width, subtitle.Width) + Padding.Horizontal;
        int height = title.Height + (HasSubtitle ? Scale(_lineGap) + subtitle.Height : 0) + Padding.Vertical;
        return new Size(width, height);
    }

    private void ContentChanged()
    {
        if (AutoSize) FitToContent();
        Parent?.PerformLayout(this, nameof(Bounds));
        Invalidate();
    }

    private void FitToContent()
    {
        // Yerleştiren ebeveyn (araç çubuğu, sayfa başlığı) boyutu kendisi verir; burada yalnızca bağımsız kullanım için.
        if (Parent is TwToolbar or TwPageHeader) return;
        Size = GetPreferredSize(Size.Empty);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        ContentChanged();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ContentChanged();
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        ContentChanged();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var flags = Flags;
        bool rtl = TwRtl.NeedsMirroring(this);
        if (rtl) flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
        var content = new Rectangle(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
        var title = TitleSize;
        var subtitle = SubtitleSize;
        int total = title.Height + (HasSubtitle ? Scale(_lineGap) + subtitle.Height : 0);
        int top = content.Top + Math.Max(0, (content.Height - total) / 2);
        var color = Enabled ? ForeColor : TwCharts.Blend(ForeColor, TwCharts.Surface(this), 0.5f);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(content.Left, top, content.Width, title.Height), color, flags);
        if (HasSubtitle)
        {
            var bounds = new Rectangle(content.Left, top + title.Height + Scale(_lineGap), content.Width, subtitle.Height);
            TextRenderer.DrawText(e.Graphics, _subtitle, EffectiveSubtitleFont, bounds, EffectiveSubtitleColor, flags);
        }
    }

    internal void SetPartStyles(Color? subtitle, Font? subtitleFont)
    {
        _subtitleColor = subtitle ?? Color.Empty;
        _subtitleFont = subtitleFont;
        ContentChanged();
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new HeadingAccessibleObject(this);

    private sealed class HeadingAccessibleObject : ControlAccessibleObject
    {
        private readonly TwHeading _owner;

        public HeadingAccessibleObject(TwHeading owner) : base(owner) => _owner = owner;

        public override AccessibleRole Role => _owner.AccessibleRole == AccessibleRole.Default ? AccessibleRole.StaticText : _owner.AccessibleRole;

        public override string? Name => _owner.AccessibleName ?? _owner.Text;

        public override string? Description => _owner.AccessibleDescription ?? (_owner._subtitle.Length > 0 ? _owner._subtitle : null);
    }
}
