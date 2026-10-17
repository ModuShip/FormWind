using System;
using System.IO;

namespace FormWind;

/// <summary>
/// Ready-made looks. Each one is a theme file in the <c>tw.config.json</c> format (<c>src/FormWind/Presets/</c>, embedded in the
/// library) that sets the semantic colors (<see cref="TwTheme.SemanticColors"/>), the fonts and the components whose shape
/// differs; all presets define the same component names. To customize one, copy its file into the application's theme file and
/// edit it, or set <c>"preset"</c> and override only what differs. The looks are inspired by the named design systems; colors are
/// adjusted where the original fails the WCAG AA contrast checks the presets are tested with.
/// </summary>
public enum TwPreset
{
    /// <summary>The defaults: Tailwind CSS colors and components.</summary>
    Tailwind = 0,

    /// <summary>Inspired by Fluent 2 (Windows 11): Segoe UI Variable, 4 px corners, neutral grays, communication blue.</summary>
    Fluent = 1,

    /// <summary>Inspired by Ant Design: 6 px corners, compact buttons, Ant blue, borders that turn blue on hover.</summary>
    Antd = 2,

    /// <summary>Inspired by Material Design 3: pill buttons, tonal surfaces, the baseline purple.</summary>
    Material = 3,
}

internal static class TwPresets
{
    public static string Json(TwPreset preset)
    {
        var name = "FormWind.Presets." + Name(preset) + ".json";
        using var stream = typeof(TwPresets).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Preset resource not found: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string Name(TwPreset preset) => preset switch
    {
        TwPreset.Tailwind => "tailwind",
        TwPreset.Fluent => "fluent",
        TwPreset.Antd => "antd",
        TwPreset.Material => "material",
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

    public static TwPreset Parse(string? value)
    {
        foreach (TwPreset preset in Enum.GetValues(typeof(TwPreset)))
            if (string.Equals(Name(preset), value, StringComparison.OrdinalIgnoreCase)) return preset;
        throw new FormatException($"tw.config.json: invalid 'preset' value: {value} (tailwind, fluent, antd or material).");
    }
}
