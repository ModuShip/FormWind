using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FormWind.Mcp;

[McpServerResourceType]
public sealed class DocResources
{
    [McpServerResource(UriTemplate = "formwind://llms.txt", Name = "llms.txt", Title = "FormWind for coding agents", MimeType = "text/markdown")]
    [Description("Summary of FormWind for coding agents: approaches, components, recognized classes, variants, controls and rules.")]
    public static string Llms()
    {
        using var stream = typeof(DocResources).Assembly.GetManifestResourceStream("FormWind.Mcp.llms.txt")
                           ?? throw ToolErrors.Create("missing_resource", "llms.txt is missing from the server.", "Reinstall FormWind.Mcp.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [McpServerResource(UriTemplate = "formwind://presets/{name}", Name = "preset", Title = "FormWind preset", MimeType = "application/json")]
    [Description("A preset in the tw.config.json format (tailwind, fluent, antd, material). Copy it as tw.config.json to change a preset throughout.")]
    public static string Preset([Description("tailwind, fluent, antd or material.")] string name)
    {
        var preset = Themes.ParsePreset(name);
        if (preset != TwPreset.Tailwind) return TwPresets.Json(preset);
        // Tailwind varsayılandır; ayrı bir dosyası yok. Bileşenleri ve AutoStyle eşlemesi aynı biçimde yazılır.
        var defaults = new
        {
            components = TwTheme.DefaultComponents,
            autoStyle = TwTheme.DefaultAutoStyle,
        };
        return JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
    }
}
