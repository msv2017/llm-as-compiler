using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.Compiler.Tests;

public class DeadNodeEliminatorTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    private static ObjectType RequestType => new("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String });

    private static ObjectType ResultType(params (string Name, FlowType Type)[] fields) =>
        new("Result", fields.ToDictionary(f => f.Name, f => f.Type));

    private static ToolCatalog Catalog(params ToolDefinition[] tools) => new(tools);

    private static ToolDefinition Tool(string name, ToolEffect effect) =>
        new(name,
            new ObjectType("In", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            new ObjectType("Out", new Dictionary<string, FlowType> { ["value"] = PrimitiveType.String }),
            effect, ToolRetryPolicy.Safe);

    [Fact]
    public void RemovesTrailingUnusedAggregate()
    {
        // Matches the real examples/07-bounded-foreach/workflow.json shape: an unused Count
        // aggregate alongside the one actually wired to the return.
        var listCall = new CallNode("items", "demo.list",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var usedCount = new AggregateNode("usedCount", new PathExpression("items"), AggregateOperation.Count, null, null);
        var deadCount = new AggregateNode("deadCount", new PathExpression("items"), AggregateOperation.Count, null, null);
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["count"] = new PathExpression("usedCount") });
        var workflow = new WorkflowDefinition("Test", RequestType, ResultType(("count", PrimitiveType.Int)),
            new WorkflowNode[] { listCall, usedCount, deadCount }, returnNode);
        var tools = Catalog(Tool("demo.list", ToolEffect.Read));

        var pruned = DeadNodeEliminator.Eliminate(workflow, tools);

        Assert.Equal(new[] { "items", "usedCount" }, pruned.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void RemovesAChainOfUnusedNodes()
    {
        var listCall = new CallNode("items", "demo.list",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var deadSort = new SortNode("deadSorted", new PathExpression("items"), "x", new PathExpression("x"), SortDirection.Ascending);
        var deadCount = new AggregateNode("deadCount", new PathExpression("deadSorted"), AggregateOperation.Count, null, null);
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["itemIds"] = new PathExpression("items") });
        var workflow = new WorkflowDefinition("Test", RequestType, ResultType(("itemIds", new ListType(PrimitiveType.String))),
            new WorkflowNode[] { listCall, deadSort, deadCount }, returnNode);
        var tools = Catalog(Tool("demo.list", ToolEffect.Read));

        var pruned = DeadNodeEliminator.Eliminate(workflow, tools);

        Assert.Equal(new[] { "items" }, pruned.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void RemovesUnusedReadEffectCall()
    {
        var deadCall = new CallNode("unused", "demo.read",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new ConstantExpression(true, Provenance) });
        var workflow = new WorkflowDefinition("Test", RequestType, ResultType(("ok", PrimitiveType.Bool)),
            new WorkflowNode[] { deadCall }, returnNode);
        var tools = Catalog(Tool("demo.read", ToolEffect.Read));

        var pruned = DeadNodeEliminator.Eliminate(workflow, tools);

        Assert.Empty(pruned.Nodes);
    }

    [Fact]
    public void KeepsUnusedWriteEffectCall()
    {
        var writeCall = new CallNode("unused", "demo.write",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new ConstantExpression(true, Provenance) });
        var workflow = new WorkflowDefinition("Test", RequestType, ResultType(("ok", PrimitiveType.Bool)),
            new WorkflowNode[] { writeCall }, returnNode);
        var tools = Catalog(Tool("demo.write", ToolEffect.Write));

        var pruned = DeadNodeEliminator.Eliminate(workflow, tools);

        Assert.Equal(new[] { "unused" }, pruned.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void KeepsUnusedUnknownEffectCall()
    {
        var unknownCall = new CallNode("unused", "demo.unknown",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new ConstantExpression(true, Provenance) });
        var workflow = new WorkflowDefinition("Test", RequestType, ResultType(("ok", PrimitiveType.Bool)),
            new WorkflowNode[] { unknownCall }, returnNode);
        var tools = Catalog(Tool("demo.unknown", ToolEffect.Unknown));

        var pruned = DeadNodeEliminator.Eliminate(workflow, tools);

        Assert.Equal(new[] { "unused" }, pruned.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void KeepsUnusedAssert()
    {
        var assertNode = new AssertNode("guard", new ConstantExpression(true, Provenance), "SOME_CODE");
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new ConstantExpression(true, Provenance) });
        var workflow = new WorkflowDefinition("Test", RequestType, ResultType(("ok", PrimitiveType.Bool)),
            new WorkflowNode[] { assertNode }, returnNode);
        var tools = Catalog();

        var pruned = DeadNodeEliminator.Eliminate(workflow, tools);

        Assert.Equal(new[] { "guard" }, pruned.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void KeepsUnusedIfAndForeachContainersEvenWithFullyPrunableInsides()
    {
        // Both branches/body contain only prunable nodes, and nothing outside references
        // "guard" or "loop" -- both containers are still kept in full, per the documented scope
        // boundary (no whole-subtree side-effect analysis).
        var deadInTrue = new AggregateNode("deadInTrue", new PathExpression("input.customerId"), AggregateOperation.Count, null, null);
        var ifNode = new IfNode(
            "guard",
            new ConstantExpression(true, Provenance),
            new IfBranch(new WorkflowNode[] { deadInTrue }, new ConstantExpression(1, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(0, Provenance)));

        var deadInBody = new AggregateNode("deadInBody", new PathExpression("input.customerId"), AggregateOperation.Count, null, null);
        var foreachNode = new ForeachNode(
            "loop", new PathExpression("input.customerId"), "x", 20,
            new WorkflowNode[] { deadInBody }, new ConstantExpression(1, Provenance));

        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new ConstantExpression(true, Provenance) });
        var workflow = new WorkflowDefinition("Test", RequestType, ResultType(("ok", PrimitiveType.Bool)),
            new WorkflowNode[] { ifNode, foreachNode }, returnNode);
        var tools = Catalog();

        var pruned = DeadNodeEliminator.Eliminate(workflow, tools);

        Assert.Equal(new[] { "guard", "loop" }, pruned.Nodes.Select(n => n.Id));
        var prunedIf = Assert.IsType<IfNode>(pruned.Nodes[0]);
        Assert.Empty(prunedIf.TrueBranch.Nodes);
        var prunedForeach = Assert.IsType<ForeachNode>(pruned.Nodes[1]);
        Assert.Empty(prunedForeach.Body);
    }

    [Fact]
    public void RemovesDeadNodeInsideOneIfBranchWithoutAffectingSiblingBranchOrOuterScope()
    {
        var deadInTrue = new AggregateNode("deadInTrue", new PathExpression("input.customerId"), AggregateOperation.Count, null, null);
        var liveInFalse = new AggregateNode("liveInFalse", new PathExpression("input.customerId"), AggregateOperation.Count, null, null);
        var ifNode = new IfNode(
            "guard",
            new ConstantExpression(true, Provenance),
            new IfBranch(new WorkflowNode[] { deadInTrue }, new ConstantExpression(1, Provenance)),
            new IfBranch(new WorkflowNode[] { liveInFalse }, new PathExpression("liveInFalse")));

        var outerLive = new AggregateNode("outerLive", new PathExpression("input.customerId"), AggregateOperation.Count, null, null);
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["fromIf"] = new PathExpression("guard"),
            ["fromOuter"] = new PathExpression("outerLive")
        });
        var workflow = new WorkflowDefinition("Test", RequestType,
            ResultType(("fromIf", PrimitiveType.Int), ("fromOuter", PrimitiveType.Int)),
            new WorkflowNode[] { ifNode, outerLive }, returnNode);
        var tools = Catalog();

        var pruned = DeadNodeEliminator.Eliminate(workflow, tools);

        Assert.Equal(new[] { "guard", "outerLive" }, pruned.Nodes.Select(n => n.Id));
        var prunedIf = Assert.IsType<IfNode>(pruned.Nodes[0]);
        Assert.Empty(prunedIf.TrueBranch.Nodes);
        Assert.Equal(new[] { "liveInFalse" }, prunedIf.FalseBranch.Nodes.Select(n => n.Id));
    }

    [Fact]
    public void NoOpWhenNothingIsPrunable()
    {
        var call = new CallNode("customer", "demo.write",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new ConstantExpression(true, Provenance) });
        var workflow = new WorkflowDefinition("Test", RequestType, ResultType(("ok", PrimitiveType.Bool)),
            new WorkflowNode[] { call }, returnNode);
        var tools = Catalog(Tool("demo.write", ToolEffect.Write));

        var pruned = DeadNodeEliminator.Eliminate(workflow, tools);

        Assert.Equal(new[] { "customer" }, pruned.Nodes.Select(n => n.Id));
        var keptCall = Assert.IsType<CallNode>(pruned.Nodes[0]);
        Assert.Equal("demo.write", keptCall.ToolName);
    }
}
