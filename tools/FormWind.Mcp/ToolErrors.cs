using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FormWind.Mcp;

/// <summary>The error result of a tool: the text content is a JSON object with a stable code, the message and a hint.</summary>
public sealed record ToolError(string Code, string Message, string? Hint);

internal static class ToolErrors
{
    // Mesajlardaki tırnaklar (Unknown preset 'x') ' olarak kaçırılmasın; ajan metni okuyabilsin.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static McpException Create(string code, string message, string? hint = null) =>
        new(JsonSerializer.Serialize(new ToolError(code, message, hint), Options));

    public static McpException Create(string code, string message, string? hint, Exception inner) =>
        new(JsonSerializer.Serialize(new ToolError(code, message, hint), Options), inner);

    /// <summary>Reads an error back (tests, clients written in .NET); <c>null</c> when the text is not one.</summary>
    public static ToolError? Parse(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<ToolError>(text, Options) is { Code: not null, Message: not null } error ? error : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Call-tool filter: the SDK would report a tool's exception as "An error occurred invoking 'tool': message"; FormWind's
    /// errors are returned as the error JSON alone, so a client can parse it.
    /// </summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, cancellationToken) =>
        {
            try
            {
                return await next(context, cancellationToken);
            }
            catch (McpException ex) when (Parse(ex.Message) is not null)
            {
                return new CallToolResult { IsError = true, Content = new List<ContentBlock> { new TextContentBlock { Text = ex.Message } } };
            }
        };
}
