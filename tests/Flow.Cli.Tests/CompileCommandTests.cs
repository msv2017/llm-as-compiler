using Flow.Cli;

namespace Flow.Cli.Tests;

public class CompileCommandTests
{
    [Fact]
    public async Task NoArguments_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await CompileCommand.RunAsync(Array.Empty<string>(), stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task TooManyArguments_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await CompileCommand.RunAsync(new[] { "a.json", "b.json" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task ScenarioFileDoesNotExist_ReturnsExitCode2_WithFilePathInError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var missingPath = Path.Combine(Path.GetTempPath(), $"flow-cli-does-not-exist-{Guid.NewGuid():N}.json");

        var exitCode = await CompileCommand.RunAsync(new[] { missingPath }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains(missingPath, stderr.ToString());
    }

    [Fact]
    public async Task MalformedScenarioJson_ReturnsExitCode2_WithParseError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{ not valid json");

            var exitCode = await CompileCommand.RunAsync(new[] { path }, stdout, stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("Invalid scenario file", stderr.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ValidScenarioButNoApiKey_ReturnsExitCode2_WithClearError()
    {
        var previousKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, ValidScenarioJson);

                var exitCode = await CompileCommand.RunAsync(new[] { path }, stdout, stderr);

                Assert.Equal(2, exitCode);
                Assert.Contains("OPENAI_API_KEY", stderr.ToString());
            }
            finally
            {
                File.Delete(path);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", previousKey);
        }
    }

    [Fact]
    public async Task ProviderAnthropicFlag_ButNoApiKey_ReturnsExitCode2_WithClearError()
    {
        var previousKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, ValidScenarioJson);

                var exitCode = await CompileCommand.RunAsync(new[] { path, "--provider", "anthropic" }, stdout, stderr);

                Assert.Equal(2, exitCode);
                Assert.Contains("ANTHROPIC_API_KEY", stderr.ToString());
            }
            finally
            {
                File.Delete(path);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previousKey);
        }
    }

    [Fact]
    public async Task ProviderFlagBeforePath_IsAlsoAccepted()
    {
        var previousKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, ValidScenarioJson);

                var exitCode = await CompileCommand.RunAsync(new[] { "--provider", "anthropic", path }, stdout, stderr);

                Assert.Equal(2, exitCode);
                Assert.Contains("ANTHROPIC_API_KEY", stderr.ToString());
            }
            finally
            {
                File.Delete(path);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previousKey);
        }
    }

    [Fact]
    public async Task UnknownProvider_ReturnsExitCode2_NamingTheBadValue()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, ValidScenarioJson);

            var exitCode = await CompileCommand.RunAsync(new[] { path, "--provider", "bogus" }, stdout, stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("bogus", stderr.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ProviderFlagMissingValue_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await CompileCommand.RunAsync(new[] { "scenario.json", "--provider" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task SaveFlagMissingValue_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var path = Path.GetTempFileName();
        try
        {
            var exitCode = await CompileCommand.RunAsync(new[] { path, "--save" }, stdout, stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("Usage:", stderr.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveFlag_ScenarioFileMissing_DoesNotWriteSaveFile()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var missingScenarioPath = Path.Combine(Path.GetTempPath(), $"flow-cli-scenario-{Guid.NewGuid():N}.json");
        var savePath = Path.Combine(Path.GetTempPath(), $"flow-cli-save-{Guid.NewGuid():N}.json");

        var exitCode = await CompileCommand.RunAsync(new[] { missingScenarioPath, "--save", savePath }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.False(File.Exists(savePath));
        Assert.False(File.Exists(CompileCommand.GetHumanReadablePath(savePath)));
    }

    [Fact]
    public void GetHumanReadablePath_JsonExtension_SwapsToTxt()
    {
        var result = CompileCommand.GetHumanReadablePath("workflow.json");

        Assert.Equal("workflow.txt", result);
    }

    [Fact]
    public void GetHumanReadablePath_NoExtension_AppendsTxt()
    {
        var result = CompileCommand.GetHumanReadablePath("workflow");

        Assert.Equal("workflow.txt", result);
    }

    [Fact]
    public void GetHumanReadablePath_AlreadyTxtExtension_AppendsReadableTxtToAvoidCollision()
    {
        var result = CompileCommand.GetHumanReadablePath("workflow.txt");

        Assert.Equal("workflow.txt.readable.txt", result);
    }

    [Fact]
    public void GetHumanReadablePath_PathWithDirectory_PreservesDirectory()
    {
        var result = CompileCommand.GetHumanReadablePath(Path.Combine("out", "workflow.json"));

        Assert.Equal(Path.Combine("out", "workflow.txt"), result);
    }

    private const string ValidScenarioJson = """
    {
      "prompt": "p",
      "inputType": { "kind": "primitive", "name": "String" },
      "outputType": { "kind": "primitive", "name": "String" },
      "tools": []
    }
    """;
}
