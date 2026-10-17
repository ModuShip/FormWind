using System;

namespace FormWind;

/// <summary>
/// State flags used to resolve styles. The values also define the order in which variants
/// apply: a higher bit applies later (and overrides earlier ones).
/// </summary>
[Flags]
public enum TwState
{
    None = 0,
    Dark = 1 << 0,
    Checked = 1 << 1,
    Focus = 1 << 2,

    /// <summary>Focus came from the keyboard (<c>focus-visible:</c>); not applied when clicked with the mouse.</summary>
    FocusVisible = 1 << 3,

    /// <summary>While over a parent with the <c>group</c> class (<c>group-hover:</c>).</summary>
    GroupHover = 1 << 4,

    Hover = 1 << 5,
    Active = 1 << 6,
    Disabled = 1 << 7,

    /// <summary>The control has a validation error (<c>invalid:</c>; see <see cref="TailwindProvider.SetError"/>).</summary>
    Invalid = 1 << 8,

    /// <summary>The form is at least 640 px wide (<c>sm:</c>; see <see cref="TwBreakpoints"/>).</summary>
    Sm = 1 << 9,

    /// <summary>En az 768 px (<c>md:</c>).</summary>
    Md = 1 << 10,

    /// <summary>En az 1024 px (<c>lg:</c>).</summary>
    Lg = 1 << 11,

    /// <summary>En az 1280 px (<c>xl:</c>).</summary>
    Xl = 1 << 12,

    /// <summary>En az 1536 px (<c>2xl:</c>).</summary>
    Xxl = 1 << 13,

    /// <summary>The control's (inherited) <c>RightToLeft</c> is <c>No</c> (<c>ltr:</c>).</summary>
    Ltr = 1 << 14,

    /// <summary>The control's (inherited) <c>RightToLeft</c> is <c>Yes</c> (<c>rtl:</c>).</summary>
    Rtl = 1 << 15,

    /// <summary>
    /// Windows High Contrast mode is on (<c>forced-colors:</c>). Class colors are converted to system colors;
    /// only the colors of <c>forced-colors:</c> variants apply.
    /// </summary>
    HighContrast = 1 << 16,

    /// <summary>All width breakpoints.</summary>
    Breakpoints = Sm | Md | Lg | Xl | Xxl,
}
