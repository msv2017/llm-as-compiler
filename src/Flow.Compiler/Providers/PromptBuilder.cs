using System.Text;
using System.Text.Json;
using Flow.Compiler.Candidate;
using Flow.Contracts;
using Flow.TypeSystem;
using Flow.Validation;

namespace Flow.Compiler.Providers;

public static class PromptBuilder
{
    private const string SystemPrompt = """
        You are a deterministic workflow compiler. Given a natural-language prompt, an input type, an
        output type, and a catalog of available MCP tools, produce a candidate workflow as a JSON object
        matching the provided schema exactly.

        Rules:
        - Every constant you introduce must carry a "source" of PROMPT, TOOL_SCHEMA, SYSTEM_POLICY, or
          EXPLICIT_COMPILER_POLICY, with a "detail" explaining where it came from. Never invent a business
          rule or a magic number without a traceable source.
        - If the prompt is ambiguous or requires a judgment call the given inputs cannot resolve
          deterministically, add an entry to "unresolved" describing exactly what is unresolved and why,
          instead of guessing.
        - Record any non-obvious interpretation you made in "assumptions".
        - Only use tools and fields that appear in the catalog and type descriptions below.
        """;

    public static (string System, string User) BuildGeneratePrompt(WorkflowSource source, ToolCatalog tools)
    {
        var user = new StringBuilder();
        user.AppendLine("Prompt:");
        user.AppendLine(source.Prompt);
        user.AppendLine();
        user.AppendLine("Input type:");
        user.AppendLine(DescribeType(source.InputType));
        user.AppendLine();
        user.AppendLine("Output type:");
        user.AppendLine(DescribeType(source.OutputType));
        user.AppendLine();
        user.AppendLine("Available tools:");
        foreach (var tool in tools.Tools)
        {
            user.AppendLine(DescribeTool(tool));
        }
        return (SystemPrompt, user.ToString());
    }

    public static (string System, string User) BuildRepairPrompt(
        WorkflowSource source, ToolCatalog tools, CandidateWorkflowAst priorCandidate, IReadOnlyList<ValidationDiagnostic> diagnostics)
    {
        var (system, generateUser) = BuildGeneratePrompt(source, tools);

        var user = new StringBuilder();
        user.AppendLine(generateUser);
        user.AppendLine();
        user.AppendLine("Your previous candidate failed validation:");
        user.AppendLine(JsonSerializer.Serialize(priorCandidate, CandidateJson.Options));
        user.AppendLine();
        user.AppendLine("Diagnostics (fix all of these):");
        foreach (var diagnostic in diagnostics)
        {
            user.AppendLine($"{diagnostic.Code} at '{diagnostic.NodeId ?? "(workflow)"}': {diagnostic.Message}");
        }
        return (system, user.ToString());
    }

    private static string DescribeType(FlowType type) => type switch
    {
        ObjectType obj => $"{obj.Name} {{ {string.Join(", ", obj.Fields.Select(f => $"{f.Key}: {f.Value.DisplayName}"))} }}",
        _ => type.DisplayName
    };

    private static string DescribeTool(ToolDefinition tool) =>
        $"- {tool.Name}({string.Join(", ", tool.InputType.Fields.Select(f => $"{f.Key}: {f.Value.DisplayName}"))}) " +
        $"-> {tool.OutputType.DisplayName} [{tool.Effect}]";
}
