using Flow.IR.Expressions;
using Xunit;

namespace Flow.IR.Tests;

public class ObjectExpressionTests
{
    [Fact]
    public void ObjectExpression_StoresFields()
    {
        var provenance = new ConstantProvenance(ConstantProvenanceKind.HandWritten);
        var expr = new ObjectExpression(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new ConstantExpression("", provenance),
            ["outstanding"] = new ConstantExpression(0m, provenance)
        });

        Assert.Equal(2, expr.Fields.Count);
        Assert.IsType<ConstantExpression>(expr.Fields["customerName"]);
    }
}
