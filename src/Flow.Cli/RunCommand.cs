using System.Text.Json;
using Flow.Analysis;
using Flow.Cli.Runtime;
using Flow.IR;
using Flow.Runtime;
using ModelContextProtocol.Client;

namespace Flow.Cli;

public static class RunCommand
{
    private const string Usage =
        "Usage: flow-cli run <compiled-workflow.json> --mcp-url <url> (--input <input.json> | --input-json <json>)";

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? workflowPath = null;
        string? mcpUrl = null;
        string? inputPath = null;
        string? inputJson = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mcp-url":
                    if (i + 1 >= args.Length) { stderr.WriteLine(Usage); return 2; }
                    mcpUrl = args[++i];
                    break;
                case "--input":
                    if (i + 1 >= args.Length) { stderr.WriteLine(Usage); return 2; }
                    inputPath = args[++i];
                    break;
                case "--input-json":
                    if (i + 1 >= args.Length) { stderr.WriteLine(Usage); return 2; }
                    inputJson = args[++i];
                    break;
                default:
                    if (workflowPath is null) { workflowPath = args[i]; }
                    else { stderr.WriteLine(Usage); return 2; }
                    break;
            }
        }

        if (workflowPath is null || mcpUrl is null)
        {
            stderr.WriteLine(Usage);
            return 2;
        }

        if ((inputPath is null) == (inputJson is null))
        {
            stderr.WriteLine(Usage);
            return 2;
        }

        if (!Uri.TryCreate(mcpUrl, UriKind.Absolute, out var mcpUri))
        {
            stderr.WriteLine($"'{mcpUrl}' is not a valid absolute URL.");
            return 2;
        }

        WorkflowDefinition workflow;
        try
        {
            var workflowJson = await File.ReadAllTextAsync(workflowPath);
            workflow = CompiledWorkflowJson.Parse(workflowJson);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ScenarioParseException)
        {
            stderr.WriteLine($"Could not read '{workflowPath}': {ex.Message}");
            return 2;
        }

        object? input;
        try
        {
            var rawInputJson = inputPath is not null ? await File.ReadAllTextAsync(inputPath) : inputJson!;
            using var document = JsonDocument.Parse(rawInputJson);
            input = FlowJsonBridge.ToFlowValue(document.RootElement);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Could not read input: {ex.Message}");
            return 2;
        }

        return await ExecuteAsync(workflow, input, mcpUri, stdout, stderr);
    }

    private static async Task<int> ExecuteAsync(
        WorkflowDefinition workflow, object? input, Uri mcpUri, TextWriter stdout, TextWriter stderr)
    {
        McpClient client;
        try
        {
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = mcpUri,
                TransportMode = HttpTransportMode.AutoDetect
            });
            client = await McpClient.CreateAsync(transport);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Could not connect to '{mcpUri}': {ex.Message}");
            return 2;
        }

        await using (client)
        {
            var invoker = new McpToolInvoker(client);
            var guard = new RuntimeGuard(invoker, CapabilityAnalyzer.Analyze(workflow));

            ExecutionResult result;
            try
            {
                result = await new WorkflowExecutor().ExecuteAsync(workflow, input, guard);
            }
            catch (Exception ex)
            {
                stderr.WriteLine(ex.Message);
                return 1;
            }

            if (!result.Success)
            {
                stdout.WriteLine("Status: Failed");
                stdout.WriteLine($"Reason: {result.FailureReason}");
                return 1;
            }

            stdout.WriteLine("Status: Success");
            stdout.WriteLine();
            stdout.WriteLine("Output:");
            var outputJson = FlowJsonBridge.ToJsonValue(result.Output);
            stdout.WriteLine(outputJson!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
    }
}
