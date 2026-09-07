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

        foreach (var node in workflow.Nodes.OfType<CallNode>())
        {
            if (!tools.TryGet(node.ToolName, out var tool) || tool is null)
            {
                diagnostics.Add(new ValidationDiagnostic("F001", $"Unknown tool '{node.ToolName}'.", node.Id));
                continue;
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

        return diagnostics;
    }
}
