using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace FormWind;

/// <summary>
/// Shared <see cref="Font"/> instances and fonts shipped with the application. Every <c>new Font</c> holds a GDI handle,
/// so a single instance is used per family/size/style; instances live for the process and must not be disposed.
/// Because controls hold the fonts, the cache cannot shrink; when <see cref="CacheWarningThreshold"/> is exceeded a Debug
/// warning is written once (usually a sign of <c>text-[…px]</c> classes generated in code).
/// </summary>
public static class TwFonts
{
    private const uint FrPrivate = 0x10;

    // İlk statik alan: GDI+ başlamadan WinForms DPI ayarı uygulanır (bkz. TwDpi).
    private static readonly bool DpiReady = TwDpi.EnsureInitialized();

    private static readonly ConcurrentDictionary<(string Family, float Size, FontStyle Style), Font> Cache =
        new ConcurrentDictionary<(string Family, float Size, FontStyle Style), Font>();

    /// <summary>When the number of cached fonts exceeds this, a warning is written to the Debug output once.</summary>
    public const int CacheWarningThreshold = 256;

    private static int _warned;

    private static readonly object Gate = new object();
    private static readonly PrivateFontCollection Private = new PrivateFontCollection();
    private static readonly Dictionary<string, FontFamily> PrivateFamilies = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

    private static readonly Lazy<HashSet<string>> Installed = new Lazy<HashSet<string>>(() =>
    {
        using var collection = new InstalledFontCollection();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in collection.Families) names.Add(family.Name);
        return names;
    });

    /// <summary>
    /// Registers a font file for this process without installing it: on both the GDI+ (<see cref="PrivateFontCollection"/>)
    /// and GDI (<c>AddFontResourceEx</c>, FR_PRIVATE) side, so TextRenderer and controls find the font too.
    /// Returns the family names in the file. Registering the same file again is harmless.
    /// </summary>
    public static IReadOnlyList<string> Register(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Font file not found.", fullPath);

        string[] names;
        using (var probe = new PrivateFontCollection())
        {
            probe.AddFontFile(fullPath);
            names = probe.Families.Select(f => f.Name).ToArray();
        }

        lock (Gate)
        {
            AddFontResourceEx(fullPath, FrPrivate, IntPtr.Zero);
            Private.AddFontFile(fullPath);
            foreach (var family in Private.Families)
                PrivateFamilies[family.Name] = family;
        }
        return names;
    }

    /// <summary>Whether a font family is installed or registered with <see cref="Register"/>.</summary>
    public static bool IsInstalled(string family)
    {
        lock (Gate)
        {
            if (PrivateFamilies.ContainsKey(family)) return true;
        }
        return Installed.Value.Contains(family);
    }

    /// <summary>The system UI font used when no family is found (usually Segoe UI).</summary>
    public static string DefaultFamily { get; } = SystemFonts.MessageBoxFont?.FontFamily.Name ?? "Segoe UI";

    /// <summary>
    /// Resolves a CSS font stack: <c>"Quicksand, 'Segoe UI', sans-serif"</c> → the first available family.
    /// Returns the system UI font (Segoe UI) when none is available.
    /// </summary>
    public static string ResolveFamily(string stack)
    {
        if (string.IsNullOrWhiteSpace(stack)) return DefaultFamily;
        if (stack.IndexOf(',') < 0 && IsInstalled(stack.Trim())) return stack.Trim();

        foreach (var part in stack.Split(','))
        {
            var name = part.Trim().Trim('"', '\'');
            if (name.Length > 0 && IsInstalled(name)) return name;
        }
        return DefaultFamily;
    }

    public static Font Get(string family, float sizeInPoints, FontStyle style)
    {
        var resolved = ResolveFamily(family);
        var key = (resolved, (float)Math.Round(sizeInPoints, 2), style);
        return Cache.GetOrAdd(key, k =>
        {
            if (Cache.Count >= CacheWarningThreshold && System.Threading.Interlocked.Exchange(ref _warned, 1) == 0)
                Debug.WriteLine($"FormWind: the font cache exceeded {CacheWarningThreshold} instances; the classes may use many different font sizes.");
            FontFamily? privateFamily;
            lock (Gate) PrivateFamilies.TryGetValue(k.Item1, out privateFamily);
            return privateFamily != null && privateFamily.IsStyleAvailable(k.Item3)
                ? new Font(privateFamily, k.Item2, k.Item3, GraphicsUnit.Point)
                : new Font(k.Item1, k.Item2, k.Item3, GraphicsUnit.Point);
        });
    }

    /// <summary>Number of cached fonts (for tests).</summary>
    internal static int CachedCount => Cache.Count;

    /// <summary>
    /// Applies the style's font values on top of <paramref name="baseFont"/>. The weight is resolved to the theme's
    /// weight families (e.g. 600 → "Segoe UI Semibold").
    /// </summary>
    [Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
    public static Font Create(TwStyle style, Font baseFont, TwTheme theme) => Create(style, baseFont, theme, 1f);

    /// <summary><see cref="Create(TwStyle, Font, TwTheme)"/>; <c>text-*</c> sizes are multiplied by <paramref name="pixelScale"/>.</summary>
    /// <param name="style">The style to apply.</param>
    /// <param name="baseFont">The font that supplies values the style does not set.</param>
    /// <param name="theme">The theme for weight families.</param>
    /// <param name="pixelScale">
    /// Multiplier for <c>text-*</c> sizes: <see cref="TwDpi.FontScale"/> when, under PerMonitorV2, the control's screen differs
    /// from the system DPI. A size taken from the base font already matches that screen and is not multiplied.
    /// </param>
    [Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
    public static Font Create(TwStyle style, Font baseFont, TwTheme theme, float pixelScale)
    {
        var family = ResolveFamily(style.FontFamily ?? baseFont.FontFamily.Name);
        var size = style.FontSizePx is float px ? px * 0.75f * pixelScale : baseFont.SizeInPoints;
        var weight = style.FontWeight ?? (baseFont.Bold ? 700 : 400);
        var (face, bold) = theme.ResolveFontWeight(family, weight);

        var fontStyle = bold ? FontStyle.Bold : FontStyle.Regular;
        if (style.Italic ?? baseFont.Italic) fontStyle |= FontStyle.Italic;
        if (style.Underline ?? baseFont.Underline) fontStyle |= FontStyle.Underline;
        return Get(face, size, fontStyle);
    }

    internal static void TryRegister(string path)
    {
        try
        {
            Register(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is ArgumentException || ex is ExternalException)
        {
            // Font isteğe bağlıdır; yığındaki bir sonraki aile kullanılır.
            Debug.WriteLine($"FormWind: could not load font ({path}): {ex.Message}");
        }
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceEx(string path, uint flags, IntPtr reserved);
}
