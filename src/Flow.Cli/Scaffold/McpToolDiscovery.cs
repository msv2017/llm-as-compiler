using System.Text.Json;
using ModelContextProtocol.Client;

namespace Flow.Cli.Scaffold;

public sealed record DiscoveredTool(string Name, string? Description, JsonElement InputSchema, JsonElement? OutputSchema);

public static class McpToolDiscovery
{
    public static async Task<IReadOnlyList<DiscoveredTool>> DiscoverAsync(
        Uri mcpUrl, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? additionalHeaders = null)
    {
        var options = new HttpClientTransportOptions
        {
            Endpoint = mcpUrl,
            TransportMode = HttpTransportMode.AutoDetect
        };

        if (additionalHeaders is not null)
            options.AdditionalHeaders = new Dictionary<string, string>(additionalHeaders);

        var transport = new HttpClientTransport(options);

        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

        return tools
            .Select(tool => new DiscoveredTool(tool.Name, tool.Description, tool.JsonSchema, tool.ReturnJsonSchema))
            .ToList();
    }
}
