using Flow.Compiler;
using Flow.Compiler.Providers;

namespace Flow.Cli;

public static class CliRunner
{
    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length != 1)
        {
            stderr.WriteLine("Usage: flow-cli <scenario.json>");
            return 2;
        }

        var path = args[0];
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

        OpenAiSemanticCompilerModel model;
        try
        {
            model = OpenAiSemanticCompilerModel.FromEnvironment();
        }
        catch (InvalidOperationException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }

        var compiler = new PromptCompiler(model);
        var result = await compiler.CompileAsync(source, tools);

        WriteResult(result, stdout);
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
