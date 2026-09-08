using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class ForeachAndAssertValidationTests
{
    private static readonly ToolCatalog EmptyCatalog = new(Array.Empty<ToolDefinition>());
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);
    private static readonly ObjectType CustomerType = new("Customer", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });

    [Fact]
    public void Dataflow_BodyValueReferencingBodyLocalCall_ProducesNoDiagnostics()
    {
        var body = new WorkflowNode[]
        {
            new CallNode("balance", "billing.getBalance",
                new Dictionary<string, FlowExpression> { ["customerId"] = new PathExpression("customer.id") })
        };
        var foreachNode = new ForeachNode("balances", new PathExpression("input.customers"), "customer", 100, body, new PathExpression("balance"));
        var diagnostics = new DataflowValidator().Validate(WorkflowWith(foreachNode), EmptyCatalog);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Dataflow_BodyValueReferencingUndefinedId_ProducesD301()
    {
        var foreachNode = new ForeachNode(
            "balances", new PathExpression("input.customers"), "customer", 100, Array.Empty<WorkflowNode>(), new PathExpression("doesNotExist"));
        var diagnostics = new DataflowValidator().Validate(WorkflowWith(foreachNode), EmptyCatalog);

        Assert.Contains(diagnostics, d => d.Code == "D301");
    }

    [Fact]
    public void Type_AssertConditionNotBool_ProducesT101()
    {
        var assertNode = new AssertNode("check", new ConstantExpression(1, Provenance), "SOME_CODE");
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new PathExpression("check") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["ok"] = PrimitiveType.Bool });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { assertNode }, returnNode);

        var diagnostics = new TypeValidator().Validate(workflow, EmptyCatalog);

        Assert.Contains(diagnostics, d => d.Code == "T101");
    }

    private static WorkflowDefinition WorkflowWith(ForeachNode foreachNode)
    {
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["result"] = new PathExpression(foreachNode.Id) });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customers"] = new ListType(CustomerType) });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["result"] = new ListType(PrimitiveType.String) });
        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { foreachNode }, returnNode);
    }
}
