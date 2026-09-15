using Flow.Runtime;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Flow.Cli.Runtime;

public sealed class McpToolInvoker : IMcpInvoker
{
    private readonly McpClient _client;

    public McpToolInvoker(McpClient client) => _client = client;

    public async Task<object?> InvokeAsync(
        string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var jsonArguments = arguments.ToDictionary(kv => kv.Key, kv => (object)FlowJsonBridge.ToJsonValue(kv.Value)!);
        var result = await _client.CallToolAsync(toolName, (IReadOnlyDictionary<string, object?>)jsonArguments, cancellationToken: cancellationToken);
        return ConvertResult(result, toolName);
    }

    internal static object? ConvertResult(CallToolResult result, string toolName)
    {
        if (result.IsError == true)
        {
            var message = string.Join(" ", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
            throw new InvalidOperationException(
                $"Tool '{toolName}' reported an error: {(string.IsNullOrWhiteSpace(message) ? "(no details provided)" : message)}");
        }

        if (result.StructuredContent is not { } structuredContent)
        {
            throw new InvalidOperationException(
                $"Tool '{toolName}' did not return structured content; Flow Runtime requires structured MCP tool output.");
        }

        return FlowJsonBridge.ToFlowValue(structuredContent);
    }
}
