using Flow.Cli;

namespace Flow.Cli.Tests;

public class CliDispatcherTests
{
    [Fact]
    public async Task NoArguments_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await CliDispatcher.RunAsync(Array.Empty<string>(), stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task UnknownSubcommand_PrintsError_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await CliDispatcher.RunAsync(new[] { "frobnicate" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("frobnicate", stderr.ToString());
    }

    [Fact]
    public async Task CompileSubcommand_DelegatesToCompileCommand()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // No further args after "compile" -> CompileCommand's own usage error, proving dispatch
        // actually reached CompileCommand rather than swallowing the rest of the args.
        var exitCode = await CliDispatcher.RunAsync(new[] { "compile" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("compile <scenario.json>", stderr.ToString());
    }

    [Fact]
    public async Task ScaffoldSubcommand_DelegatesToScaffoldCommand()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await CliDispatcher.RunAsync(new[] { "scaffold" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("scaffold <output.json>", stderr.ToString());
    }

    [Fact]
    public async Task RunSubcommand_DelegatesToRunCommand()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // No further args after "run" -> RunCommand's own usage error, proving dispatch
        // actually reached RunCommand rather than falling through to the Unknown branch.
        var exitCode = await CliDispatcher.RunAsync(new[] { "run" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("run <compiled-workflow.json>", stderr.ToString());
    }
}
