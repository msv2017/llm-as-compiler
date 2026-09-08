using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Xunit;

namespace Flow.IR.Tests;

public class CollectionNodeTests
{
    [Fact]
    public void FilterNode_StoresSourceParameterAndPredicate()
    {
        var node = new FilterNode("unpaid", new PathExpression("invoices"), "x",
            new BinaryExpression(new PathExpression("x.amount"), BinaryOperator.GreaterThan,
                new ConstantExpression(0, new ConstantProvenance(ConstantProvenanceKind.HandWritten))));

        Assert.Equal("unpaid", node.Id);
        Assert.Equal("x", node.ParameterName);
    }

    [Fact]
    public void SortNode_StoresDirection()
    {
        var node = new SortNode("ordered", new PathExpression("unpaid"), "x", new PathExpression("x.createdAt"), SortDirection.Ascending);
        Assert.Equal(SortDirection.Ascending, node.Direction);
    }

    [Fact]
    public void AggregateNode_Sum_StoresParameterAndSelector()
    {
        var node = new AggregateNode("total", new PathExpression("ordered"), AggregateOperation.Sum, "x", new PathExpression("x.amount"));
        Assert.Equal(AggregateOperation.Sum, node.Operation);
        Assert.NotNull(node.Selector);
    }

    [Fact]
    public void AggregateNode_Count_HasNoSelector()
    {
        var node = new AggregateNode("invoiceCount", new PathExpression("ordered"), AggregateOperation.Count, null, null);
        Assert.Null(node.Selector);
    }
}
