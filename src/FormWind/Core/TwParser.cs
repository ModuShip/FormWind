using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Converts a class string to a variant/style dictionary. A pure function; the cache is in <see cref="TwTheme.Parse"/>.
/// </summary>
internal static class TwParser
{
    private const int MaxComponentDepth = 5;
    private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private static readonly char[] Separators = { ' ', '\t', '\r', '\n' };

    // Renk class'ları: önek-renk[-ton][/saydamlık] ya da önek-[#hex][/saydamlık]. "ring-offset", "ring"den önce denenmeli.
    private const string ColorTargets = "bg|text|border|divide|ring-offset|ring|shadow|from|via|to|fill|accent";
    private static readonly Regex ColorRx = new Regex(@"^(" + ColorTargets + @")-([a-z][a-z0-9]*)(?:-(\d{2,3}))?(?:/(\d{1,3}))?$", Options);
    private static readonly Regex ArbitraryColorRx = new Regex(@"^(" + ColorTargets + @")-\[(#[0-9a-fA-F]{6}|#[0-9a-fA-F]{3})\](?:/(\d{1,3}))?$", Options);
    private static readonly Regex BorderSideRx = new Regex(@"^border-([xytrbl])(?:-(0|2|4|8))?$", Options);
    private static readonly Regex ShadowRx = new Regex(@"^shadow(?:-(sm|md|lg|xl|2xl|none))?$", Options);
    private static readonly Regex RingRx = new Regex(@"^ring(?:-(0|1|2|4|8))?$", Options);
    private static readonly Regex RingOffsetRx = new Regex(@"^ring-offset-(0|1|2|4|8)$", Options);
    private static readonly Regex GradientRx = new Regex(@"^bg-gradient-to-(r|l|t|b|tr|tl|br|bl)$", Options);
    private static readonly Regex DurationRx = new Regex(@"^(duration|delay)-(0|75|100|150|200|300|500|700|1000|\[\d+ms\])$", Options);
    private static readonly Regex FontSizeRx = new Regex(@"^text-(xs|sm|base|lg|xl|2xl|3xl|4xl|\[\d{1,3}(?:\.\d{1,2})?px\])$", Options);
    private static readonly Regex SpacingRx = new Regex(@"^([pm])([xytrbl]?)-(\d+(?:\.5)?|px|\[\d+px\])$", Options);
    private static readonly Regex SizeRx = new Regex(@"^([wh])-(\d+(?:\.5)?|px|\[\d+px\]|full|\d+/\d+)$", Options);
    private static readonly Regex MinMaxRx = new Regex(@"^(min|max)-([wh])-(\d+(?:\.5)?|px|\[\d+px\]|xs|sm|md|lg|xl|2xl|3xl|4xl|5xl|6xl|7xl)$", Options);
    private static readonly Regex GapRx = new Regex(@"^gap(?:-([xy]))?-(\d+(?:\.5)?|px|\[\d+px\])$", Options);
    private static readonly Regex BasisRx = new Regex(@"^basis-(\d+(?:\.5)?|px|\[\d+px\]|full|auto|\d+/\d+)$", Options);
    private static readonly Regex GridColumnsRx = new Regex(@"^grid-cols-(\d{1,2})$", Options);
    private static readonly Regex ColumnSpanRx = new Regex(@"^col-span-(\d{1,2}|full)$", Options);
    private static readonly Regex GridRowsRx = new Regex(@"^grid-rows-(\d{1,2})$", Options);
    private static readonly Regex RowSpanRx = new Regex(@"^row-span-(\d{1,2}|full)$", Options);
    private static readonly Regex RadiusRx = new Regex(@"^rounded(?:-(none|sm|md|lg|xl|2xl|3xl|full|\[\d+px\]))?$", Options);
    private static readonly Regex BorderWidthRx = new Regex(@"^border(?:-(0|2|4|8))?$", Options);

    private static readonly Dictionary<string, float> FontSizes = new Dictionary<string, float>
    {
        ["xs"] = 12, ["sm"] = 14, ["base"] = 16, ["lg"] = 18, ["xl"] = 20, ["2xl"] = 24, ["3xl"] = 30, ["4xl"] = 36,
    };

    private static readonly Dictionary<string, int> Radii = new Dictionary<string, int>
    {
        [""] = 4, ["none"] = 0, ["sm"] = 2, ["md"] = 6, ["lg"] = 8, ["xl"] = 12, ["2xl"] = 16, ["3xl"] = 24, ["full"] = 9999,
    };

    // max-w-md gibi adlandırılmış genişlikler (Tailwind v3, 1 rem = 16 px).
    private static readonly Dictionary<string, int> NamedSizes = new Dictionary<string, int>
    {
        ["xs"] = 320, ["sm"] = 384, ["md"] = 448, ["lg"] = 512, ["xl"] = 576, ["2xl"] = 672,
        ["3xl"] = 768, ["4xl"] = 896, ["5xl"] = 1024, ["6xl"] = 1152, ["7xl"] = 1280,
    };

    /// <summary>Removes extra whitespace; used as the cache key.</summary>
    public static string Normalize(string? classes) =>
        string.IsNullOrWhiteSpace(classes)
            ? ""
            : string.Join(" ", classes!.Split(Separators, StringSplitOptions.RemoveEmptyEntries));

    public static TwParsed Parse(string? classes, TwTheme theme)
    {
        if (theme is null) throw new ArgumentNullException(nameof(theme));

        var variants = new Dictionary<(TwState Conditions, string? Part), TwVariant>();
        var unknown = new List<string>();

        foreach (var token in Expand(classes, theme))
        {
            var variantName = "";
            var utility = token;
            int separator = token.LastIndexOf(':');
            if (separator > 0)
            {
                variantName = token.Substring(0, separator);
                utility = token.Substring(separator + 1);
            }

            if (!TryParseConditions(variantName, out var conditions, out var part))
            {
                unknown.Add(token);
                continue;
            }

            if (!variants.TryGetValue((conditions, part), out var variant))
            {
                variant = new TwVariant(conditions, part, variantName);
                variants.Add((conditions, part), variant);
            }

            if (!TryApplyUtility(utility, variant.Style, theme))
                unknown.Add(token);
        }

        // Önce en yüksek öncelikli durum biti, eşitlikte daha az koşullu varyant: "hover" < "dark:hover" < "active".
        // motion-safe:/motion-reduce: dark: gibi ortam koşuludur, sıraya girmez: forced-colors: yine en son uygulanır.
        var ordered = variants.Values
            .OrderBy(v => HighestBit(v.Conditions & ~TwState.Motion))
            .ThenBy(v => BitCount(v.Conditions))
            .ToList();

        return new TwParsed(Normalize(classes), ordered, unknown);
    }

    // Keyfi değerlerdeki çok uzun sayılar (w-[99999999999px]) int'e sığmaz: class tanınmaz, istisna SetClasses'tan dışarı
    // çıkıp uygulamayı düşürmez.
    private static bool TryApplyUtility(string utility, TwStyle style, TwTheme theme)
    {
        try
        {
            return ApplyUtility(utility, style, theme);
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    // Bileşenler açılır. Her bileşenin açılımı bir kez hesaplanır ve tekrarlayan class'lar ayıklanır (her class'ın son geçtiği
    // yer kalır; sonra gelen kazandığından sonuç değişmez): kendini birden çok kez içeren bileşen ("a": "a a a …") açılımı
    // üstel büyütüp ayrıştırmayı saniyelerce kilitliyordu. Açılmakta olan bir bileşene yeniden başvuru açılmaz (döngü); o
    // ad tanınmayan class olarak kalır.
    private static List<string> Expand(string? classes, TwTheme theme)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(classes)) return result;
        var expanded = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in classes!.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            result.AddRange(ExpandToken(token, theme, expanded, active));
        return result;
    }

    private static List<string> ExpandToken(string token, TwTheme theme, Dictionary<string, List<string>> expanded, HashSet<string> active)
    {
        if (expanded.TryGetValue(token, out var known)) return known;
        var component = active.Count < MaxComponentDepth && !active.Contains(token) ? theme.GetComponent(token) : null;
        if (component is null) return new List<string>(1) { token };

        active.Add(token);
        var tokens = new List<string>();
        foreach (var inner in component.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            tokens.AddRange(ExpandToken(inner, theme, expanded, active));
        active.Remove(token);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = new List<string>(tokens.Count);
        for (int i = tokens.Count - 1; i >= 0; i--)
            if (seen.Add(tokens[i])) distinct.Add(tokens[i]);
        distinct.Reverse();
        expanded[token] = distinct;
        return distinct;
    }

    // Kontrol parçaları. Bir varyantta en fazla bir parça olabilir; parçayı tanımayan kontrol onu yok sayar.
    private static readonly HashSet<string> Parts = new HashSet<string>(StringComparer.Ordinal)
    {
        "header", "cell", "alt", "selected", "row", "row-header", // DataGridView
        "match", "group", "footer",                               // DataGridView: arama eşleşmesi, grup başlığı, alt toplam
        "item", "menu",                                            // ToolStrip / MenuStrip / TwSelect
        "caption",                                                 // Form başlık çubuğu
        "placeholder",                                             // TwTextBox
        "label", "hint", "error",                                  // TwField
    };

    internal static bool TryParseConditions(string name, out TwState conditions, out string? part)
    {
        conditions = TwState.None;
        part = null;
        if (name.Length == 0) return true;

        foreach (var segment in name.Split(':'))
        {
            switch (segment)
            {
                case "dark": conditions |= TwState.Dark; break;
                case "forced-colors": conditions |= TwState.HighContrast; break;
                case "rtl": conditions |= TwState.Rtl; break;
                case "ltr": conditions |= TwState.Ltr; break;
                case "checked": conditions |= TwState.Checked; break;
                case "focus": conditions |= TwState.Focus; break;
                case "focus-visible": conditions |= TwState.FocusVisible; break;
                case "hover": conditions |= TwState.Hover; break;
                case "group-hover": conditions |= TwState.GroupHover; break;
                case "active": conditions |= TwState.Active; break;
                case "disabled": conditions |= TwState.Disabled; break;
                case "invalid": conditions |= TwState.Invalid; break;
                case "sm": conditions |= TwState.Sm; break;
                case "md": conditions |= TwState.Md; break;
                case "lg": conditions |= TwState.Lg; break;
                case "xl": conditions |= TwState.Xl; break;
                case "2xl": conditions |= TwState.Xxl; break;
                case "motion-safe": conditions |= TwState.MotionSafe; break;
                case "motion-reduce": conditions |= TwState.MotionReduce; break;
                default:
                    if (part != null || !Parts.Contains(segment)) return false;
                    part = segment;
                    break;
            }
        }
        return true;
    }

    private static bool ApplyUtility(string utility, TwStyle style, TwTheme theme)
    {
        // Sabit anahtar kelimeler önce: regex zincirine girmeden (parse süresinin büyük kısmı) eşleşir.
        if (ApplyKeyword(utility, style, theme)) return true;

        Match match;

        if ((match = ArbitraryColorRx.Match(utility)).Success)
        {
            if (!TryApplyOpacity(ColorTranslator.FromHtml(match.Groups[2].Value), match.Groups[3], out var arbitrary)) return false;
            SetColor(style, match.Groups[1].Value, arbitrary);
            return true;
        }

        if ((match = ColorRx.Match(utility)).Success
            && theme.TryGetColor(match.Groups[2].Value, match.Groups[3].Value, out var color))
        {
            if (!TryApplyOpacity(color, match.Groups[4], out color)) return false;
            SetColor(style, match.Groups[1].Value, color);
            return true;
        }

        if ((match = BorderSideRx.Match(utility)).Success)
        {
            var width = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
            SetSides(style.BorderSides, match.Groups[1].Value, width);
            return true;
        }

        if ((match = ShadowRx.Match(utility)).Success)
        {
            style.Shadow = match.Groups[1].Value switch
            {
                "sm" => TwShadowSize.Sm,
                "md" => TwShadowSize.Md,
                "lg" => TwShadowSize.Lg,
                "xl" => TwShadowSize.Xl,
                "2xl" => TwShadowSize.Xl2,
                "none" => TwShadowSize.None,
                _ => TwShadowSize.Base,
            };
            return true;
        }

        if ((match = RingRx.Match(utility)).Success)
        {
            style.RingWidth = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 3;
            return true;
        }

        if ((match = RingOffsetRx.Match(utility)).Success)
        {
            style.RingOffset = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            return true;
        }

        if ((match = DurationRx.Match(utility)).Success)
        {
            var value = match.Groups[2].Value;
            int ms = value.StartsWith("[", StringComparison.Ordinal)
                ? int.Parse(value.Substring(1, value.Length - 4), NumberStyles.None, CultureInfo.InvariantCulture)
                : int.Parse(value, CultureInfo.InvariantCulture);
            if (match.Groups[1].Value == "duration") style.DurationMs = ms;
            else style.DelayMs = ms;
            return true;
        }

        if ((match = GradientRx.Match(utility)).Success)
        {
            style.GradientDirection = match.Groups[1].Value switch
            {
                "r" => TwGradientDirection.ToRight,
                "l" => TwGradientDirection.ToLeft,
                "t" => TwGradientDirection.ToTop,
                "b" => TwGradientDirection.ToBottom,
                "tr" => TwGradientDirection.ToTopRight,
                "tl" => TwGradientDirection.ToTopLeft,
                "br" => TwGradientDirection.ToBottomRight,
                _ => TwGradientDirection.ToBottomLeft,
            };
            return true;
        }

        if ((match = FontSizeRx.Match(utility)).Success)
        {
            var size = match.Groups[1].Value;
            // text-[13px]: keyfi boyut (dönüştürme aracı ve tasarım dosyalarındaki ara değerler için).
            var pixels = size[0] == '['
                ? float.Parse(size.Substring(1, size.Length - 4), System.Globalization.CultureInfo.InvariantCulture)
                : FontSizes[size];
            // text-[0px]: sıfır boyutlu font oluşturulamaz; class tanınmaz.
            if (pixels <= 0) return false;
            style.FontSizePx = pixels;
            return true;
        }

        if ((match = SpacingRx.Match(utility)).Success)
        {
            var pixels = ToPixels(match.Groups[3].Value, theme);
            var sides = match.Groups[1].Value == "p" ? style.Padding : style.Margin;
            SetSides(sides, match.Groups[2].Value, pixels);
            return true;
        }

        if ((match = SizeRx.Match(utility)).Success)
        {
            if (!TryParseLength(match.Groups[2].Value, theme, out var length)) return false;
            if (match.Groups[1].Value == "w") style.Width = length;
            else style.Height = length;
            return true;
        }

        if ((match = MinMaxRx.Match(utility)).Success)
        {
            var value = match.Groups[3].Value;
            int pixels = NamedSizes.TryGetValue(value, out var named) ? named : ToPixels(value, theme);
            bool min = match.Groups[1].Value == "min";
            if (match.Groups[2].Value == "w")
            {
                if (min) style.MinWidth = pixels;
                else style.MaxWidth = pixels;
            }
            else
            {
                if (min) style.MinHeight = pixels;
                else style.MaxHeight = pixels;
            }
            return true;
        }

        if ((match = GapRx.Match(utility)).Success)
        {
            var pixels = ToPixels(match.Groups[2].Value, theme);
            var axis = match.Groups[1].Value;
            if (axis != "y") style.GapX = pixels;
            if (axis != "x") style.GapY = pixels;
            return true;
        }

        if ((match = BasisRx.Match(utility)).Success)
        {
            if (!TryParseLength(match.Groups[1].Value, theme, out var basis)) return false;
            style.FlexBasis = basis;
            return true;
        }

        if ((match = GridColumnsRx.Match(utility)).Success)
        {
            var columns = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (columns < 1 || columns > 12) return false;
            style.GridColumns = columns;
            return true;
        }

        if ((match = GridRowsRx.Match(utility)).Success)
        {
            var rows = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (rows < 1 || rows > 12) return false;
            style.GridRows = rows;
            return true;
        }

        if ((match = RowSpanRx.Match(utility)).Success)
        {
            if (match.Groups[1].Value == "full")
            {
                style.RowSpan = int.MaxValue;
                return true;
            }
            var rowSpan = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (rowSpan < 1 || rowSpan > 12) return false;
            style.RowSpan = rowSpan;
            return true;
        }

        if ((match = ColumnSpanRx.Match(utility)).Success)
        {
            if (match.Groups[1].Value == "full")
            {
                style.ColumnSpan = int.MaxValue;
                return true;
            }
            var span = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (span < 1 || span > 12) return false;
            style.ColumnSpan = span;
            return true;
        }

        if ((match = RadiusRx.Match(utility)).Success)
        {
            var key = match.Groups[1].Value;
            style.Radius = key.StartsWith("[", StringComparison.Ordinal) ? ParseBracketPixels(key) : Radii[key];
            return true;
        }

        if ((match = BorderWidthRx.Match(utility)).Success)
        {
            style.BorderWidth = match.Groups[1].Success
                ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
                : 1;
            return true;
        }

        return false;
    }

    private static bool ApplyKeyword(string utility, TwStyle style, TwTheme theme)
    {
        switch (utility)
        {
            case "font-thin": style.FontWeight = 100; return true;
            case "font-extralight": style.FontWeight = 200; return true;
            case "font-light": style.FontWeight = 300; return true;
            case "font-normal": style.FontWeight = 400; return true;
            case "font-medium": style.FontWeight = 500; return true;
            case "font-semibold": style.FontWeight = 600; return true;
            case "font-bold": style.FontWeight = 700; return true;
            case "font-extrabold": style.FontWeight = 800; return true;
            case "font-black": style.FontWeight = 900; return true;
            case "font-sans": style.FontFamily = theme.FontFamilySans; return true;
            case "font-mono": style.FontFamily = theme.FontFamilyMono; return true;
            case "italic": style.Italic = true; return true;
            case "not-italic": style.Italic = false; return true;
            case "underline": style.Underline = true; return true;
            case "no-underline": style.Underline = false; return true;
            case "text-left": style.TextAlign = TwTextAlign.Left; return true;
            case "text-center": style.TextAlign = TwTextAlign.Center; return true;
            case "text-right": style.TextAlign = TwTextAlign.Right; return true;
            case "cursor-pointer": style.Cursor = Cursors.Hand; return true;
            case "cursor-default": style.Cursor = Cursors.Default; return true;
            case "cursor-text": style.Cursor = Cursors.IBeam; return true;
            case "hidden": style.Hidden = true; return true;
            case "visible": style.Hidden = false; return true;

            case "block": style.Display = TwDisplay.Block; return true;
            case "flex": style.Display = TwDisplay.Flex; return true;
            case "grid": style.Display = TwDisplay.Grid; return true;
            case "flex-row": style.FlexDirection = TwFlexDirection.Row; return true;
            case "flex-col": style.FlexDirection = TwFlexDirection.Column; return true;
            case "flex-wrap": style.FlexWrap = true; return true;
            case "flex-nowrap": style.FlexWrap = false; return true;

            case "flex-1": SetFlex(style, 1, 1, TwLength.Pixels(0)); return true;
            case "flex-auto": SetFlex(style, 1, 1, TwLength.Auto); return true;
            case "flex-initial": SetFlex(style, 0, 1, TwLength.Auto); return true;
            case "flex-none": SetFlex(style, 0, 0, TwLength.Auto); return true;
            case "grow": style.FlexGrow = 1; return true;
            case "grow-0": style.FlexGrow = 0; return true;
            case "shrink": style.FlexShrink = 1; return true;
            case "shrink-0": style.FlexShrink = 0; return true;

            case "justify-start": style.JustifyContent = TwJustify.Start; return true;
            case "justify-end": style.JustifyContent = TwJustify.End; return true;
            case "justify-center": style.JustifyContent = TwJustify.Center; return true;
            case "justify-between": style.JustifyContent = TwJustify.Between; return true;
            case "justify-around": style.JustifyContent = TwJustify.Around; return true;
            case "justify-evenly": style.JustifyContent = TwJustify.Evenly; return true;

            case "items-start":
            case "items-baseline": style.AlignItems = TwAlign.Start; return true;
            case "items-end": style.AlignItems = TwAlign.End; return true;
            case "items-center": style.AlignItems = TwAlign.Center; return true;
            case "items-stretch": style.AlignItems = TwAlign.Stretch; return true;

            case "self-start": style.AlignSelf = TwAlign.Start; return true;
            case "self-end": style.AlignSelf = TwAlign.End; return true;
            case "self-center": style.AlignSelf = TwAlign.Center; return true;
            case "self-stretch": style.AlignSelf = TwAlign.Stretch; return true;
            case "self-auto": return true; // varsayılan: konteynerin items-* değeri

            case "group": style.IsGroup = true; return true;
            case "object-cover": style.ObjectFit = TwImageFit.Cover; return true;
            case "object-contain": style.ObjectFit = TwImageFit.Contain; return true;
            case "object-fill": style.ObjectFit = TwImageFit.Fill; return true;
            case "transition":
            case "transition-all": SetTransition(style, TwTransitionProperty.All); return true;
            case "transition-colors": SetTransition(style, TwTransitionProperty.Colors); return true;
            case "transition-shadow": SetTransition(style, TwTransitionProperty.Shadow); return true;
            case "transition-none": style.Transition = false; style.TransitionProperty = TwTransitionProperty.None; return true;
            case "ease-linear": style.Easing = TwEasing.Linear; return true;
            case "ease-in": style.Easing = TwEasing.In; return true;
            case "ease-out": style.Easing = TwEasing.Out; return true;
            case "ease-in-out": style.Easing = TwEasing.InOut; return true;
            case "animate-spin": style.Animation = TwAnimation.Spin; return true;
            case "animate-pulse": style.Animation = TwAnimation.Pulse; return true;
            case "animate-none": style.Animation = TwAnimation.None; return true;

            case "truncate": style.Truncate = true; style.NoWrap = true; return true;
            case "backdrop-mica": style.Backdrop = TwBackdrop.Mica; return true;
            case "backdrop-acrylic": style.Backdrop = TwBackdrop.Acrylic; return true;
            case "backdrop-tabbed": style.Backdrop = TwBackdrop.Tabbed; return true;
            case "backdrop-none": style.Backdrop = TwBackdrop.None; return true;
            // Buzlu cam (TwPanel): arkadaki çizim bulanıklaştırılır; yarı saydam bg-*/NN ile birlikte kullanılır.
            case "backdrop-blur-none": style.BackdropBlur = 0; return true;
            case "backdrop-blur-sm": style.BackdropBlur = 4; return true;
            case "backdrop-blur": style.BackdropBlur = 8; return true;
            case "backdrop-blur-md": style.BackdropBlur = 12; return true;
            case "backdrop-blur-lg": style.BackdropBlur = 16; return true;
            case "backdrop-blur-xl": style.BackdropBlur = 24; return true;
            case "backdrop-blur-2xl": style.BackdropBlur = 40; return true;
            case "backdrop-blur-3xl": style.BackdropBlur = 64; return true;
            case "whitespace-nowrap": style.NoWrap = true; return true;
            case "whitespace-normal": style.NoWrap = false; return true;

            case "divide-x": style.DivideX = true; return true;
            case "divide-y": style.DivideY = true; return true;
            case "divide-x-0": style.DivideX = false; return true;
            case "divide-y-0": style.DivideY = false; return true;
        }

        return false;
    }

    private static void SetTransition(TwStyle style, TwTransitionProperty properties)
    {
        style.Transition = true;
        style.TransitionProperty = properties;
    }

    private static void SetFlex(TwStyle style, float grow, float shrink, TwLength basis)
    {
        style.FlexGrow = grow;
        style.FlexShrink = shrink;
        style.FlexBasis = basis;
    }

    // "full" = 1, "1/2" = 0,5, "auto" = auto, aksi halde piksel.
    private static bool TryParseLength(string value, TwTheme theme, out TwLength length)
    {
        length = TwLength.Auto;
        if (value == "auto") return true;
        if (value == "full")
        {
            length = TwLength.Fraction(1);
            return true;
        }

        int slash = value.IndexOf('/');
        if (slash > 0)
        {
            var numerator = int.Parse(value.Substring(0, slash), CultureInfo.InvariantCulture);
            var denominator = int.Parse(value.Substring(slash + 1), CultureInfo.InvariantCulture);
            if (denominator == 0 || numerator > denominator) return false;
            length = TwLength.Fraction(numerator / (float)denominator);
            return true;
        }

        length = TwLength.Pixels(ToPixels(value, theme));
        return true;
    }

    private static void SetColor(TwStyle style, string target, Color color)
    {
        switch (target)
        {
            case "bg": style.BackColor = color; break;
            case "text": style.ForeColor = color; break;
            case "border": style.BorderColor = color; break;
            case "divide": style.DivideColor = color; break;
            case "ring": style.RingColor = color; break;
            case "ring-offset": style.RingOffsetColor = color; break;
            case "shadow": style.ShadowColor = color; break;
            case "from": style.GradientFrom = color; break;
            case "via": style.GradientVia = color; break;
            case "to": style.GradientTo = color; break;
            case "fill": style.FillColor = color; break;
            case "accent": style.AccentColor = color; break;
        }
    }

    // "/50" → %50 saydamlık. Saydamlık verilmemişse renk olduğu gibi kalır.
    private static bool TryApplyOpacity(Color color, Group opacity, out Color result)
    {
        result = color;
        if (!opacity.Success) return true;
        var percent = int.Parse(opacity.Value, CultureInfo.InvariantCulture);
        if (percent > 100) return false;
        result = Color.FromArgb((int)Math.Round(color.A * percent / 100.0), color.R, color.G, color.B);
        return true;
    }

    private static void SetSides(TwSides sides, string axis, int pixels)
    {
        switch (axis)
        {
            case "": sides.Left = sides.Top = sides.Right = sides.Bottom = pixels; break;
            case "x": sides.Left = sides.Right = pixels; break;
            case "y": sides.Top = sides.Bottom = pixels; break;
            case "l": sides.Left = pixels; break;
            case "t": sides.Top = pixels; break;
            case "r": sides.Right = pixels; break;
            case "b": sides.Bottom = pixels; break;
        }
    }

    private static int ToPixels(string value, TwTheme theme)
    {
        if (value == "px") return 1;
        if (value.StartsWith("[", StringComparison.Ordinal)) return ParseBracketPixels(value);
        var units = double.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return (int)Math.Round(units * theme.SpacingUnit);
    }

    // "[12px]" -> 12
    private static int ParseBracketPixels(string value) =>
        int.Parse(value.Substring(1, value.Length - 4), NumberStyles.None, CultureInfo.InvariantCulture);

    private static int HighestBit(TwState state)
    {
        int value = (int)state, bit = 0;
        while (value != 0)
        {
            value >>= 1;
            bit++;
        }
        return bit;
    }

    private static int BitCount(TwState state)
    {
        int value = (int)state, count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }
}
