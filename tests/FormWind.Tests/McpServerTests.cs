#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace FormWind.Tests;

/// <summary>formwind-mcp end to end: the real executable over stdio, as an MCP client sees it.</summary>
public class McpServerTests
{
    private static async Task<McpClient> StartAsync()
    {
        // Sunucu kendi çıktı klasöründen çalıştırılır: test klasöründeki bağımlılıklar testin hedef çatısı için (net9, net10)
        // çözülmüştür, sunucu ise .NET 8 çalışma zamanında açılır. Klasör: tests/FormWind.Tests/bin/<Configuration>/<tfm>/.
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var repository = output.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var server = Path.Combine(repository, "tools", "FormWind.Mcp", "bin", configuration, "net8.0", "formwind-mcp.dll");
        Assert.True(File.Exists(server), server);
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "formwind",
            Command = "dotnet",
            Arguments = new[] { server },
            // tw.config.json olmayan bir klasör: varsayılan tema.
            WorkingDirectory = Path.GetTempPath(),
        });
        return await McpClient.CreateAsync(transport);
    }

    [Fact]
    public async Task Lists_tools_and_resources()
    {
        await using var client = await StartAsync();

        Assert.Equal("formwind", client.ServerInfo.Name);
        Assert.False(string.IsNullOrEmpty(client.ServerInstructions));
        var tools = await client.ListToolsAsync();
        Assert.Equal(new[] { "explain_classes", "list_components", "list_vocabulary", "render", "validate_classes" }, tools.Select(t => t.Name).OrderBy(n => n));
        Assert.All(tools, t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint));
        Assert.All(tools, t => Assert.NotNull(t.ProtocolTool.OutputSchema));

        var resources = await client.ListResourcesAsync();
        Assert.Contains(resources, r => r.Uri == "formwind://llms.txt");
        var templates = await client.ListResourceTemplatesAsync();
        Assert.Contains(templates, t => t.UriTemplate == "formwind://presets/{name}");
    }

    [Fact]
    public async Task Validates_classes_with_structured_content()
    {
        await using var client = await StartAsync();

        var result = await client.CallToolAsync("validate_classes", new Dictionary<string, object?> { ["classes"] = "bg-blu-600 p-4" });

        Assert.NotEqual(true, result.IsError);
        var structured = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(result.StructuredContent));
        Assert.False(structured.GetProperty("valid").GetBoolean());
        var unknown = structured.GetProperty("unknown")[0];
        Assert.Equal("bg-blu-600", unknown.GetProperty("class").GetString());
        Assert.Equal("bg-blue-600", unknown.GetProperty("suggestion").GetString());
    }

    [Fact]
    public async Task Reports_errors_as_tool_errors()
    {
        await using var client = await StartAsync();

        var result = await client.CallToolAsync("list_components", new Dictionary<string, object?> { ["preset"] = "bootstrap" });

        Assert.True(result.IsError);
        var error = JsonSerializer.Deserialize<JsonElement>(Assert.IsType<TextContentBlock>(result.Content[0]).Text);
        Assert.Equal("unknown_preset", error.GetProperty("code").GetString());
        Assert.Equal("Unknown preset 'bootstrap'.", error.GetProperty("message").GetString());
        Assert.Contains("fluent", error.GetProperty("hint").GetString());
    }

    [Fact]
    public async Task Renders_a_png()
    {
        await using var client = await StartAsync();

        var result = await client.CallToolAsync("render", new Dictionary<string, object?>
        {
            ["controlType"] = "TwButton",
            ["classes"] = "btn-primary",
            ["text"] = "Save",
            ["states"] = new[] { "base", "hover" },
        });

        Assert.NotEqual(true, result.IsError);
        var image = Assert.Single(result.Content.OfType<ImageContentBlock>());
        Assert.Equal("image/png", image.MimeType);
        var png = image.DecodedData.ToArray();
        Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, png.Take(4));

        var summary = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(result.StructuredContent));
        Assert.Equal("TwButton", summary.GetProperty("controlType").GetString());
        Assert.Equal(new[] { "base", "hover" }, summary.GetProperty("states").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal("control", summary.GetProperty("stateTarget").GetString());
        Assert.True(summary.GetProperty("layout").GetProperty("cellWidth").GetInt32() > 0);
    }

    [Fact]
    public async Task Serves_llms_txt()
    {
        await using var client = await StartAsync();

        var resource = await client.ReadResourceAsync("formwind://llms.txt");

        var text = Assert.IsType<TextResourceContents>(Assert.Single(resource.Contents));
        Assert.StartsWith("# FormWind", text.Text);
    }
}
#endif
