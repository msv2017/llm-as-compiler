using Flow.Contracts;
using Flow.IR;
using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.Validation;

public sealed class ToolResolutionValidator : IWorkflowValidationPass
{
    public string Name => "ToolResolution";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        CheckNodes(workflow.Nodes, tools, diagnostics);
        return diagnostics;
    }

    // Recurses into ForeachNode.Body and IfNode branches, matching DataflowValidator/TypeValidator/
    // BoundednessValidator — a CallNode nested inside a bounded loop or a branch is just as resolvable
    // against the tool catalog as a top-level one.
    private static void CheckNodes(IReadOnlyList<WorkflowNode> nodes, ToolCatalog tools, List<ValidationDiagnostic> diagnostics)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CallNode call:
                    CheckCall(call, tools, diagnostics);
                    break;

                case ForeachNode foreachNode:
                    CheckNodes(foreachNode.Body, tools, diagnostics);
                    break;

                case IfNode ifNode:
                    CheckNodes(ifNode.TrueBranch.Nodes, tools, diagnostics);
                    CheckNodes(ifNode.FalseBranch.Nodes, tools, diagnostics);
                    break;
            }
        }
    }

    private static void CheckCall(CallNode node, ToolCatalog tools, List<ValidationDiagnostic> diagnostics)
    {
        if (!tools.TryGet(node.ToolName, out var tool) || tool is null)
        {
            diagnostics.Add(new ValidationDiagnostic("F001", $"Unknown tool '{node.ToolName}'.", node.Id));
            return;
        }

        foreach (var argumentName in node.Arguments.Keys)
        {
            if (!tool.InputType.Fields.ContainsKey(argumentName))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    "F002", $"Tool '{node.ToolName}' has no argument named '{argumentName}'.", node.Id));
            }
        }

        foreach (var (fieldName, fieldType) in tool.InputType.Fields)
        {
            var isRequired = fieldType is not OptionalType;
            if (isRequired && !node.Arguments.ContainsKey(fieldName))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    "F003", $"Missing required argument '{fieldName}' for tool '{node.ToolName}'.", node.Id));
            }
        }
    }
}
