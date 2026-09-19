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
        var (prunedNodes, _) = PruneScope(workflow.Nodes, live, tools);
        return workflow with { Nodes = prunedNodes };
    }

    // Returns the kept nodes for this scope, plus the "escaped" names: names this scope's own
    // live set ended up referencing that don't correspond to any node defined in this scope --
    // i.e. references to an outer scope (or "input"), which the caller must fold into its own
    // live set. A branch/loop body may legally reference an outer-scope name (DataflowValidator's
    // ValidateBranch seeds branchDefined from outerDefinedBefore), so that reference has to
    // propagate back out, or the outer node could be wrongly pruned as unreferenced.
    private static (IReadOnlyList<WorkflowNode> Kept, HashSet<string> Escaped) PruneScope(
        IReadOnlyList<WorkflowNode> nodes, HashSet<string> live, ToolCatalog tools)
    {
        var kept = new List<WorkflowNode>();
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            if (IsPrunable(node, tools) && !live.Contains(node.Id))
                continue;

            var (processed, innerEscaped) = ProcessNode(node, tools);
            kept.Add(processed);
            foreach (var name in ReferencedNames(OwnExpressions(processed)))
                live.Add(name);
            live.UnionWith(innerEscaped);
        }
        kept.Reverse();

        var localIds = new HashSet<string>(nodes.Select(n => n.Id));
        var escaped = new HashSet<string>(live.Where(name => !localIds.Contains(name)));
        return (kept, escaped);
    }

    // Recurses into an IfNode's branches / a ForeachNode's body first, so their own dead nodes
    // are pruned using that scope's own exit expression as the seed -- never the outer scope's.
    // Returns names referenced from inside that scope but not defined inside it, so the caller
    // can add them to its own live set (see PruneScope's doc comment).
    private static (WorkflowNode Node, HashSet<string> Escaped) ProcessNode(WorkflowNode node, ToolCatalog tools) => node switch
    {
        IfNode ifNode => ProcessIf(ifNode, tools),
        ForeachNode foreachNode => ProcessForeach(foreachNode, tools),
        _ => (node, new HashSet<string>())
    };

    private static (WorkflowNode, HashSet<string>) ProcessIf(IfNode ifNode, ToolCatalog tools)
    {
        var (trueNodes, trueEscaped) = PruneScope(ifNode.TrueBranch.Nodes, ReferencedNames(new[] { ifNode.TrueBranch.Value }), tools);
        var (falseNodes, falseEscaped) = PruneScope(ifNode.FalseBranch.Nodes, ReferencedNames(new[] { ifNode.FalseBranch.Value }), tools);
        var updated = ifNode with
        {
            TrueBranch = ifNode.TrueBranch with { Nodes = trueNodes },
            FalseBranch = ifNode.FalseBranch with { Nodes = falseNodes }
        };
        var escaped = new HashSet<string>(trueEscaped);
        escaped.UnionWith(falseEscaped);
        return (updated, escaped);
    }

    private static (WorkflowNode, HashSet<string>) ProcessForeach(ForeachNode foreachNode, ToolCatalog tools)
    {
        var (bodyNodes, escaped) = PruneScope(foreachNode.Body, ReferencedNames(new[] { foreachNode.BodyValue }), tools);
        return (foreachNode with { Body = bodyNodes }, escaped);
    }

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
    // ProcessNode/PruneBranch via the returned "escaped" set, not read directly here.
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
