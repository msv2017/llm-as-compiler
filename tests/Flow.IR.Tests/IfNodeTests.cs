using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Xunit;

namespace Flow.IR.Tests;

public class IfNodeTests
{
    [Fact]
    public void IfNode_StoresConditionAndBranches()
    {
        var provenance = new ConstantProvenance(ConstantProvenanceKind.HandWritten);
        var condition = new BinaryExpression(new PathExpression("customer"), BinaryOperator.Equal, new ConstantExpression(null, provenance));
        var trueBranch = new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("", provenance));
        var falseBranch = new IfBranch(Array.Empty<WorkflowNode>(), new PathExpression("customer.name"));

        var node = new IfNode("customerName", condition, trueBranch, falseBranch);

        Assert.Equal("customerName", node.Id);
        Assert.Same(trueBranch, node.TrueBranch);
        Assert.Same(falseBranch, node.FalseBranch);
    }
}
