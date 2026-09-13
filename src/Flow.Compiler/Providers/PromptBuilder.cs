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
          instead of guessing. Comparing a field against a literal value the prompt itself states (e.g.
          "keep only invoices with status UNPAID") is NOT this kind of ambiguity -- it is a normal
          PROMPT-sourced constant comparison, satisfying the "source" rule above, and does not need an
          "unresolved" entry just because the field's full set of possible values isn't documented
          elsewhere. Reserve "unresolved" for judgment calls the prompt does not itself resolve (e.g.
          "the most appropriate account", "sounds angry").
        - Record any non-obvious interpretation you made in "assumptions".
        - Only use tools and fields that appear in the catalog and type descriptions below.
        - Path syntax: a field on the workflow's input MUST be written as "input.fieldName" -- never as a
          bare "fieldName". A field on a prior node's result is written as "nodeId.fieldName". For example,
          if the input type has a field "customerId", refer to it as "input.customerId", and if a call node
          with id "customer" produces a field "name", refer to it as "customer.name".
        - Paths never index into a list by position -- there is no "someList.0.field" syntax. To get a
          single item from a list (e.g. "the first" or "the oldest"), use an aggregate node with operation
          "First". Its result may be absent (the list could be empty), so you cannot access a field on it
          directly. Prove it is present first with an if node whose condition is exactly
          "oldestInvoice != null" (a bare not-null/null comparison against that exact path, with no "&&" or
          "||" combining it with anything else) -- only inside that branch can you write "oldestInvoice.id".
          An assert node does NOT prove a value non-null for later steps; only an if node's own bare
          "!= null"/"== null" condition does, and only within that specific branch. To act on every item,
          use a foreach node instead.
        - A node's result is referenced by its own bare id (e.g. "refundDecision"), never with a ".value"
          suffix -- "value" is only the key inside an if-branch's own JSON definition (the expression that
          branch produces), not something you write when referring to that node from elsewhere.
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

    private static string DescribeType(FlowType type, int depth = 0) => type switch
    {
        ObjectType obj when depth < 2 =>
            $"{obj.Name} {{ {string.Join(", ", obj.Fields.Select(f => $"{f.Key}: {DescribeType(f.Value, depth + 1)}"))} }}",
        ListType list when depth < 2 => $"List<{DescribeType(list.ElementType, depth + 1)}>",
        _ => type.DisplayName
    };

    private static string DescribeTool(ToolDefinition tool) =>
        $"- {tool.Name}({string.Join(", ", tool.InputType.Fields.Select(f => $"{f.Key}: {f.Value.DisplayName}"))}) " +
        $"-> {DescribeType(tool.OutputType)} [{tool.Effect}]";
}
