using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Xunit;

namespace Flow.IR.Tests;

public class ForeachAndAssertNodeTests
{
    [Fact]
    public void ForeachNode_StoresLimitBodyAndBodyValue()
    {
        var body = new WorkflowNode[]
        {
            new CallNode("balance", "billing.getBalance",
                new Dictionary<string, FlowExpression> { ["customerId"] = new PathExpression("customer.id") })
        };
        var node = new ForeachNode("balances", new PathExpression("customers"), "customer", 100, body, new PathExpression("balance"));

        Assert.Equal(100, node.Limit);
        Assert.Single(node.Body);
        Assert.Equal("customer", node.ParameterName);
    }

    [Fact]
    public void AssertNode_StoresConditionAndFailureCode()
    {
        var provenance = new ConstantProvenance(ConstantProvenanceKind.HandWritten);
        var node = new AssertNode("totalNonNegative",
            new BinaryExpression(new PathExpression("total"), BinaryOperator.GreaterThanOrEqual, new ConstantExpression(0m, provenance)),
            "INVALID_TOTAL");

        Assert.Equal("INVALID_TOTAL", node.FailureCode);
    }
}
