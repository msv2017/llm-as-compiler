using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;

namespace Flow.Validation;

public sealed class ConstantProvenanceValidator : IWorkflowValidationPass
{
    public string Name => "ConstantProvenance";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        CheckNodes(workflow.Nodes, diagnostics);
        foreach (var expression in workflow.Return.Fields.Values)
            CheckExpression(workflow.Return.Id, expression, diagnostics);
        return diagnostics;
    }

    private static void CheckNodes(IReadOnlyList<WorkflowNode> nodes, List<ValidationDiagnostic> diagnostics)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CallNode call:
                    foreach (var expression in call.Arguments.Values)
                        CheckExpression(call.Id, expression, diagnostics);
                    break;
                case IfNode ifNode:
                    CheckExpression(ifNode.Id, ifNode.Condition, diagnostics);
                    CheckNodes(ifNode.TrueBranch.Nodes, diagnostics);
                    CheckExpression(ifNode.Id, ifNode.TrueBranch.Value, diagnostics);
                    CheckNodes(ifNode.FalseBranch.Nodes, diagnostics);
                    CheckExpression(ifNode.Id, ifNode.FalseBranch.Value, diagnostics);
                    break;
                case FilterNode filterNode:
                    CheckExpression(filterNode.Id, filterNode.Source, diagnostics);
                    CheckExpression(filterNode.Id, filterNode.Predicate, diagnostics);
                    break;
                case SortNode sortNode:
                    CheckExpression(sortNode.Id, sortNode.Source, diagnostics);
                    CheckExpression(sortNode.Id, sortNode.Key, diagnostics);
                    break;
                case AggregateNode aggregateNode:
                    CheckExpression(aggregateNode.Id, aggregateNode.Source, diagnostics);
                    if (aggregateNode.Selector is not null)
                        CheckExpression(aggregateNode.Id, aggregateNode.Selector, diagnostics);
                    break;
                case ForeachNode foreachNode:
                    CheckExpression(foreachNode.Id, foreachNode.Source, diagnostics);
                    CheckNodes(foreachNode.Body, diagnostics);
                    CheckExpression(foreachNode.Id, foreachNode.BodyValue, diagnostics);
                    break;
                case AssertNode assertNode:
                    CheckExpression(assertNode.Id, assertNode.Condition, diagnostics);
                    break;
            }
        }
    }

    private static void CheckExpression(string ownerNodeId, FlowExpression expression, List<ValidationDiagnostic> diagnostics)
    {
        switch (expression)
        {
            case ConstantExpression { Provenance.Kind: ConstantProvenanceKind.Unproven } constant:
                diagnostics.Add(new ValidationDiagnostic(
                    "S204",
                    $"Constant '{constant.Value}' has no traceable source (prompt span, tool schema, or policy).",
                    ownerNodeId));
                break;
            case BinaryExpression binary:
                CheckExpression(ownerNodeId, binary.Left, diagnostics);
                CheckExpression(ownerNodeId, binary.Right, diagnostics);
                break;
            case ObjectExpression obj:
                foreach (var fieldExpression in obj.Fields.Values)
                    CheckExpression(ownerNodeId, fieldExpression, diagnostics);
                break;
        }
    }
}
