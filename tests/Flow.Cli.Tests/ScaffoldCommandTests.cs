using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Cli;
using Flow.Cli.Scaffold;
using Flow.Contracts;

namespace Flow.Cli.Tests;

public class ScaffoldCommandTests
{
    [Fact]
    public async Task MissingAllArguments_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await ScaffoldCommand.RunAsync(Array.Empty<string>(), stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task MissingPrompt_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await ScaffoldCommand.RunAsync(
            new[] { "out.json", "--mcp-url", "http://localhost:3001/mcp" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task MissingMcpUrl_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await ScaffoldCommand.RunAsync(
            new[] { "out.json", "--prompt", "do the thing" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task MalformedMcpUrl_ReturnsExitCode2_NamingTheBadValue()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await ScaffoldCommand.RunAsync(
            new[] { "out.json", "--mcp-url", "not-a-url", "--prompt", "p" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("not-a-url", stderr.ToString());
    }

    [Fact]
    public async Task McpUrlFlagMissingValue_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await ScaffoldCommand.RunAsync(
            new[] { "out.json", "--mcp-url" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task UnreachableMcpServer_ReturnsExitCode2_WithUrlInError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var outputPath = Path.Combine(Path.GetTempPath(), $"flow-cli-scaffold-{Guid.NewGuid():N}.json");

        var exitCode = await ScaffoldCommand.RunAsync(
            new[] { outputPath, "--mcp-url", "http://localhost:1/mcp", "--prompt", "p" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("localhost:1", stderr.ToString());
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void ToToolJson_ThenScenarioJsonParse_RoundTripsWithoutThrowing()
    {
        var inputSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": { "message": { "type": "string" } },
          "required": ["message"]
        }
        """).RootElement;
        var tool = new DiscoveredTool("echo", "Echoes the message back", inputSchema, null);
        var warnings = new List<string>();

        var toolJson = ScaffoldCommand.ToToolJson(tool, warnings);

        var scenario = new ScenarioFileJson
        {
            Prompt = "Echo the message.",
            InputType = new FlowTypeJson
            {
                Kind = "object",
                Name = "Request",
                Fields = new Dictionary<string, FlowTypeJson> { ["message"] = new() { Kind = "primitive", Name = "String" } }
            },
            OutputType = new FlowTypeJson
            {
                Kind = "object",
                Name = "Result",
                Fields = new Dictionary<string, FlowTypeJson> { ["reply"] = new() { Kind = "primitive", Name = "String" } }
            },
            Tools = new List<ToolJson> { toolJson }
        };

        var json = JsonSerializer.Serialize(scenario, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        Assert.DoesNotContain(": null", json);

        var (source, catalog) = ScenarioJson.Parse(json);

        Assert.Equal("Echo the message.", source.Prompt);
        Assert.True(catalog.TryGet("echo", out var parsedTool));
        Assert.Equal(ToolEffect.Unknown, parsedTool!.Effect);
        Assert.Equal(ToolRetryPolicy.Never, parsedTool.Retry);
    }
}
