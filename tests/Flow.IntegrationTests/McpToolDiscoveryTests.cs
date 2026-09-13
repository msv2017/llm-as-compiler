using System.Text.Json;
using Flow.Cli.Scaffold;
using Xunit;

namespace Flow.IntegrationTests;

// Requires a local MCP server. Start one before running this test:
//   npx -y @modelcontextprotocol/server-everything streamableHttp
// It listens on http://localhost:3001/mcp (per its own source, dist/transports/streamableHttp.js).
// Skips if unreachable, the same way the OpenAI/Anthropic integration tests skip when their API key
// isn't set -- this is this project's live, not scripted-fake, verification for the one piece of
// real MCP wire-protocol code here.
public class McpToolDiscoveryTests
{
    private static readonly Uri LocalEverythingServer = new("http://localhost:3001/mcp");

    private static async Task<bool> IsReachableAsync()
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await httpClient.GetAsync(LocalEverythingServer);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsToolsFromLocalEverythingServer()
    {
        if (!await IsReachableAsync()) return; // local MCP server not running; see class comment

        var tools = await McpToolDiscovery.DiscoverAsync(LocalEverythingServer, CancellationToken.None);

        Assert.NotEmpty(tools);
        var echoTool = Assert.Single(tools, t => t.Name == "echo");
        Assert.Equal(JsonValueKind.Object, echoTool.InputSchema.ValueKind);
    }
}
