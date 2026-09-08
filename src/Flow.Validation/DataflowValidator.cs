using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.Validation;

public sealed class DataflowValidator : IWorkflowValidationPass
{
    public string Name => "Dataflow";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        var definedBefore = new HashSet<string> { "input" };

        if (workflow.InputType is ObjectType inputType)
        {
            foreach (var fieldName in inputType.Fields.Keys)
            {
                definedBefore.Add(fieldName);
            }
        }

        ValidateNodes(workflow.Nodes, definedBefore, diagnostics);
        CheckExpression(workflow.Return.Id, CombineReturnFields(workflow), definedBefore, diagnostics);

        return diagnostics;
    }

    private static void ValidateNodes(
        IReadOnlyList<WorkflowNode> nodes, HashSet<string> definedBefore, List<ValidationDiagnostic> diagnostics)
    {
        foreach (var node in nodes)
        {
            if (node is IfNode ifNode)
            {
                CheckExpression(ifNode.Id, ifNode.Condition, definedBefore, diagnostics);
                ValidateBranch(ifNode.TrueBranch, ifNode.Id, definedBefore, diagnostics);
                ValidateBranch(ifNode.FalseBranch, ifNode.Id, definedBefore, diagnostics);
            }
            else if (node is FilterNode filterNode)
            {
                CheckExpression(filterNode.Id, filterNode.Source, definedBefore, diagnostics);
                var scope = new HashSet<string>(definedBefore) { filterNode.ParameterName };
                CheckExpression(filterNode.Id, filterNode.Predicate, scope, diagnostics);
            }
            else if (node is SortNode sortNode)
            {
                CheckExpression(sortNode.Id, sortNode.Source, definedBefore, diagnostics);
                var scope = new HashSet<string>(definedBefore) { sortNode.ParameterName };
                CheckExpression(sortNode.Id, sortNode.Key, scope, diagnostics);
            }
            else if (node is AggregateNode aggregateNode)
            {
                CheckExpression(aggregateNode.Id, aggregateNode.Source, definedBefore, diagnostics);
                if (aggregateNode.Selector is not null && aggregateNode.ParameterName is not null)
                {
                    var scope = new HashSet<string>(definedBefore) { aggregateNode.ParameterName };
                    CheckExpression(aggregateNode.Id, aggregateNode.Selector, scope, diagnostics);
                }
            }
            else
            {
                foreach (var expression in ExpressionsOf(node))
                {
                    CheckExpression(node.Id, expression, definedBefore, diagnostics);
                }
            }

            definedBefore.Add(node.Id);
        }
    }

    private static void ValidateBranch(
        IfBranch branch, string ifNodeId, HashSet<string> outerDefinedBefore, List<ValidationDiagnostic> diagnostics)
    {
        var branchDefined = new HashSet<string>(outerDefinedBefore);
        ValidateNodes(branch.Nodes, branchDefined, diagnostics);
        CheckExpression(ifNodeId, branch.Value, branchDefined, diagnostics);
    }

    private static void CheckExpression(
        string ownerNodeId, FlowExpression expression, HashSet<string> definedBefore, List<ValidationDiagnostic> diagnostics)
    {
        switch (expression)
        {
            case PathExpression path:
                var root = path.Path.Split('.')[0];
                if (root == ownerNodeId)
                    diagnostics.Add(new ValidationDiagnostic("D302", $"'{ownerNodeId}' cannot reference itself in '{path.Path}'.", ownerNodeId));
                else if (!definedBefore.Contains(root))
                    diagnostics.Add(new ValidationDiagnostic("D301", $"'{path.Path}' references undefined value '{root}'.", ownerNodeId));
                break;
            case BinaryExpression binary:
                CheckExpression(ownerNodeId, binary.Left, definedBefore, diagnostics);
                CheckExpression(ownerNodeId, binary.Right, definedBefore, diagnostics);
                break;
        }
    }

    private static IEnumerable<FlowExpression> CombineReturnFields(WorkflowDefinition workflow) => workflow.Return.Fields.Values;

    private static void CheckExpression(
        string ownerNodeId, IEnumerable<FlowExpression> expressions, HashSet<string> definedBefore, List<ValidationDiagnostic> diagnostics)
    {
        foreach (var expression in expressions)
            CheckExpression(ownerNodeId, expression, definedBefore, diagnostics);
    }

    // Extend this switch whenever a new WorkflowNode kind is added.
    private static IEnumerable<FlowExpression> ExpressionsOf(WorkflowNode node) => node switch
    {
        CallNode call => call.Arguments.Values,
        ReturnNode ret => ret.Fields.Values,
        _ => Enumerable.Empty<FlowExpression>()
    };
}
