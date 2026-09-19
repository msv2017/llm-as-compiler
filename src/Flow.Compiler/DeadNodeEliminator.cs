using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;

namespace Flow.Compiler;

public static class DeadNodeEliminator
{
    public static WorkflowDefinition Eliminate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var live = ReferencedNames(workflow.Return.Fields.Values);
        var prunedNodes = PruneScope(workflow.Nodes, live, tools);
        return workflow with { Nodes = prunedNodes };
    }

    private static IReadOnlyList<WorkflowNode> PruneScope(
        IReadOnlyList<WorkflowNode> nodes, HashSet<string> live, ToolCatalog tools)
    {
        var kept = new List<WorkflowNode>();
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            if (IsPrunable(node, tools) && !live.Contains(node.Id))
                continue;

            var processed = ProcessNode(node, tools);
            kept.Add(processed);
            foreach (var name in ReferencedNames(OwnExpressions(processed)))
                live.Add(name);
        }
        kept.Reverse();
        return kept;
    }

    // Recurses into an IfNode's branches / a ForeachNode's body first, so their own dead nodes
    // are pruned using that scope's own exit expression as the seed -- never the outer scope's.
    private static WorkflowNode ProcessNode(WorkflowNode node, ToolCatalog tools) => node switch
    {
        IfNode ifNode => ifNode with
        {
            TrueBranch = PruneBranch(ifNode.TrueBranch, tools),
            FalseBranch = PruneBranch(ifNode.FalseBranch, tools)
        },
        ForeachNode foreachNode => foreachNode with
        {
            Body = PruneScope(foreachNode.Body, ReferencedNames(new[] { foreachNode.BodyValue }), tools)
        },
        _ => node
    };

    private static IfBranch PruneBranch(IfBranch branch, ToolCatalog tools) =>
        branch with { Nodes = PruneScope(branch.Nodes, ReferencedNames(new[] { branch.Value }), tools) };

    // Extend this switch whenever a new WorkflowNode kind is added -- default false (never prune)
    // is the safe choice for anything not explicitly known to be side-effect-free.
    private static bool IsPrunable(WorkflowNode node, ToolCatalog tools) => node switch
    {
        FilterNode or SortNode or AggregateNode => true,
        CallNode call => tools.TryGet(call.ToolName, out var tool) &&
            tool is { Effect: ToolEffect.Pure or ToolEffect.Read },
        _ => false
    };

    // Extend this switch whenever a new WorkflowNode kind is added. Deliberately excludes
    // IfNode.TrueBranch/FalseBranch and ForeachNode.Body -- those are separate scopes, handled by
    // ProcessNode/PruneBranch, not the outer scope's liveness set.
    private static IEnumerable<FlowExpression> OwnExpressions(WorkflowNode node) => node switch
    {
        CallNode call => call.Arguments.Values,
        FilterNode filter => new[] { filter.Source, filter.Predicate },
        SortNode sort => new[] { sort.Source, sort.Key },
        AggregateNode aggregate => aggregate.Selector is not null
            ? new[] { aggregate.Source, aggregate.Selector }
            : new[] { aggregate.Source },
        AssertNode assert => new[] { assert.Condition },
        IfNode ifNode => new[] { ifNode.Condition },
        ForeachNode foreachNode => new[] { foreachNode.Source },
        ReturnNode ret => ret.Fields.Values,
        _ => Enumerable.Empty<FlowExpression>()
    };

    private static HashSet<string> ReferencedNames(IEnumerable<FlowExpression> expressions)
    {
        var names = new HashSet<string>();
        foreach (var expression in expressions)
            CollectNames(expression, names);
        return names;
    }

    private static void CollectNames(FlowExpression expression, HashSet<string> names)
    {
        switch (expression)
        {
            case PathExpression path:
                names.Add(path.Path.Split('.')[0]);
                break;
            case BinaryExpression binary:
                CollectNames(binary.Left, names);
                CollectNames(binary.Right, names);
                break;
            case ObjectExpression obj:
                foreach (var field in obj.Fields.Values)
                    CollectNames(field, names);
                break;
        }
    }
}
