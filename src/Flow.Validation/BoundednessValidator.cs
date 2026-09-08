using Flow.Contracts;
using Flow.IR;
using Flow.IR.Nodes;

namespace Flow.Validation;

public sealed class BoundednessValidator : IWorkflowValidationPass
{
    public string Name => "Boundedness";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        CheckNodes(workflow.Nodes, diagnostics);
        return diagnostics;
    }

    private static void CheckNodes(IReadOnlyList<WorkflowNode> nodes, List<ValidationDiagnostic> diagnostics)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case ForeachNode foreachNode:
                    if (foreachNode.Limit <= 0)
                    {
                        diagnostics.Add(new ValidationDiagnostic(
                            "B501",
                            $"foreach '{foreachNode.Id}' has no positive limit; unbounded iteration is not allowed.",
                            foreachNode.Id));
                    }
                    CheckNodes(foreachNode.Body, diagnostics);
                    break;

                case IfNode ifNode:
                    CheckNodes(ifNode.TrueBranch.Nodes, diagnostics);
                    CheckNodes(ifNode.FalseBranch.Nodes, diagnostics);
                    break;
            }
        }
    }
}
