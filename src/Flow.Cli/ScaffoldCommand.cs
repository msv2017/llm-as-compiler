using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Cli.Runtime;
using Flow.Cli.Scaffold;

namespace Flow.Cli;

public static class ScaffoldCommand
{
    private const string Usage = "Usage: flow-cli scaffold <output.json> --mcp-url <url> --prompt \"<text>\" [--mcp-auth-header <Name>]";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? outputPath = null;
        string? mcpUrl = null;
        string? prompt = null;
        string? authHeaderName = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mcp-url":
                    if (i + 1 >= args.Length) { stderr.WriteLine(Usage); return 2; }
                    mcpUrl = args[++i];
                    break;
                case "--prompt":
                    if (i + 1 >= args.Length) { stderr.WriteLine(Usage); return 2; }
                    prompt = args[++i];
                    break;
                case "--mcp-auth-header":
                    if (i + 1 >= args.Length) { stderr.WriteLine(Usage); return 2; }
                    authHeaderName = args[++i];
                    break;
                default:
                    if (outputPath is null)
                    {
                        outputPath = args[i];
                    }
                    else
                    {
                        stderr.WriteLine(Usage);
                        return 2;
                    }
                    break;
            }
        }

        if (outputPath is null || mcpUrl is null || prompt is null)
        {
            stderr.WriteLine(Usage);
            return 2;
        }

        if (!Uri.TryCreate(mcpUrl, UriKind.Absolute, out var mcpUri))
        {
            stderr.WriteLine($"'{mcpUrl}' is not a valid absolute URL.");
            return 2;
        }

        if (!McpAuthHeaders.TryResolve(authHeaderName, out var authHeaders, out var authError))
        {
            stderr.WriteLine(authError);
            return 2;
        }

        IReadOnlyList<DiscoveredTool> discovered;
        try
        {
            discovered = await McpToolDiscovery.DiscoverAsync(mcpUri, CancellationToken.None, authHeaders);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Could not discover tools from '{mcpUrl}': {ex.Message}");
            return 2;
        }

        var warnings = new List<string>();
        var tools = discovered.Select(tool => ToToolJson(tool, warnings)).ToList();

        var scenario = new ScenarioFileJson
        {
            Prompt = prompt,
            InputType = PlaceholderObject("TODO_ReplaceMe"),
            OutputType = PlaceholderObject("TODO_ReplaceMe"),
            Tools = tools
        };

        try
        {
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(scenario, WriteOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"Could not write '{outputPath}': {ex.Message}");
            return 2;
        }

        stdout.WriteLine($"Wrote {outputPath} with {tools.Count} tool(s) discovered from {mcpUrl}:");
        foreach (var tool in discovered)
            stdout.WriteLine($"  - {tool.Name}{(string.IsNullOrWhiteSpace(tool.Description) ? "" : $": {tool.Description}")}");
        stdout.WriteLine();
        stdout.WriteLine("Needs manual review before this compiles into anything useful:");
        stdout.WriteLine("  - inputType (currently a placeholder)");
        stdout.WriteLine("  - outputType (currently a placeholder)");
        stdout.WriteLine("  - every tool's \"effect\" and \"retry\" (defaulted to Unknown/Never)");
        foreach (var warning in warnings)
            stdout.WriteLine($"  - {warning}");

        return 0;
    }

    internal static ToolJson ToToolJson(DiscoveredTool tool, List<string> warnings)
    {
        var inputType = JsonSchemaToFlowType.Convert(tool.InputSchema, "In", warnings, $"{tool.Name}.inputSchema");
        if (inputType.Kind != "object")
        {
            warnings.Add($"{tool.Name}.inputSchema: top-level schema was not an object; using an empty placeholder object instead.");
            inputType = PlaceholderObject("In");
        }

        FlowTypeJson outputType;
        if (tool.OutputSchema is { } outputSchema)
        {
            outputType = JsonSchemaToFlowType.Convert(outputSchema, "Out", warnings, $"{tool.Name}.outputSchema");
            if (outputType.Kind != "object")
            {
                warnings.Add($"{tool.Name}.outputSchema: top-level schema was not an object; using an empty placeholder object instead.");
                outputType = PlaceholderObject("Out");
            }
        }
        else
        {
            warnings.Add($"{tool.Name}: server reported no outputSchema; using an empty placeholder object -- fill in the tool's actual return shape.");
            outputType = PlaceholderObject("Out");
        }

        return new ToolJson
        {
            Name = tool.Name,
            Effect = "Unknown",
            Retry = "Never",
            InputType = inputType,
            OutputType = outputType
        };
    }

    private static FlowTypeJson PlaceholderObject(string name) =>
        new() { Kind = "object", Name = name, Fields = new Dictionary<string, FlowTypeJson>() };
}
