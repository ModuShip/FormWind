using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

public enum TwTextAlign
{
    Left,
    Center,
    Right,
}

/// <summary>
/// Style values of a variant or a merged state. A <c>null</c> value means "these classes
/// do not touch this property". Pixel values are at 96 DPI.
/// </summary>
[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public sealed class TwStyle
{
    public Color? BackColor { get; set; }
    public Color? ForeColor { get; set; }
    public Color? BorderColor { get; set; }
    public int? BorderWidth { get; set; }

    /// <summary>Per-side widths (<c>border-b</c>, <c>border-x-2</c>); an undefined side uses <see cref="BorderWidth"/>.</summary>
    public TwSides BorderSides { get; } = new TwSides();

    public TwShadowSize? Shadow { get; set; }
    public Color? ShadowColor { get; set; }
    public int? RingWidth { get; set; }
    public Color? RingColor { get; set; }
    public int? RingOffset { get; set; }
    public Color? RingOffsetColor { get; set; }

    public TwGradientDirection? GradientDirection { get; set; }
    public Color? GradientFrom { get; set; }
    public Color? GradientVia { get; set; }
    public Color? GradientTo { get; set; }

    /// <summary><c>fill-*</c>: fill color of inner surfaces such as a checkbox or a switch track.</summary>
    public Color? FillColor { get; set; }

    /// <summary><c>accent-*</c>: color of a checked CheckBox/Radio/Switch.</summary>
    public Color? AccentColor { get; set; }

    /// <summary><c>object-cover</c> / <c>object-contain</c> / <c>object-fill</c>: image placement in <see cref="TwPictureBox"/>.</summary>
    public TwImageFit? ObjectFit { get; set; }

    /// <summary><c>group</c>: descendants react to this control's hover with <c>group-hover:</c>.</summary>
    public bool? IsGroup { get; set; }

    /// <summary><c>transition*</c>: changes are animated over <see cref="DurationMs"/>; <c>transition-none</c> = false.</summary>
    public bool? Transition { get; set; }

    /// <summary>
    /// What <see cref="Transition"/> animates: <c>transition</c>/<c>transition-all</c> everything, <c>transition-colors</c> the
    /// colors (background, text, border), <c>transition-shadow</c> the shadow and ring.
    /// </summary>
    public TwTransitionProperty? TransitionProperty { get; set; }

    /// <summary><c>duration-*</c> (ms). The Tailwind default is 150.</summary>
    public int? DurationMs { get; set; }

    /// <summary><c>delay-*</c>: wait (ms) before a transition starts.</summary>
    public int? DelayMs { get; set; }

    /// <summary><c>ease-linear</c>, <c>ease-in</c>, <c>ease-out</c> (default), <c>ease-in-out</c>.</summary>
    public TwEasing? Easing { get; set; }

    /// <summary><c>animate-spin</c>, <c>animate-pulse</c>, <c>animate-none</c>; drawn by controls that support it (e.g. <see cref="TwIcon"/>).</summary>
    public TwAnimation? Animation { get; set; }
    public int? Radius { get; set; }
    public float? FontSizePx { get; set; }
    public string? FontFamily { get; set; }
    /// <summary>CSS font weight: 100 (thin) … 400 (normal) … 700 (bold) … 900 (black).</summary>
    public int? FontWeight { get; set; }
    public bool? Italic { get; set; }
    public bool? Underline { get; set; }
    public TwSides Padding { get; } = new TwSides();
    public TwSides Margin { get; } = new TwSides();
    public TwLength? Width { get; set; }
    public TwLength? Height { get; set; }
    public int? MinWidth { get; set; }
    public int? MaxWidth { get; set; }
    public int? MinHeight { get; set; }
    public int? MaxHeight { get; set; }
    public TwTextAlign? TextAlign { get; set; }

    /// <summary><c>whitespace-nowrap</c> = true. Currently used for DataGridView cells (WrapMode).</summary>
    public bool? NoWrap { get; set; }

    /// <summary><c>truncate</c>: text that does not fit is shortened with "…" (Label.AutoEllipsis).</summary>
    public bool? Truncate { get; set; }

    /// <summary>Windows 11 window backdrop (<c>backdrop-mica</c>, <c>backdrop-acrylic</c>, <c>backdrop-tabbed</c>, <c>backdrop-none</c>); Form only.</summary>
    public TwBackdrop? Backdrop { get; set; }

    /// <summary><c>backdrop-blur-*</c>: blur radius (96 DPI pixels) of what is behind a <see cref="TwPanel"/>; 0 = none.</summary>
    public int? BackdropBlur { get; set; }

    public Cursor? Cursor { get; set; }
    public bool? Hidden { get; set; }

    // Yerleşim konteyneri (flex / grid). Yalnızca ITwLayoutHost kontrollerinde (TwPanel) etkilidir.
    public TwDisplay? Display { get; set; }
    public TwFlexDirection? FlexDirection { get; set; }
    public bool? FlexWrap { get; set; }
    public int? GapX { get; set; }
    public int? GapY { get; set; }
    public TwJustify? JustifyContent { get; set; }
    public TwAlign? AlignItems { get; set; }
    public int? GridColumns { get; set; }

    /// <summary>Explicit grid row count (<c>grid-rows-3</c>): the container's height is divided into equal rows.</summary>
    public int? GridRows { get; set; }

    // Yerleşim öğesi: ebeveyn flex / grid konteyneriyse etkilidir.
    public float? FlexGrow { get; set; }
    public float? FlexShrink { get; set; }
    public TwLength? FlexBasis { get; set; }
    public TwAlign? AlignSelf { get; set; }

    /// <summary>Number of columns spanned in the grid. <see cref="int.MaxValue"/> = <c>col-span-full</c>.</summary>
    public int? ColumnSpan { get; set; }

    /// <summary>Number of rows spanned in the grid. <see cref="int.MaxValue"/> = <c>row-span-full</c> (all explicit rows).</summary>
    public int? RowSpan { get; set; }

    // Ayraç çizgileri (divide-x, divide-y, divide-{renk}). Şimdilik DataGridView ızgara çizgilerinde kullanılır.
    public bool? DivideX { get; set; }
    public bool? DivideY { get; set; }
    public Color? DivideColor { get; set; }

    public bool HasFont =>
        FontSizePx.HasValue || FontFamily != null || FontWeight.HasValue || Italic.HasValue || Underline.HasValue;

    public bool HasChrome =>
        (BorderWidth ?? 0) > 0 || (Radius ?? 0) > 0 || HasSideBorder;

    public bool HasSideBorder =>
        (BorderSides.Left ?? 0) > 0 || (BorderSides.Top ?? 0) > 0
        || (BorderSides.Right ?? 0) > 0 || (BorderSides.Bottom ?? 0) > 0;

    /// <summary>Whether there is a shadow or ring painted outside the control by its parent.</summary>
    public bool HasOutset => (Shadow ?? TwShadowSize.None) != TwShadowSize.None || (RingWidth ?? 0) > 0;

    public bool HasGradient => GradientDirection.HasValue && (GradientFrom.HasValue || GradientTo.HasValue);

    /// <summary>
    /// High Contrast mode: converts colors to system colors. The control's own background and text are left to their
    /// original (system) values; selected and hovered parts get the highlight colors, borders and separators the text color.
    /// Shadows and gradients are not drawn.
    /// </summary>
    internal void ApplyForcedColors(string? part, TwState state)
    {
        bool highlighted = part == "selected" || ((part == "item" || part == "row") && (state & TwState.Hover) != 0);
        if (part is null)
        {
            BackColor = null;
            ForeColor = null;
        }
        else if (highlighted)
        {
            // Zemin verilmişse vurgu zemini + vurgu yazısı; yalnızca yazı rengi verilmişse (örn. alt çizgili sekme) vurgu rengi yazıya.
            bool hasBack = BackColor.HasValue;
            ForeColor = hasBack ? SystemColors.HighlightText : ForeColor.HasValue ? SystemColors.Highlight : null;
            BackColor = hasBack ? SystemColors.Highlight : null;
        }
        else if (part == "label" || part == "hint" || part == "error" || part == "placeholder" || part == "item")
        {
            BackColor = null;
            ForeColor = part == "placeholder" ? SystemColors.GrayText : SystemColors.WindowText;
        }
        else
        {
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
        }
        // Kenarlık ve ayırıcılar yalnızca class'larda varsa çizilir; rengi yazı rengidir.
        if (BorderColor.HasValue || BorderWidth.HasValue || HasSideBorder) BorderColor = SystemColors.WindowText;
        if (DivideColor.HasValue || DivideX == true || DivideY == true) DivideColor = SystemColors.WindowText;
        Shadow = null;
        ShadowColor = null;
        RingColor = SystemColors.Highlight;
        RingOffsetColor = SystemColors.Window;
        GradientDirection = null;
        GradientFrom = null;
        GradientVia = null;
        GradientTo = null;
        FillColor = SystemColors.ControlDark;
        AccentColor = SystemColors.Highlight;
        Transition = null;
        TransitionProperty = null;
    }

    /// <summary>Writes the defined values of <paramref name="other"/> over this style.</summary>
    public void MergeFrom(TwStyle other)
    {
        BackColor = other.BackColor ?? BackColor;
        ForeColor = other.ForeColor ?? ForeColor;
        BorderColor = other.BorderColor ?? BorderColor;
        BorderWidth = other.BorderWidth ?? BorderWidth;
        BorderSides.MergeFrom(other.BorderSides);
        Shadow = other.Shadow ?? Shadow;
        ShadowColor = other.ShadowColor ?? ShadowColor;
        RingWidth = other.RingWidth ?? RingWidth;
        RingColor = other.RingColor ?? RingColor;
        RingOffset = other.RingOffset ?? RingOffset;
        RingOffsetColor = other.RingOffsetColor ?? RingOffsetColor;
        GradientDirection = other.GradientDirection ?? GradientDirection;
        GradientFrom = other.GradientFrom ?? GradientFrom;
        GradientVia = other.GradientVia ?? GradientVia;
        GradientTo = other.GradientTo ?? GradientTo;
        FillColor = other.FillColor ?? FillColor;
        AccentColor = other.AccentColor ?? AccentColor;
        ObjectFit = other.ObjectFit ?? ObjectFit;
        IsGroup = other.IsGroup ?? IsGroup;
        Transition = other.Transition ?? Transition;
        TransitionProperty = other.TransitionProperty ?? TransitionProperty;
        DurationMs = other.DurationMs ?? DurationMs;
        DelayMs = other.DelayMs ?? DelayMs;
        Easing = other.Easing ?? Easing;
        Animation = other.Animation ?? Animation;
        Radius = other.Radius ?? Radius;
        FontSizePx = other.FontSizePx ?? FontSizePx;
        FontFamily = other.FontFamily ?? FontFamily;
        FontWeight = other.FontWeight ?? FontWeight;
        Italic = other.Italic ?? Italic;
        Underline = other.Underline ?? Underline;
        Padding.MergeFrom(other.Padding);
        Margin.MergeFrom(other.Margin);
        Width = other.Width ?? Width;
        Height = other.Height ?? Height;
        MinWidth = other.MinWidth ?? MinWidth;
        MaxWidth = other.MaxWidth ?? MaxWidth;
        MinHeight = other.MinHeight ?? MinHeight;
        MaxHeight = other.MaxHeight ?? MaxHeight;
        TextAlign = other.TextAlign ?? TextAlign;
        NoWrap = other.NoWrap ?? NoWrap;
        Truncate = other.Truncate ?? Truncate;
        Backdrop = other.Backdrop ?? Backdrop;
        BackdropBlur = other.BackdropBlur ?? BackdropBlur;
        Cursor = other.Cursor ?? Cursor;
        Hidden = other.Hidden ?? Hidden;

        Display = other.Display ?? Display;
        FlexDirection = other.FlexDirection ?? FlexDirection;
        FlexWrap = other.FlexWrap ?? FlexWrap;
        GapX = other.GapX ?? GapX;
        GapY = other.GapY ?? GapY;
        JustifyContent = other.JustifyContent ?? JustifyContent;
        AlignItems = other.AlignItems ?? AlignItems;
        GridColumns = other.GridColumns ?? GridColumns;
        GridRows = other.GridRows ?? GridRows;

        FlexGrow = other.FlexGrow ?? FlexGrow;
        FlexShrink = other.FlexShrink ?? FlexShrink;
        FlexBasis = other.FlexBasis ?? FlexBasis;
        AlignSelf = other.AlignSelf ?? AlignSelf;
        ColumnSpan = other.ColumnSpan ?? ColumnSpan;
        RowSpan = other.RowSpan ?? RowSpan;

        DivideX = other.DivideX ?? DivideX;
        DivideY = other.DivideY ?? DivideY;
        DivideColor = other.DivideColor ?? DivideColor;
    }
}

/// <summary>Windows 11 sistem zemini (DWMWA_SYSTEMBACKDROP_TYPE).</summary>
public enum TwBackdrop
{
    /// <summary>System default.</summary>
    Auto = 0,
    None = 1,
    Mica = 2,
    Acrylic = 3,
    /// <summary>Mica Alt for tabbed windows.</summary>
    Tabbed = 4,
}
