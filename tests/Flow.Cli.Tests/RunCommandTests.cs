using Flow.Cli;
using Xunit;

namespace Flow.Cli.Tests;

[Collection("EnvironmentVariableTests")]
public class RunCommandTests
{
    [Fact]
    public async Task NoArguments_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RunCommand.RunAsync(Array.Empty<string>(), stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task MissingMcpUrl_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RunCommand.RunAsync(new[] { "workflow.json", "--input", "input.json" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task BothInputAndInputJson_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RunCommand.RunAsync(
            new[] { "workflow.json", "--mcp-url", "http://localhost:1/mcp", "--input", "input.json", "--input-json", "{}" },
            stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task NeitherInputNorInputJson_PrintsUsage_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RunCommand.RunAsync(
            new[] { "workflow.json", "--mcp-url", "http://localhost:1/mcp" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", stderr.ToString());
    }

    [Fact]
    public async Task MalformedMcpUrl_ReturnsExitCode2_NamingTheBadValue()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RunCommand.RunAsync(
            new[] { "workflow.json", "--mcp-url", "not-a-url", "--input-json", "{}" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("not-a-url", stderr.ToString());
    }

    [Fact]
    public async Task WorkflowFileDoesNotExist_ReturnsExitCode2_WithPathInError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var missingPath = Path.Combine(Path.GetTempPath(), $"flow-cli-run-missing-{Guid.NewGuid():N}.json");

        var exitCode = await RunCommand.RunAsync(
            new[] { missingPath, "--mcp-url", "http://localhost:1/mcp", "--input-json", "{}" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains(missingPath, stderr.ToString());
    }

    [Fact]
    public async Task MalformedInputJson_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var workflowPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(workflowPath, CompiledWorkflowJson.Write(SimpleWorkflow()));

            var exitCode = await RunCommand.RunAsync(
                new[] { workflowPath, "--mcp-url", "http://localhost:1/mcp", "--input-json", "{ not valid" }, stdout, stderr);

            Assert.Equal(2, exitCode);
        }
        finally
        {
            File.Delete(workflowPath);
        }
    }

    [Fact]
    public async Task UnreachableMcpServer_ReturnsExitCode2_WithUrlInError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var workflowPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(workflowPath, CompiledWorkflowJson.Write(SimpleWorkflow()));

            var exitCode = await RunCommand.RunAsync(
                new[] { workflowPath, "--mcp-url", "http://localhost:1/mcp", "--input-json", "{ \"customerId\": \"c1\" }" },
                stdout, stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("localhost:1", stderr.ToString());
        }
        finally
        {
            File.Delete(workflowPath);
        }
    }

    [Fact]
    public async Task McpAuthHeaderFlagWithoutEnvVar_ReturnsExitCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var original = Environment.GetEnvironmentVariable("MCP_AUTH_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("MCP_AUTH_TOKEN", null);

            var exitCode = await RunCommand.RunAsync(
                new[] { "workflow.json", "--mcp-url", "http://localhost:1/mcp", "--input-json", "{}", "--mcp-auth-header", "X-Api-Key" },
                stdout, stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("MCP_AUTH_TOKEN", stderr.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_AUTH_TOKEN", original);
        }
    }

    [Fact]
    public async Task InvalidMcpAuthHeaderName_ReturnsExitCode2_WithoutLeakingToken()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var original = Environment.GetEnvironmentVariable("MCP_AUTH_TOKEN");
        var workflowPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(workflowPath, CompiledWorkflowJson.Write(SimpleWorkflow()));
            Environment.SetEnvironmentVariable("MCP_AUTH_TOKEN", "Bearer super-secret-token-value");

            var exitCode = await RunCommand.RunAsync(
                new[] { workflowPath, "--mcp-url", "http://localhost:1/mcp", "--input-json", "{}", "--mcp-auth-header", "X-Api Key" },
                stdout, stderr);

            Assert.Equal(2, exitCode);
            Assert.DoesNotContain("super-secret-token-value", stderr.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_AUTH_TOKEN", original);
            File.Delete(workflowPath);
        }
    }

    private static Flow.IR.WorkflowDefinition SimpleWorkflow()
    {
        var call = new Flow.IR.Nodes.CallNode("customer", "crm.getCustomerById",
            new Dictionary<string, Flow.IR.Expressions.FlowExpression> { ["id"] = new Flow.IR.Expressions.PathExpression("input.customerId") });
        var returnNode = new Flow.IR.Nodes.ReturnNode(
            new Dictionary<string, Flow.IR.Expressions.FlowExpression> { ["customerName"] = new Flow.IR.Expressions.PathExpression("customer.name") });
        var inputType = new Flow.TypeSystem.ObjectType("Request", new Dictionary<string, Flow.TypeSystem.FlowType> { ["customerId"] = Flow.TypeSystem.PrimitiveType.String });
        var outputType = new Flow.TypeSystem.ObjectType("Result", new Dictionary<string, Flow.TypeSystem.FlowType> { ["customerName"] = Flow.TypeSystem.PrimitiveType.String });
        return new Flow.IR.WorkflowDefinition("FindCustomerName", inputType, outputType, new Flow.IR.WorkflowNode[] { call }, returnNode);
    }
}
