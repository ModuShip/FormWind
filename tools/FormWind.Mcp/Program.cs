using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

// Araç net8.0 hedefler (dotnet tool), ama WinForms kullanır: yalnızca Windows.
[assembly: SupportedOSPlatform("windows")]

namespace FormWind.Mcp;

/// <summary>
/// formwind-mcp: a stdio MCP server that lets coding agents validate and explain FormWind classes, list components and render
/// controls. Read-only: no tool writes files; the theme comes from <c>tw.config.json</c> in the working folder unless a tool
/// is given a preset or a theme file.
/// </summary>
internal static class Program
{
    public const string Instructions =
        "FormWind styles WinForms controls with Tailwind-style classes. Read formwind://llms.txt first. "
        + "Before writing classes, prefer a component from list_components; after writing them, call validate_classes and fix "
        + "every unknown class (use the suggestion). Use explain_classes to check what a state (dark, hover, disabled …) resolves "
        + "to, and render to look at the result in light and dark mode.";

    public static string Version { get; } =
        typeof(TwTheme).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(TwTheme).Assembly.GetName().Version?.ToString() ?? "0";

    private static async Task Main(string[] args)
    {
        if (args.Contains("--version"))
        {
            Console.WriteLine(Version);
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        // stdout MCP iletileri içindir; günlükler stderr'e gider.
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton<StaThread>();
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "formwind", Title = "FormWind", Version = Version };
                options.ServerInstructions = Instructions;
            })
            .WithStdioServerTransport()
            .WithRequestFilters(filters => filters.AddCallToolFilter(ToolErrors.Filter))
            .WithTools<ClassTools>()
            .WithTools<RenderTools>()
            .WithResources<DocResources>();
        await builder.Build().RunAsync();
    }
}
