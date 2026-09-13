using Flow.Cli;

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
}
