using Flow.IR.Expressions;
using Xunit;

namespace Flow.IR.Tests;

public class ExpressionTests
{
    [Fact]
    public void PathExpression_StoresPath()
    {
        var expr = new PathExpression("input.email");
        Assert.Equal("input.email", expr.Path);
    }

    [Fact]
    public void ConstantExpression_StoresValueAndProvenance()
    {
        var provenance = new ConstantProvenance(ConstantProvenanceKind.HandWritten);
        var expr = new ConstantExpression("UNPAID", provenance);

        Assert.Equal("UNPAID", expr.Value);
        Assert.Equal(ConstantProvenanceKind.HandWritten, expr.Provenance.Kind);
    }

    [Fact]
    public void Expressions_WithEqualValues_AreEqual()
    {
        var a = new PathExpression("customer.id");
        var b = new PathExpression("customer.id");
        Assert.Equal(a, b);
    }
}
