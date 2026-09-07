using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;

namespace Flow.Validation;

public sealed class DataflowValidator : IWorkflowValidationPass
{
    public string Name => "Dataflow";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        var definedBefore = new HashSet<string> { "input" };

        foreach (var node in workflow.Nodes)
        {
            CheckExpressions(node.Id, ExpressionsOf(node), definedBefore, diagnostics);
            definedBefore.Add(node.Id);
        }

        CheckExpressions(workflow.Return.Id, ExpressionsOf(workflow.Return), definedBefore, diagnostics);

        return diagnostics;
    }

    private static void CheckExpressions(
        string ownerNodeId,
        IEnumerable<FlowExpression> expressions,
        HashSet<string> definedBefore,
        List<ValidationDiagnostic> diagnostics)
    {
        foreach (var expression in expressions)
        {
            if (expression is not PathExpression path)
                continue;

            var root = path.Path.Split('.')[0];

            if (root == ownerNodeId)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    "D302", $"'{ownerNodeId}' cannot reference itself in '{path.Path}'.", ownerNodeId));
            }
            else if (!definedBefore.Contains(root))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    "D301", $"'{path.Path}' references undefined value '{root}'.", ownerNodeId));
            }
        }
    }

    // Extend this switch whenever a new WorkflowNode kind is added.
    private static IEnumerable<FlowExpression> ExpressionsOf(WorkflowNode node) => node switch
    {
        CallNode call => call.Arguments.Values,
        ReturnNode ret => ret.Fields.Values,
        _ => Enumerable.Empty<FlowExpression>()
    };
}
