using Flow.Compiler;
using Flow.Compiler.Providers;

namespace Flow.Cli;

public static class CompileCommand
{
    private const string Usage = "Usage: flow-cli compile <scenario.json> [--provider openai|anthropic] [--save <path>]";

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? path = null;
        string provider = "openai";
        string? savePath = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--provider")
            {
                if (i + 1 >= args.Length)
                {
                    stderr.WriteLine(Usage);
                    return 2;
                }
                provider = args[++i];
            }
            else if (args[i] == "--save")
            {
                if (i + 1 >= args.Length)
                {
                    stderr.WriteLine(Usage);
                    return 2;
                }
                savePath = args[++i];
            }
            else if (path is null)
            {
                path = args[i];
            }
            else
            {
                stderr.WriteLine(Usage);
                return 2;
            }
        }

        if (path is null)
        {
            stderr.WriteLine(Usage);
            return 2;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"Could not read '{path}': {ex.Message}");
            return 2;
        }

        WorkflowSource source;
        Flow.Contracts.ToolCatalog tools;
        try
        {
            (source, tools) = ScenarioJson.Parse(json);
        }
        catch (ScenarioParseException ex)
        {
            stderr.WriteLine($"Invalid scenario file: {ex.Message}");
            return 2;
        }

        ISemanticCompilerModel model;
        try
        {
            model = provider.ToLowerInvariant() switch
            {
                "openai" => OpenAiSemanticCompilerModel.FromEnvironment(),
                "anthropic" => AnthropicSemanticCompilerModel.FromEnvironment(),
                _ => throw new InvalidOperationException($"Unknown provider '{provider}'. Expected 'openai' or 'anthropic'.")
            };
        }
        catch (InvalidOperationException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }

        var compiler = new PromptCompiler(model);
        var result = await compiler.CompileAsync(source, tools);

        WriteResult(result, stdout);

        if (savePath is not null && result.Status == CompilationStatus.Success)
        {
            try
            {
                await File.WriteAllTextAsync(savePath, CompiledWorkflowJson.Write(result.Workflow!));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                stderr.WriteLine($"Could not write '{savePath}': {ex.Message}");
                return 2;
            }
        }

        return result.Status == CompilationStatus.Success ? 0 : 1;
    }

    private static void WriteResult(CompilationResult result, TextWriter stdout)
    {
        stdout.WriteLine($"Status: {result.Status}");

        if (result.Interpretations.Count > 0)
        {
            stdout.WriteLine();
            stdout.WriteLine("Interpretations:");
            foreach (var interpretation in result.Interpretations)
                stdout.WriteLine($"  - {interpretation}");
        }

        if (result.Assumptions.Count > 0)
        {
            stdout.WriteLine();
            stdout.WriteLine("Assumptions:");
            foreach (var assumption in result.Assumptions)
                stdout.WriteLine($"  - {assumption.Description}");
        }

        if (result.Diagnostics.Count > 0)
        {
            stdout.WriteLine();
            stdout.WriteLine("Diagnostics:");
            foreach (var diagnostic in result.Diagnostics)
                stdout.WriteLine($"  - [{diagnostic.Code}] {(diagnostic.NodeId is null ? "" : $"{diagnostic.NodeId}: ")}{diagnostic.Message}");
        }

        if (result.Unresolved.Count > 0)
        {
            stdout.WriteLine();
            stdout.WriteLine("Unresolved:");
            foreach (var unresolved in result.Unresolved)
                stdout.WriteLine($"  - [{unresolved.Code}] {unresolved.Description}");
        }

        if (result.Workflow is not null)
        {
            stdout.WriteLine();
            stdout.WriteLine("Workflow:");
            stdout.Write(WorkflowPrinter.Print(result.Workflow));
        }
    }
}
