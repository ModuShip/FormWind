using System.Collections.Concurrent;

namespace FormWind.Mcp;

/// <summary>The theme a tool call uses: a preset, a theme file, or <c>tw.config.json</c> in the working folder.</summary>
internal static class Themes
{
    public const string PresetDescription = "Preset: tailwind (default), fluent, antd or material. Applied before the theme file.";
    public const string ThemeFileDescription =
        "Path to a tw.config.json. Default: tw.config.json in the server's working folder, if it exists.";

    // Anahtar: ön ayar + dosya yolu + son yazma zamanı; dosya değişince tema yeniden okunur.
    private static readonly ConcurrentDictionary<(TwPreset, string?, DateTime), TwTheme> Cache = new();

    public static TwTheme Resolve(string? preset, string? themeFile)
    {
        var parsedPreset = ParsePreset(preset);
        string? path = null;
        if (!string.IsNullOrWhiteSpace(themeFile))
        {
            path = Path.GetFullPath(themeFile);
            if (!File.Exists(path))
                throw ToolErrors.Create("theme_not_found", $"Theme file not found: {path}",
                    "Pass an absolute path, or omit themeFile to use tw.config.json in the server's working folder.");
        }
        else if (File.Exists("tw.config.json"))
        {
            path = Path.GetFullPath("tw.config.json");
        }

        var key = (parsedPreset, path, path is null ? default : File.GetLastWriteTimeUtc(path));
        return Cache.GetOrAdd(key, k => Load(k.Item1, k.Item2));
    }

    public static TwPreset ParsePreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset)) return TwPreset.Tailwind;
        if (Enum.TryParse<TwPreset>(preset, ignoreCase: true, out var value) && Enum.IsDefined(value)) return value;
        throw ToolErrors.Create("unknown_preset", $"Unknown preset '{preset}'.", "Use tailwind, fluent, antd or material.");
    }

    private static TwTheme Load(TwPreset preset, string? path)
    {
        var theme = new TwTheme(preset);
        if (path is null) return theme;
        try
        {
            theme.ApplyJson(File.ReadAllText(path), Path.GetDirectoryName(path));
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or IOException or ArgumentException)
        {
            throw ToolErrors.Create("invalid_theme", $"{path}: {ex.Message}",
                "Fix the theme file; its format is described in the README (Theme file) and in formwind://presets/{name}.", ex);
        }
        return theme;
    }
}
