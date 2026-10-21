using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FormWind;

/// <summary>
/// Color palette, component classes and font settings. Parse results are cached per theme;
/// the cache is cleared when the theme changes.
/// </summary>
public sealed class TwTheme
{
    // İlk statik alan: GDI+ başlamadan WinForms DPI ayarı uygulanır (bkz. TwDpi).
    private static readonly bool DpiReady = TwDpi.EnsureInitialized();

    private static readonly Regex ColorNameRx = new Regex("^[a-z][a-z0-9]*$", RegexOptions.CultureInvariant);

    // Paletler bir kez çözülür (v4'ün OKLCH dönüşümü tema başına tekrarlanmasın). Varsayılan tema (Current) bunları
    // kullandığından ondan önce başlatılmalı: statik alanlar yazım sırasıyla başlar.
    private static readonly Lazy<Dictionary<string, Color[]>> V3Colors = new Lazy<Dictionary<string, Color[]>>(() => Resolve(TwPalette.Colors));
    private static readonly Lazy<Dictionary<string, Color[]>> V4Colors = new Lazy<Dictionary<string, Color[]>>(() => Resolve(TwPaletteV4.Colors));

    private readonly Dictionary<string, Dictionary<string, Color>> _colors =
        new Dictionary<string, Dictionary<string, Color>>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _components = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TwParsed> _cache =
        new ConcurrentDictionary<string, TwParsed>(StringComparer.Ordinal);

    private readonly Dictionary<string, SortedDictionary<int, string>> _fontWeights =
        new Dictionary<string, SortedDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, char> _icons = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TwSvgIcon> _svgIcons = new Dictionary<string, TwSvgIcon>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _autoStyle = new Dictionary<string, string>(StringComparer.Ordinal);

    // Uygulamanın (tema dosyası ya da SetColor ile) kendisi tanımladığı anlamsal renkler; palet değişince yeniden kopyalanmaz.
    private readonly HashSet<string> _ownSemanticColors = new HashSet<string>(StringComparer.Ordinal);

    private string _fontFamilySans = "Segoe UI";
    private string _fontFamilyMono = "Consolas";
    private int _spacingUnit = 4;

    public TwTheme()
        : this(TwPaletteVersion.V3)
    {
    }

    /// <summary>Creates a theme with the default palette of the given Tailwind version.</summary>
    public TwTheme(TwPaletteVersion palette)
    {
        LoadPalette(palette);
        SetColorCore("white", "", Color.White);
        SetColorCore("black", "", Color.Black);
        SetColorCore("transparent", "", Color.Transparent);

        foreach (var pair in DefaultComponents)
            _components[pair.Key] = pair.Value;
        foreach (var pair in DefaultAutoStyle)
            _autoStyle[pair.Key] = pair.Value;

        // Windows'ta Segoe UI ağırlıkları ayrı aileler olarak kurulur. 400 ve 700 ailenin kendisidir.
        SetFontWeight("Segoe UI", 300, "Segoe UI Light");
        SetFontWeight("Segoe UI", 350, "Segoe UI Semilight");
        SetFontWeight("Segoe UI", 600, "Segoe UI Semibold");
        SetFontWeight("Segoe UI", 900, "Segoe UI Black");
    }

    /// <summary>
    /// Creates a theme from a ready-made look (<see cref="TwPreset"/>): its colors, fonts and component classes on top of the
    /// defaults. A theme file can still override any of them.
    /// </summary>
    public TwTheme(TwPreset preset)
        : this(TwPaletteVersion.V3)
    {
        UsePresetCore(preset);
    }

    /// <summary>The ready-made look the theme starts from (<c>"preset"</c> in the theme file); <see cref="TwPreset.Tailwind"/> by default.</summary>
    public TwPreset Preset { get; private set; }

    private void UsePresetCore(TwPreset preset)
    {
        Preset = preset;
        if (preset == TwPreset.Tailwind) return;
        ApplyJson(TwPresets.Json(preset), null, skipMissingFiles: false);
    }

    /// <summary>Font the icons are drawn with. Default: Segoe Fluent Icons (Win11), otherwise Segoe MDL2 Assets (Win10).</summary>
    public string IconFontFamily { get; set; } = TwIcons.DefaultFontFamily;

    /// <summary>Defines the installed family for a specific weight of a font family (e.g. 600 → "Inter SemiBold").</summary>
    public void SetFontWeight(string family, int weight, string face)
    {
        if (string.IsNullOrWhiteSpace(family)) throw new ArgumentException("The family name cannot be empty.", nameof(family));
        if (weight < 1 || weight > 1000) throw new ArgumentOutOfRangeException(nameof(weight));
        if (!_fontWeights.TryGetValue(family, out var weights))
        {
            weights = new SortedDictionary<int, string>();
            _fontWeights.Add(family, weights);
        }
        weights[weight] = face ?? throw new ArgumentNullException(nameof(face));
    }

    /// <summary>
    /// Chooses a weight from the installed weight families with CSS's matching rule. Returns the family to use and
    /// whether a bold style is needed (when it falls back to the family itself, as for 400/700).
    /// </summary>
    public (string Face, bool Bold) ResolveFontWeight(string family, int weight) =>
        ResolveFontWeight(family, weight, TwFonts.IsInstalled);

    internal (string Face, bool Bold) ResolveFontWeight(string family, int weight, Func<string, bool> isInstalled)
    {
        var available = new SortedDictionary<int, (string Face, bool Bold)>
        {
            [400] = (family, false),
            [700] = (family, true),
        };
        if (_fontWeights.TryGetValue(family, out var weights))
        {
            foreach (var pair in weights)
                if (isInstalled(pair.Value)) available[pair.Key] = (pair.Value, false);
        }

        if (available.TryGetValue(weight, out var exact)) return exact;

        var keys = new List<int>(available.Keys);
        int? Find(Func<int, bool> predicate, bool ascending)
        {
            if (ascending)
            {
                foreach (var key in keys) if (predicate(key)) return key;
            }
            else
            {
                for (int i = keys.Count - 1; i >= 0; i--) if (predicate(keys[i])) return keys[i];
            }
            return null;
        }

        int? chosen = weight >= 400 && weight <= 500
            ? Find(k => k > weight && k <= 500, true) ?? Find(k => k < weight, false) ?? Find(k => k > 500, true)
            : weight < 400
                ? Find(k => k < weight, false) ?? Find(k => k > weight, true)
                : Find(k => k > weight, true) ?? Find(k => k < weight, false);

        return available[chosen ?? 400];
    }

    /// <summary>Adds a theme-level icon or overrides a built-in one (e.g. for a custom icon font).</summary>
    public void SetIcon(string name, char glyph)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The icon name cannot be empty.", nameof(name));
        _icons[name] = glyph;
    }

    /// <summary>
    /// Adds an SVG icon or overrides the glyph icon with the same name (SVG wins). The file content of sets such as
    /// Lucide/Heroicons can be passed directly; the color is the text color where it is drawn.
    /// </summary>
    public void SetSvgIcon(string name, string svg)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The icon name cannot be empty.", nameof(name));
        _svgIcons[name] = TwSvgIcon.Parse(svg);
    }

    internal bool TryGetSvgIcon(string? name, out TwSvgIcon? icon)
    {
        icon = null;
        return !string.IsNullOrWhiteSpace(name) && _svgIcons.TryGetValue(name!, out icon);
    }

    /// <summary>Whether the name is an SVG or glyph icon.</summary>
    public bool HasIcon(string? name) => TryGetSvgIcon(name, out _) || TryGetIcon(name, out _);

    /// <summary>Converts an icon name to a glyph: theme icons first, then <see cref="TwIcons"/>; the <c>#E710</c> form is accepted too.</summary>
    public bool TryGetIcon(string? name, out char glyph)
    {
        glyph = '\0';
        if (string.IsNullOrWhiteSpace(name)) return false;
        return _icons.TryGetValue(name!, out glyph) || TwIcons.TryGetGlyph(name!, out glyph);
    }

    /// <summary>The theme used by all <see cref="TailwindProvider"/>s without a theme of their own.</summary>
    public static TwTheme Current
    {
        get => _current;
        set
        {
            _current = value ?? throw new ArgumentNullException(nameof(value));
            CurrentChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? CurrentChanged;

    /// <summary>
    /// Default component classes built into the code. A theme file adds to and overrides them; the Designer uses them when
    /// it cannot find the project's theme file. Every <see cref="TwPreset"/> defines the same names, so switching presets
    /// restyles an application without changing its classes. The colors are the semantic ones (<see cref="SemanticColors"/>).
    /// </summary>
    public static IReadOnlyDictionary<string, string> DefaultComponents { get; } = new Dictionary<string, string>
    {
        // Butonlar. btn-ghost kenarlıklı varsayılan buton, btn-secondary dolgulu nötr buton.
        ["btn"] = "px-4 py-2 rounded-md text-sm font-semibold cursor-pointer transition",
        // Koyu temada da primary-600/700: beyaz yazı blue-500 üzerinde 3,7:1, blue-400 üzerinde 2,5:1 ile WCAG AA'yı geçmez.
        ["btn-primary"] = "btn bg-primary-600 text-white hover:bg-primary-700 active:bg-primary-800 disabled:bg-surface-300 disabled:text-surface-500 dark:bg-primary-600 dark:hover:bg-primary-700 dark:disabled:bg-surface-700 dark:disabled:text-surface-400",
        ["btn-secondary"] = "btn bg-surface-100 text-surface-900 hover:bg-surface-200 active:bg-surface-300 disabled:bg-surface-50 disabled:text-surface-500 dark:bg-surface-700 dark:text-surface-100 dark:hover:bg-surface-600 dark:active:bg-surface-600 dark:disabled:bg-surface-800 dark:disabled:text-surface-400",
        ["btn-ghost"] = "btn bg-white text-surface-700 border border-surface-300 hover:bg-surface-100 active:bg-surface-200 dark:bg-surface-800 dark:text-surface-200 dark:border-surface-600 dark:hover:bg-surface-700 disabled:bg-surface-50 disabled:text-surface-500 disabled:border-surface-200 dark:disabled:bg-surface-800 dark:disabled:text-surface-500 dark:disabled:border-surface-700",
        ["btn-danger"] = "btn bg-danger-600 text-white hover:bg-danger-700 active:bg-danger-800 disabled:bg-surface-300 disabled:text-surface-500 dark:disabled:bg-surface-700 dark:disabled:text-surface-400",
        ["link"] = "text-primary-600 underline cursor-pointer hover:text-primary-800 dark:text-primary-400 dark:hover:text-primary-300",
        // LinkLabel: yazı text-*, bağlantılar accent-*.
        ["link-label"] = "text-surface-700 accent-primary-600 dark:text-surface-200 dark:accent-primary-400",

        // Form alanları
        ["input"] = "bg-white text-surface-900 border border-surface-300 rounded-md px-3 py-2 placeholder:text-surface-400 dark:placeholder:text-surface-500 focus:border-primary-500 focus:ring-2 focus:ring-primary-500/30 dark:bg-surface-900 dark:text-surface-100 dark:border-surface-600 dark:focus:border-primary-400 invalid:border-danger-500 invalid:ring-2 invalid:ring-danger-500/20 dark:invalid:border-danger-400",
        ["select"] = "input menu:bg-white menu:border-surface-200 item:hover:bg-surface-100 selected:bg-primary-600 selected:text-white dark:menu:bg-surface-800 dark:menu:border-surface-700 dark:item:hover:bg-surface-700",
        ["field"] = "label:text-sm label:font-medium label:text-surface-700 hint:text-xs hint:text-surface-500 error:text-xs error:text-danger-600 " +
                    "dark:label:text-surface-200 dark:hint:text-surface-400 dark:error:text-danger-400",
        ["toggle"] = "text-surface-700 accent-primary-600 dark:text-surface-200 dark:fill-surface-700 dark:border-surface-600",
        ["range"] = "fill-surface-200 accent-primary-600 dark:fill-surface-700 dark:accent-primary-500 dark:item:bg-surface-100",

        // Yüzeyler ve yazı
        ["page"] = "font-sans bg-surface-50 text-surface-900 dark:bg-surface-900 dark:text-surface-100",
        ["card"] = "bg-white border border-surface-200 rounded-lg p-4 shadow-sm dark:bg-surface-800 dark:border-surface-700",
        ["sidebar"] = "bg-white border-r border-surface-200 dark:bg-surface-800 dark:border-surface-700",
        ["nav-item"] = "px-3 py-2 rounded-md text-sm font-medium text-left cursor-pointer bg-white text-surface-700 hover:bg-surface-100 checked:bg-primary-50 checked:text-primary-700 " +
                       "dark:bg-surface-800 dark:text-surface-200 dark:hover:bg-surface-700 dark:checked:bg-primary-950 dark:checked:text-primary-200",
        ["title"] = "text-2xl font-semibold text-surface-900 dark:text-white",
        ["muted"] = "text-sm text-surface-500 dark:text-surface-400",
        ["badge"] = "text-xs font-semibold rounded-full px-2 text-center bg-primary-100 text-primary-800 dark:bg-primary-900 dark:text-primary-100",
        ["alert-info"] = "p-3 rounded-md border text-sm bg-primary-50 border-primary-200 text-primary-800 dark:bg-primary-950 dark:border-primary-800 dark:text-primary-100",
        ["alert-success"] = "p-3 rounded-md border text-sm bg-success-50 border-success-200 text-success-800 dark:bg-success-950 dark:border-success-800 dark:text-success-100",
        ["alert-warning"] = "p-3 rounded-md border text-sm bg-warning-50 border-warning-200 text-warning-800 dark:bg-warning-950 dark:border-warning-800 dark:text-warning-100",
        ["alert-danger"] = "p-3 rounded-md border text-sm bg-danger-50 border-danger-200 text-danger-800 dark:bg-danger-950 dark:border-danger-800 dark:text-danger-100",

        // Veri ve gezinme
        ["table"] = "bg-white text-sm text-surface-700 border border-surface-200 divide-y divide-surface-200 " +
                    "header:bg-surface-50 header:text-surface-500 header:text-xs header:font-bold header:h-10 header:px-3 header:whitespace-nowrap " +
                    "cell:px-3 row:h-10 row:hover:bg-surface-50 selected:bg-primary-50 selected:text-primary-900 row-header:hidden " +
                    "dark:bg-surface-800 dark:text-surface-200 dark:border-surface-700 dark:divide-surface-700 " +
                    "dark:header:bg-surface-900 dark:header:text-surface-400 dark:row:hover:bg-surface-700 " +
                    "dark:selected:bg-primary-900 dark:selected:text-primary-100 " +
                    "group:bg-surface-50 group:text-surface-700 group:font-semibold footer:bg-surface-50 footer:text-surface-900 footer:font-semibold " +
                    "match:bg-warning-200 dark:group:bg-surface-900 dark:group:text-surface-200 dark:footer:bg-surface-900 dark:footer:text-surface-100 " +
                    "dark:match:bg-warning-800 dark:match:text-white",
        // Grafikler (TwChart, TwDonutChart, TwRingChart, TwHeatmap, TwTimeline, TwSparkline), TwStepper ve KPI kartı (TwStat).
        ["chart"] = "text-sm text-surface-900 divide-surface-200 label:text-surface-500 " +
                    "dark:text-surface-100 dark:divide-surface-700 dark:label:text-surface-400",
        ["stat"] = "bg-white border border-surface-200 rounded-lg p-4 text-sm text-surface-900 label:text-surface-500 " +
                   "dark:bg-surface-800 dark:border-surface-700 dark:text-surface-100 dark:label:text-surface-400",
        // Tablo varyantları: "table table-sm" gibi table'dan sonra yazılır.
        ["table-sm"] = "text-xs header:h-8 header:px-2 cell:px-2 row:h-8",
        ["table-lg"] = "header:h-12 header:px-4 cell:px-4 row:h-12",
        ["table-striped"] = "alt:bg-surface-50 dark:alt:bg-surface-900",
        ["table-bordered"] = "divide-x divide-y",
        ["list"] = "p-1 bg-white text-surface-900 item:hover:bg-surface-100 selected:bg-primary-50 selected:text-primary-900 row:border-surface-100 fill-surface-300 " +
                   "dark:bg-surface-800 dark:text-surface-100 dark:item:hover:bg-surface-700 dark:selected:bg-primary-950 dark:selected:text-primary-100 dark:row:border-surface-700 dark:fill-surface-600",
        // Standart TabControl ve sayfaları (TwTabStrip için "tabs").
        ["tab-control"] = "bg-surface-100 text-surface-600 selected:bg-white selected:text-surface-900 selected:border-primary-600 " +
                          "dark:bg-surface-800 dark:text-surface-300 dark:selected:bg-surface-900 dark:selected:text-white dark:selected:border-primary-400",
        ["tab-page"] = "bg-white text-surface-900 dark:bg-surface-900 dark:text-surface-100",
        ["tabs"] = "text-surface-500 border-b border-surface-200 selected:text-surface-900 selected:border-primary-600 item:hover:text-surface-700 " +
                   "dark:text-surface-400 dark:border-surface-700 dark:selected:text-white dark:selected:border-primary-400",
        ["menu"] = "bg-white text-surface-700 item:hover:bg-surface-100 dark:bg-surface-800 dark:text-surface-100 dark:item:hover:bg-surface-700",
        // PropertyGrid: header: kategori satırları, selected: seçili özellik, divide-* iç çizgiler.
        ["properties"] = "bg-white text-surface-900 divide-surface-200 header:bg-surface-100 header:text-surface-700 selected:bg-primary-600 selected:text-white " +
                         "dark:bg-surface-800 dark:text-surface-100 dark:divide-surface-700 dark:header:bg-surface-900 dark:header:text-surface-300",
    };

    /// <summary>
    /// Semantic colors and the default palette color each one copies (<c>primary</c> → <c>blue</c>, <c>surface</c> → <c>gray</c>
    /// …). The components use them, so a preset or a theme file restyles every component by defining these ramps (for example
    /// <c>"primary": { "50": …, "950": … }</c>). Until the application defines one, it follows the palette version.
    /// </summary>
    public static IReadOnlyDictionary<string, string> SemanticColors { get; } = new Dictionary<string, string>
    {
        ["primary"] = "blue",
        ["surface"] = "gray",
        ["danger"] = "red",
        ["success"] = "green",
        ["warning"] = "amber",
    };

    /// <summary>
    /// Components given to controls without classes when <see cref="TailwindProvider.AutoStyle"/> is on, by control type name.
    /// The type and then its base types are looked up, so <c>TextBoxBase</c> also covers <c>TextBox</c> and <c>RichTextBox</c>;
    /// an empty value leaves that type unstyled. Labels and panels are not listed: they take the form's colors. A LinkLabel gets
    /// <c>link-label</c>: its links are drawn with <c>accent-*</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> DefaultAutoStyle { get; } = new Dictionary<string, string>
    {
        ["Form"] = "page",
        ["Button"] = "btn-ghost",
        ["TextBoxBase"] = "input",
        ["TwTextBox"] = "input",
        ["UpDownBase"] = "input",
        ["TwDatePicker"] = "select",
        ["DateTimePicker"] = "select",
        ["TwSelect"] = "select",
        ["ComboBox"] = "select",
        ["CheckBox"] = "toggle",
        ["RadioButton"] = "toggle",
        ["TwSlider"] = "range",
        ["TwProgress"] = "range",
        ["TwField"] = "field",
        ["DataGridView"] = "table",
        ["TwChart"] = "chart",
        ["TwDonutChart"] = "chart",
        ["TwSparkline"] = "chart",
        ["TwRingChart"] = "chart",
        ["TwHeatmap"] = "chart",
        ["TwTimeline"] = "chart",
        ["TwStepper"] = "chart",
        ["TwStat"] = "stat",
        ["TwDataList"] = "list",
        ["ListBox"] = "list",
        ["TreeView"] = "list",
        ["ListView"] = "list",
        ["TwTabStrip"] = "tabs",
        ["ToolStrip"] = "menu",
        ["PropertyGrid"] = "properties",
        ["TabControl"] = "tab-control",
        ["TabPage"] = "tab-page",
        ["ProgressBar"] = "range",
        ["LinkLabel"] = "link-label",
    };

    // DefaultComponents'tan sonra bildirilmeli: statik başlatıcılar yazım sırasıyla çalışır.
    private static TwTheme _current = new TwTheme();

    // .NET Framework'te bağlama yönlendirmesi olmayan bir sürece (KeePass eklentisi) yüklenen FormWind'in bağımlılıkları.
    static TwTheme() => TwDependencyResolver.EnsureInstalled();

    public string FontFamilySans
    {
        get => _fontFamilySans;
        set { _fontFamilySans = value ?? throw new ArgumentNullException(nameof(value)); _cache.Clear(); }
    }

    public string FontFamilyMono
    {
        get => _fontFamilyMono;
        set { _fontFamilyMono = value ?? throw new ArgumentNullException(nameof(value)); _cache.Clear(); }
    }

    /// <summary>Pixel value of one unit in spacing and size classes (Tailwind: 4).</summary>
    public int SpacingUnit
    {
        get => _spacingUnit;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "SpacingUnit must be positive.");
            _spacingUnit = value;
            _cache.Clear();
        }
    }

    /// <summary>Color drawn when <c>border</c> is used without a color (Tailwind: gray-200).</summary>
    public Color DefaultBorderColor { get; set; } = ColorTranslator.FromHtml("#e5e7eb");

    public IReadOnlyDictionary<string, string> Components => _components;

    /// <summary>Classes for <see cref="TailwindProvider.AutoStyle"/> by control type name (see <see cref="DefaultAutoStyle"/>).</summary>
    public IReadOnlyDictionary<string, string> AutoStyle => _autoStyle;

    /// <summary>Sets the automatic classes of a control type (simple type name, e.g. <c>KryptonButton</c>); empty leaves it unstyled.</summary>
    public void SetAutoStyle(string typeName, string classes)
    {
        if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException("The type name cannot be empty.", nameof(typeName));
        _autoStyle[typeName] = classes ?? "";
    }

    public bool RemoveAutoStyle(string typeName) => _autoStyle.Remove(typeName);

    /// <summary>
    /// The automatic classes of a control type: the entry of the type or of its nearest base type; <c>null</c> when there is
    /// none or it is empty.
    /// </summary>
    public string? GetAutoStyle(Type controlType)
    {
        if (controlType is null) throw new ArgumentNullException(nameof(controlType));
        for (var type = controlType; type != null; type = type.BaseType)
        {
            if (_autoStyle.TryGetValue(type.Name, out var classes))
                return string.IsNullOrWhiteSpace(classes) ? null : classes;
        }
        return null;
    }

    public IEnumerable<string> ColorNames => _colors.Keys;

    /// <summary>
    /// Color keys as written in classes (<c>brand-500</c>, <c>white</c>): the theme's own colors first, then the default
    /// palette in its order.
    /// </summary>
    internal IEnumerable<string> ColorKeys()
    {
        IEnumerable<string> Keys(string name) => _colors[name].Keys.Select(shade => shade.Length == 0 ? name : name + "-" + shade);
        var custom = _colors.Keys.Where(n => !TwPalette.Colors.ContainsKey(n));
        var palette = _colors.Keys.Where(n => TwPalette.Colors.ContainsKey(n));
        return custom.Concat(palette).SelectMany(Keys);
    }

    public void SetColor(string name, Color color) => SetColor(name, "", color);

    /// <summary>Version of the default palette (<see cref="UsePalette"/> or <c>"palette"</c> in the theme file).</summary>
    public TwPaletteVersion Palette { get; private set; }

    /// <summary>
    /// Replaces the 22 colors of the default palette (red … rose, slate … stone) with those of the given version. Custom
    /// colors (such as <c>brand</c>) are kept; default colors overridden under the same name return to the version's values.
    /// </summary>
    public void UsePalette(TwPaletteVersion palette)
    {
        LoadPalette(palette);
        _cache.Clear();
    }

    private void LoadPalette(TwPaletteVersion palette)
    {
        Palette = palette;
        var colors = palette == TwPaletteVersion.V4 ? V4Colors.Value : V3Colors.Value;
        foreach (var pair in colors)
        {
            for (int i = 0; i < TwPalette.Shades.Length; i++)
                SetColorCore(pair.Key, TwPalette.Shades[i], pair.Value[i]);
        }

        foreach (var pair in SemanticColors)
        {
            if (_ownSemanticColors.Contains(pair.Key)) continue;
            _colors.Remove(pair.Key);
            var source = colors[pair.Value];
            for (int i = 0; i < TwPalette.Shades.Length; i++)
                SetColorCore(pair.Key, TwPalette.Shades[i], source[i]);
        }
    }

    private static Dictionary<string, Color[]> Resolve(IReadOnlyDictionary<string, string[]> source)
    {
        var colors = new Dictionary<string, Color[]>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            var shades = new Color[pair.Value.Length];
            for (int i = 0; i < shades.Length; i++) shades[i] = ParseColor(pair.Value[i], pair.Key + "-" + TwPalette.Shades[i]);
            colors[pair.Key] = shades;
        }
        return colors;
    }

    /// <summary>Adds or overrides a palette color. When <paramref name="shade"/> is empty the color is used without a shade (<c>bg-brand</c>).</summary>
    public void SetColor(string name, string shade, Color color)
    {
        if (name is null || !ColorNameRx.IsMatch(name))
            throw new ArgumentException("A color name must start with a lowercase letter and contain only a-z and 0-9.", nameof(name));
        // Uygulamanın tanımladığı anlamsal renk artık paletle değişmez (tanımlamadığı tonlar paletten kopyalandığı gibi kalır).
        if (SemanticColors.ContainsKey(name)) _ownSemanticColors.Add(name);
        SetColorCore(name, shade ?? "", color);
        _cache.Clear();
    }

    public bool TryGetColor(string name, string shade, out Color color)
    {
        color = Color.Empty;
        return _colors.TryGetValue(name, out var shades) && shades.TryGetValue(shade ?? "", out color);
    }

    public void SetComponent(string name, string classes)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The component name cannot be empty.", nameof(name));
        _components[name] = classes ?? "";
        _cache.Clear();
    }

    public bool RemoveComponent(string name)
    {
        var removed = _components.Remove(name);
        if (removed) _cache.Clear();
        return removed;
    }

    public string? GetComponent(string name) => _components.TryGetValue(name, out var classes) ? classes : null;

    /// <summary>
    /// The closest class this theme recognizes for an unknown one ("did you mean"), variant prefixes included:
    /// <c>hovr:bg-blu-600</c> → <c>hover:bg-blue-600</c>. <c>null</c> when the class is recognized or nothing is close enough.
    /// </summary>
    public string? SuggestClass(string unknownClass) => TwSuggestions.For(unknownClass, this);

    /// <summary>Parses a class string. Returns the same result instance for the same (normalized) string.</summary>
    [Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
    public TwParsed Parse(string? classes)
    {
        var key = TwParser.Normalize(classes);
        return key.Length == 0 ? TwParsed.Empty : _cache.GetOrAdd(key, k => TwParser.Parse(k, this));
    }

    /// <summary>
    /// The newest theme file format (<c>"version"</c> in <c>tw.config.json</c>) this FormWind reads. Files without <c>"version"</c>
    /// are version 1; files with a newer version are rejected with <see cref="NotSupportedException"/>.
    /// </summary>
    public const int FileVersion = 1;

    public static TwTheme FromJson(string json)
    {
        var theme = new TwTheme();
        theme.ApplyJson(json);
        return theme;
    }

    /// <summary>Loads a theme file; paths in the <c>fonts</c> list are resolved relative to the file's folder.</summary>
    public static TwTheme Load(string path)
    {
        var theme = new TwTheme();
        theme.ApplyJson(File.ReadAllText(path), Path.GetDirectoryName(Path.GetFullPath(path)));
        return theme;
    }

    /// <summary>Loads the file if it exists (throws if it is invalid), otherwise returns the default theme.</summary>
    public static TwTheme LoadIfExists(string path) => File.Exists(path) ? Load(path) : new TwTheme();

    /// <summary>Applies settings in the <c>tw.config.json</c> format on top of this theme.</summary>
    /// <param name="json">Theme content.</param>
    /// <param name="baseDirectory">Folder the <c>fonts</c> paths are relative to (default: the application folder).</param>
    public void ApplyJson(string json, string? baseDirectory = null) => ApplyJson(json, baseDirectory, skipMissingFiles: false);

    // skipMissingFiles: .svg dosyası bulunamayan ikonlar hata yerine atlanır (Designer temayı uygulamanın varlıkları olmayan
    // bir klasörden okuyabilir).
    internal void ApplyJson(string json, string? baseDirectory, bool skipMissingFiles)
    {
        // Bozuk dosya türüne göre JsonException (sözdizimi), InvalidOperationException (beklenmeyen değer tipi, örn. bileşen
        // yerine sayı) ya da FormatException çıkıyordu; uygulama hepsini ayrı ayrı yakalamak zorundaydı. Hepsi
        // FormatException olur, asıl hata InnerException'dadır.
        try
        {
            ApplyJsonCore(json, baseDirectory, skipMissingFiles);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"tw.config.json: invalid JSON: {ex.Message}", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new FormatException($"tw.config.json: unexpected value type: {ex.Message}", ex);
        }
    }

    private void ApplyJsonCore(string json, string? baseDirectory, bool skipMissingFiles)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        using var document = JsonDocument.Parse(json, options);
        var root = document.RootElement;

        // "version": 1 — dosya biçiminin sürümü; yoksa 1 sayılır. Daha yeni bir biçim sessizce yanlış okunmak yerine reddedilir.
        if (root.TryGetProperty("version", out var fileVersion))
        {
            if (fileVersion.ValueKind != JsonValueKind.Number || !fileVersion.TryGetInt32(out var number) || number < 1)
                throw new FormatException($"tw.config.json: invalid 'version' value: {fileVersion} (a positive integer).");
            if (number > FileVersion)
                throw new NotSupportedException(
                    $"tw.config.json: file version {number} is newer than this FormWind supports ({FileVersion}); update the FormWind package.");
        }

        // "preset": "fluent" — hazır görünüm; dosyanın geri kalanı onun üzerine uygulanır.
        if (root.TryGetProperty("preset", out var preset))
            UsePresetCore(TwPresets.Parse(preset.GetString()));

        // "fonts": ["Assets/fonts/Quicksand.ttf"] — kurulum gerektirmeden kaydedilir; bulunamayan dosya atlanır.
        if (root.TryGetProperty("fonts", out var fonts))
        {
            var directory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            foreach (var font in fonts.EnumerateArray())
            {
                var path = font.GetString();
                if (!string.IsNullOrWhiteSpace(path)) TwFonts.TryRegister(Path.Combine(directory, path!));
            }
        }

        // "palette": "v4" — Tailwind v4 renkleri; "colors" bunun üzerine eklenir.
        if (root.TryGetProperty("palette", out var palette))
        {
            var version = palette.GetString();
            UsePalette(string.Equals(version, "v4", StringComparison.OrdinalIgnoreCase) ? TwPaletteVersion.V4
                : string.Equals(version, "v3", StringComparison.OrdinalIgnoreCase) ? TwPaletteVersion.V3
                : throw new FormatException($"tw.config.json: invalid 'palette' value: {version} (v3 or v4)."));
        }

        if (root.TryGetProperty("fontFamily", out var sans))
            FontFamilySans = sans.GetString() ?? FontFamilySans;
        if (root.TryGetProperty("fontFamilyMono", out var mono))
            FontFamilyMono = mono.GetString() ?? FontFamilyMono;
        if (root.TryGetProperty("spacingUnit", out var unit))
            SpacingUnit = unit.GetInt32();
        if (root.TryGetProperty("defaultBorderColor", out var border))
            DefaultBorderColor = ParseColor(border.GetString(), "defaultBorderColor");

        if (root.TryGetProperty("colors", out var colors))
        {
            foreach (var color in colors.EnumerateObject())
            {
                if (color.Value.ValueKind == JsonValueKind.String)
                {
                    SetColor(color.Name, ParseColor(color.Value.GetString(), color.Name));
                }
                else if (color.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var shade in color.Value.EnumerateObject())
                        SetColor(color.Name, shade.Name, ParseColor(shade.Value.GetString(), color.Name + "-" + shade.Name));
                }
                else
                {
                    throw new FormatException($"tw.config.json: 'colors.{color.Name}' must be a color or an object of shades.");
                }
            }
        }

        if (root.TryGetProperty("components", out var components))
        {
            foreach (var component in components.EnumerateObject())
                SetComponent(component.Name, component.Value.GetString() ?? "");
        }

        // "autoStyle": { "Button": "btn-primary", "Label": "muted", "ListView": "" }
        if (root.TryGetProperty("autoStyle", out var autoStyle))
        {
            foreach (var entry in autoStyle.EnumerateObject())
                SetAutoStyle(entry.Name, entry.Value.GetString() ?? "");
        }

        // "fontWeights": { "Inter": { "300": "Inter Light", "600": "Inter SemiBold" } }
        if (root.TryGetProperty("fontWeights", out var fontWeights))
        {
            foreach (var family in fontWeights.EnumerateObject())
            {
                foreach (var weight in family.Value.EnumerateObject())
                {
                    if (!int.TryParse(weight.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                        throw new FormatException($"tw.config.json: 'fontWeights.{family.Name}.{weight.Name}' is not a valid weight.");
                    SetFontWeight(family.Name, value, weight.Value.GetString() ?? "");
                }
            }
        }

        if (root.TryGetProperty("iconFont", out var iconFont))
            IconFontFamily = iconFont.GetString() ?? IconFontFamily;

        // "icons": { "invoice": "E8A5", "box": "Assets/icons/box.svg", "dot": "<svg ...>...</svg>" }
        if (root.TryGetProperty("icons", out var icons))
        {
            foreach (var icon in icons.EnumerateObject())
            {
                var value = icon.Value.GetString() ?? "";
                if (value.TrimStart().StartsWith("<", StringComparison.Ordinal))
                {
                    SetSvgIcon(icon.Name, value);
                }
                else if (value.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    var path = Path.Combine(baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory, value);
                    if (!File.Exists(path))
                    {
                        if (skipMissingFiles) continue;
                        throw new FormatException($"tw.config.json: file for 'icons.{icon.Name}' not found: {path}");
                    }
                    SetSvgIcon(icon.Name, File.ReadAllText(path));
                }
                else if (TwIcons.TryParseCodePoint(value, out var glyph))
                {
                    SetIcon(icon.Name, glyph);
                }
                else
                {
                    throw new FormatException($"tw.config.json: invalid value for 'icons.{icon.Name}' (code point, .svg path or <svg>): {value}");
                }
            }
        }
    }

    private void SetColorCore(string name, string shade, Color color)
    {
        if (!_colors.TryGetValue(name, out var shades))
        {
            shades = new Dictionary<string, Color>(StringComparer.Ordinal);
            _colors.Add(name, shades);
        }
        shades[shade] = color;
    }

    private static Color ParseColor(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException($"tw.config.json: empty color value for '{key}'.");
        // Tailwind v4 biçimi: oklch(62.3% 0.214 259.8)
        if (value!.TrimStart().StartsWith("oklch", StringComparison.OrdinalIgnoreCase))
        {
            if (TwOklch.TryParse(value, out var oklch)) return oklch;
            throw new FormatException($"tw.config.json: invalid color for '{key}': {value}");
        }
        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch (Exception ex)
        {
            throw new FormatException($"tw.config.json: invalid color for '{key}': {value}", ex);
        }
    }
}
