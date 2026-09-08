using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.Validation;

public sealed class NullabilityValidator : IWorkflowValidationPass
{
    public string Name => "Nullability";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        var nodeOutputTypes = new Dictionary<string, FlowType>();
        var safe = new HashSet<string>();

        ProcessNodes(workflow.Nodes, workflow.InputType, tools, nodeOutputTypes, safe, diagnostics);
        CheckExpressions(workflow.Return.Id, workflow.Return.Fields.Values, workflow.InputType, nodeOutputTypes, safe, diagnostics);

        return diagnostics;
    }

    private static void ProcessNodes(
        IReadOnlyList<WorkflowNode> nodes,
        FlowType inputType,
        ToolCatalog tools,
        Dictionary<string, FlowType> nodeOutputTypes,
        HashSet<string> safe,
        List<ValidationDiagnostic> diagnostics)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CallNode call when tools.TryGet(call.ToolName, out var tool) && tool is not null:
                    CheckExpressions(call.Id, call.Arguments.Values, inputType, nodeOutputTypes, safe, diagnostics);
                    nodeOutputTypes[call.Id] = tool.OutputType;
                    break;

                case IfNode ifNode:
                    CheckExpressions(ifNode.Id, new[] { ifNode.Condition }, inputType, nodeOutputTypes, safe, diagnostics);

                    var (trueSafeAddition, falseSafeAddition) = GuardedRoot(ifNode.Condition);

                    var trueSafe = new HashSet<string>(safe);
                    if (trueSafeAddition is not null) trueSafe.Add(trueSafeAddition);
                    ProcessNodes(ifNode.TrueBranch.Nodes, inputType, tools, nodeOutputTypes, trueSafe, diagnostics);
                    CheckExpressions(ifNode.Id, new[] { ifNode.TrueBranch.Value }, inputType, nodeOutputTypes, trueSafe, diagnostics);

                    var falseSafe = new HashSet<string>(safe);
                    if (falseSafeAddition is not null) falseSafe.Add(falseSafeAddition);
                    ProcessNodes(ifNode.FalseBranch.Nodes, inputType, tools, nodeOutputTypes, falseSafe, diagnostics);
                    CheckExpressions(ifNode.Id, new[] { ifNode.FalseBranch.Value }, inputType, nodeOutputTypes, falseSafe, diagnostics);
                    break;
            }
        }
    }

    // Recognizes `path == null` / `null == path` / `path != null` / `null != path` and returns
    // (idMadeSafeInTrueBranch, idMadeSafeInFalseBranch) — the root identifier of `path` on whichever side.
    private static (string? TrueSafe, string? FalseSafe) GuardedRoot(FlowExpression condition)
    {
        if (condition is not BinaryExpression { Operator: BinaryOperator.Equal or BinaryOperator.NotEqual } binary)
            return (null, null);

        string? root = (binary.Left, binary.Right) switch
        {
            (PathExpression p, ConstantExpression { Value: null }) => p.Path.Split('.')[0],
            (ConstantExpression { Value: null }, PathExpression p) => p.Path.Split('.')[0],
            _ => null
        };

        if (root is null)
            return (null, null);

        return binary.Operator == BinaryOperator.Equal ? (null, root) : (root, null);
    }

    private static void CheckExpressions(
        string ownerNodeId,
        IEnumerable<FlowExpression> expressions,
        FlowType inputType,
        IReadOnlyDictionary<string, FlowType> nodeOutputTypes,
        HashSet<string> safe,
        List<ValidationDiagnostic> diagnostics)
    {
        foreach (var expression in expressions)
            CheckExpression(ownerNodeId, expression, inputType, nodeOutputTypes, safe, diagnostics);
    }

    private static void CheckExpression(
        string ownerNodeId,
        FlowExpression expression,
        FlowType inputType,
        IReadOnlyDictionary<string, FlowType> nodeOutputTypes,
        HashSet<string> safe,
        List<ValidationDiagnostic> diagnostics)
    {
        switch (expression)
        {
            case PathExpression path:
                var segments = path.Path.Split('.');
                if (segments.Length > 1 && !safe.Contains(segments[0]))
                {
                    PathTypeResolver.Resolve(path.Path, inputType, nodeOutputTypes, out var passedThroughUnwrappedOptional);
                    if (passedThroughUnwrappedOptional)
                    {
                        diagnostics.Add(new ValidationDiagnostic(
                            "T103",
                            $"'{segments[0]}' may be null; '{path.Path}' cannot be accessed until the workflow proves it is not null.",
                            ownerNodeId));
                    }
                }
                break;
            case BinaryExpression binary:
                CheckExpression(ownerNodeId, binary.Left, inputType, nodeOutputTypes, safe, diagnostics);
                CheckExpression(ownerNodeId, binary.Right, inputType, nodeOutputTypes, safe, diagnostics);
                break;
            case ObjectExpression obj:
                foreach (var fieldExpression in obj.Fields.Values)
                    CheckExpression(ownerNodeId, fieldExpression, inputType, nodeOutputTypes, safe, diagnostics);
                break;
        }
    }
}
