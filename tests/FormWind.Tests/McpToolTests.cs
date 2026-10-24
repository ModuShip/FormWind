#if !NETFRAMEWORK
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using FormWind.Mcp;
using FormWind.Tests.Performance;
using ModelContextProtocol;
using Xunit;

namespace FormWind.Tests;

/// <summary>formwind-mcp's tools, called directly.</summary>
[Collection(UiCollection.Name)] // Renderer.Render TwMotion.Enabled'ı süreç genelinde kapatır; geçiş testleriyle aynı anda koşmamalı
public class McpToolTests : IDisposable
{
    // Sunucu sürecinde animasyonlar hep kapalı kalır; test sürecinde diğer testler için TestMotion'ın değeri geri verilir.
    public void Dispose() => TwMotion.Enabled = true;

    [Fact]
    public void Validate_reports_unknown_classes_with_suggestions()
    {
        var result = ClassTools.ValidateClasses("btn-primary px-6 hovr:bg-blu-700 not-a-class-at-all");

        Assert.False(result.Valid);
        Assert.Equal(new[] { new UnknownClass("hovr:bg-blu-700", "hover:bg-blue-700"), new UnknownClass("not-a-class-at-all", null) }, result.Unknown);
        Assert.Equal(new[] { "btn-primary" }, result.Components);
    }

    [Fact]
    public void Validate_accepts_recognized_classes()
    {
        var result = ClassTools.ValidateClasses("bg-white dark:bg-gray-900 rounded-lg px-4");

        Assert.True(result.Valid);
        Assert.Empty(result.Unknown);
    }

    [Fact]
    public void Explain_resolves_each_variant_and_requested_combinations()
    {
        var result = ClassTools.ExplainClasses("bg-white px-4 hover:bg-gray-100 dark:bg-gray-900", new[] { "dark:hover" });

        var states = result.States.ToDictionary(s => s.State);
        Assert.Equal(new[] { "base", "dark", "dark:hover", "hover" }, result.States.Select(s => s.State).OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal("#ffffff", states["base"].Style["backColor"]);
        var padding = Assert.IsAssignableFrom<System.Collections.Generic.IDictionary<string, int>>(states["base"].Style["padding"]);
        Assert.Equal(16, padding["left"]);
        Assert.False(padding.ContainsKey("top"));
        Assert.Equal("#111827", states["dark"].Style["backColor"]);
        // hover:, dark:'tan sonra uygulanır (durum bitleri önceliktir).
        Assert.Equal("#f3f4f6", states["dark:hover"].Style["backColor"]);
    }

    [Fact]
    public void Explain_reports_parts_separately_and_expands_components()
    {
        var result = ClassTools.ExplainClasses("table header:bg-gray-100");

        var header = Assert.Single(result.States, s => s.Part == "header" && s.State == "base");
        Assert.Equal("#f3f4f6", header.Style["backColor"]);
        Assert.Equal("table", Assert.Single(result.Components).Name);
    }

    [Fact]
    public void Explain_rejects_unknown_states()
    {
        Assert.Throws<McpException>(() => ClassTools.ExplainClasses("bg-white", new[] { "hovered" }));
    }

    [Fact]
    public void Components_follow_the_preset()
    {
        var tailwind = ClassTools.ListComponents();
        var material = ClassTools.ListComponents("material");

        Assert.Contains(tailwind.Components, c => c.Name == "btn-primary");
        Assert.DoesNotContain(tailwind.Components, c => c.Name == "primary");
        Assert.Equal(tailwind.Components.Select(c => c.Name), material.Components.Select(c => c.Name));
        Assert.NotEqual(tailwind.Components.Single(c => c.Name == "btn-primary").Classes, material.Components.Single(c => c.Name == "btn-primary").Classes);
        Assert.Equal(TwTheme.DefaultAutoStyle["Button"], tailwind.AutoStyle["Button"]);
        Assert.Throws<McpException>(() => ClassTools.ListComponents("bootstrap"));
    }

    [Fact]
    public void Vocabulary_examples_are_recognized()
    {
        var theme = new TwTheme();
        var vocabulary = ClassTools.ListVocabulary();

        var unknown = vocabulary.Categories.SelectMany(c => c.Examples).Where(e => theme.Parse(e).Unknown.Count > 0);
        Assert.Empty(unknown);
        Assert.Contains("blue", vocabulary.Colors);
        Assert.Contains("primary", vocabulary.Colors);
        Assert.Single(ClassTools.ListVocabulary("Layout").Categories);
        Assert.Throws<McpException>(() => ClassTools.ListVocabulary("animations"));
    }

    [Fact]
    public void Theme_file_is_applied_and_errors_are_reported()
    {
        var folder = Path.Combine(Path.GetTempPath(), "formwind-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "tw.config.json");
            File.WriteAllText(file, """{ "colors": { "brand": { "500": "#123456" } }, "components": { "hero": "bg-brand-500 p-8" } }""");

            Assert.True(ClassTools.ValidateClasses("hero bg-brand-500", themeFile: file).Valid);
            Assert.Equal("#123456", ClassTools.ExplainClasses("hero", themeFile: file).States[0].Style["backColor"]);

            File.WriteAllText(file, "{ not json");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
            Assert.Throws<McpException>(() => ClassTools.ValidateClasses("p-4", themeFile: file));
            Assert.Throws<McpException>(() => ClassTools.ValidateClasses("p-4", themeFile: Path.Combine(folder, "missing.json")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Render_draws_each_state_and_mode()
    {
        RenderResult? result = null;
        Sta.Run(() => result = Renderer.Render(
            new RenderRequest("Panel", "bg-blue-600 hover:bg-blue-800 dark:bg-blue-400", null, 100, 40, Array.Empty<RenderChild>()),
            new TwTheme(), new[] { "base", "hover" }, new[] { "light", "dark" }));

        Assert.Equal(new Size(100, 40), result!.Size);
        Assert.Empty(result.Unknown);
        using var sheet = new Bitmap(new MemoryStream(result.Png));
        // Sayfa: 56 px etiket sütunu + durum başına bir hücre, 22 px başlık + mod başına bir satır; kontrol hücrede 32 px içeride.
        int cellWidth = (sheet.Width - 56) / 2, cellHeight = (sheet.Height - 22) / 2;
        Color At(int mode, int state) => sheet.GetPixel(56 + state * cellWidth + 32 + 50, 22 + mode * cellHeight + 32 + 20);
        Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), At(0, 0).ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#1e40af").ToArgb(), At(0, 1).ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#60a5fa").ToArgb(), At(1, 0).ToArgb());
        // dark:hover → hover kazanır.
        Assert.Equal(ColorTranslator.FromHtml("#1e40af").ToArgb(), At(1, 1).ToArgb());
    }

    [Fact]
    public void Render_draws_transitions_at_their_target_color()
    {
        RenderResult? result = null;
        Sta.Run(() => result = Renderer.Render(
            new RenderRequest("Panel", "bg-blue-600 hover:bg-blue-800 disabled:bg-gray-200 transition duration-1000", null, 100, 40, Array.Empty<RenderChild>()),
            new TwTheme(), new[] { "hover", "disabled" }, new[] { "light" }));

        using var sheet = new Bitmap(new MemoryStream(result!.Png));
        int cellWidth = (sheet.Width - 56) / 2;
        Color At(int state) => sheet.GetPixel(56 + state * cellWidth + 32 + 50, 22 + 32 + 20);
        Assert.Equal(ColorTranslator.FromHtml("#1e40af").ToArgb(), At(0).ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#e5e7eb").ToArgb(), At(1).ToArgb());
    }

    [Fact]
    public void Render_lays_out_children_and_reports_their_unknown_classes()
    {
        RenderResult? result = null;
        var children = new[] { new RenderChild("TwButton", "btn-primary flex-1", "Save"), new RenderChild("TwButton", "btn-ghst", "Cancel") };
        Sta.Run(() => result = Renderer.Render(
            new RenderRequest("TwPanel", "flex gap-2 p-2", null, 260, 52, children), new TwTheme(), new[] { "base" }, new[] { "light" }));

        Assert.Equal(new[] { "btn-ghst" }, result!.Unknown);
    }

    [Fact]
    public void Render_sizes_text_controls_to_their_padding_classes()
    {
        RenderResult? small = null, large = null;
        Sta.Run(() =>
        {
            small = Renderer.Render(new RenderRequest("TwButton", "px-2 py-1", "Save", null, null, Array.Empty<RenderChild>()), new TwTheme(), new[] { "base" }, new[] { "light" });
            large = Renderer.Render(new RenderRequest("TwButton", "px-8 py-4", "Save", null, null, Array.Empty<RenderChild>()), new TwTheme(), new[] { "base" }, new[] { "light" });
        });

        // px-8 − px-2 = 2 × 24 px; py-4 − py-1 = 2 × 12 px.
        Assert.Equal(48, large!.Size.Width - small!.Size.Width);
        Assert.Equal(24, large.Size.Height - small.Size.Height);
    }

    [Fact]
    public void Render_applies_states_to_the_target_child_and_to_group_hover()
    {
        RenderResult? child = null, group = null;
        var children = new[] { new RenderChild("Panel", "bg-gray-200 hover:bg-blue-600", null, 60, 40), new RenderChild("Panel", "bg-gray-200 group-hover:bg-red-600", null, 60, 40) };
        Sta.Run(() =>
        {
            child = Renderer.Render(new RenderRequest("TwPanel", "flex gap-4 group", null, 140, 40, children, StateTarget: 0), new TwTheme(), new[] { "hover" }, new[] { "light" });
            group = Renderer.Render(new RenderRequest("TwPanel", "flex gap-4 group", null, 140, 40, children), new TwTheme(), new[] { "hover" }, new[] { "light" });
        });

        // Çocuklar kontrolün içinde 32 px kenar payından sonra: ilki x 0–60, ikincisi gap-4 ile x 76–136.
        Color At(RenderResult result, int x)
        {
            using var sheet = new Bitmap(new MemoryStream(result.Png));
            return sheet.GetPixel(result.Layout.LabelWidth + result.Layout.ControlOffset + x, result.Layout.LabelHeight + result.Layout.ControlOffset + 20);
        }
        Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), At(child!, 30).ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#e5e7eb").ToArgb(), At(child!, 106).ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#e5e7eb").ToArgb(), At(group!, 30).ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#dc2626").ToArgb(), At(group!, 106).ToArgb());
    }

    [Fact]
    public void Errors_have_a_code_a_message_and_a_hint()
    {
        var error = ToolErrors.Parse(Assert.Throws<McpException>(() => ClassTools.ListComponents("bootstrap")).Message);

        Assert.NotNull(error);
        Assert.Equal("unknown_preset", error!.Code);
        Assert.Equal("Unknown preset 'bootstrap'.", error.Message);
        Assert.Contains("fluent", error.Hint);
        Assert.Equal("unknown_control_type", ToolErrors.Parse(Assert.Throws<McpException>(() => Renderer.ResolveType("Bogus")).Message)!.Code);
        Assert.Equal("unknown_state", ToolErrors.Parse(Assert.Throws<McpException>(() => ClassTools.ExplainClasses("p-4", new[] { "hovered" })).Message)!.Code);
    }

    [Fact]
    public void Render_rejects_unknown_types_and_states()
    {
        Assert.Throws<McpException>(() => Renderer.ResolveType("Form"));
        Assert.Throws<McpException>(() => Renderer.ResolveType("Bogus"));
        Assert.Equal(typeof(TwButton), Renderer.ResolveType("TwButton"));
        Assert.Throws<McpException>(() => RenderTools.ParseStates(new[] { "pressed" }));
        Assert.Throws<McpException>(() => RenderTools.ParseModes("sepia"));
    }

    [Fact]
    public void Resources_serve_llms_txt_and_presets()
    {
        Assert.StartsWith("# FormWind", DocResources.Llms());
        Assert.Contains("\"btn-primary\"", DocResources.Preset("tailwind"));
        Assert.Contains("\"components\"", DocResources.Preset("fluent"));
        Assert.Throws<McpException>(() => DocResources.Preset("bootstrap"));
    }
}
#endif
