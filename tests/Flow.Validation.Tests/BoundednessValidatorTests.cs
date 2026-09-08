using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class BoundednessValidatorTests
{
    private static readonly ToolCatalog EmptyCatalog = new(Array.Empty<ToolDefinition>());

    [Fact]
    public void ZeroLimit_ProducesB501()
    {
        var foreachNode = new ForeachNode(
            "balances", new PathExpression("input.customers"), "customer", 0, Array.Empty<WorkflowNode>(), new PathExpression("customer"));
        var diagnostics = new BoundednessValidator().Validate(WorkflowWith(foreachNode), EmptyCatalog);

        Assert.Contains(diagnostics, d => d.Code == "B501");
    }

    [Fact]
    public void PositiveLimit_ProducesNoDiagnostics()
    {
        var foreachNode = new ForeachNode(
            "balances", new PathExpression("input.customers"), "customer", 100, Array.Empty<WorkflowNode>(), new PathExpression("customer"));
        var diagnostics = new BoundednessValidator().Validate(WorkflowWith(foreachNode), EmptyCatalog);

        Assert.Empty(diagnostics);
    }

    private static WorkflowDefinition WorkflowWith(ForeachNode foreachNode)
    {
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["result"] = new PathExpression(foreachNode.Id) });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customers"] = new ListType(PrimitiveType.String) });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["result"] = new ListType(PrimitiveType.String) });
        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { foreachNode }, returnNode);
    }
}
