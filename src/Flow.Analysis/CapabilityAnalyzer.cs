using Flow.IR;
using Flow.IR.Nodes;

namespace Flow.Analysis;

public static class CapabilityAnalyzer
{
    public static IReadOnlySet<string> Analyze(WorkflowDefinition workflow)
    {
        var tools = new HashSet<string>();
        CollectFrom(workflow.Nodes, tools);
        return tools;
    }

    private static void CollectFrom(IReadOnlyList<WorkflowNode> nodes, HashSet<string> tools)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CallNode call:
                    tools.Add(call.ToolName);
                    break;
                case IfNode ifNode:
                    CollectFrom(ifNode.TrueBranch.Nodes, tools);
                    CollectFrom(ifNode.FalseBranch.Nodes, tools);
                    break;
                case ForeachNode foreachNode:
                    CollectFrom(foreachNode.Body, tools);
                    break;
            }
        }
    }
}
