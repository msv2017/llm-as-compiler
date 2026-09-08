using Flow.Analysis;
using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class CapabilityAndEffectAnalyzerTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition("crm.findCustomer",
            new ObjectType("In", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new ObjectType("Customer", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            ToolEffect.Read, ToolRetryPolicy.Safe),
        new ToolDefinition("billing.createRefund",
            new ObjectType("In", new Dictionary<string, FlowType> { ["invoiceId"] = PrimitiveType.String }),
            new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            ToolEffect.Write, ToolRetryPolicy.Never)
    });

    private static WorkflowDefinition BuildWorkflow()
    {
        var find = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var ifNode = new IfNode(
            "refunded",
            new BinaryExpression(new PathExpression("customer"), BinaryOperator.NotEqual, new ConstantExpression(null, Provenance)),
            new IfBranch(
                new WorkflowNode[]
                {
                    new CallNode("refund", "billing.createRefund",
                        new Dictionary<string, FlowExpression> { ["invoiceId"] = new ConstantExpression("inv-1", Provenance) })
                },
                new ConstantExpression(true, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(false, Provenance)));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["refunded"] = new PathExpression("refunded") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["refunded"] = PrimitiveType.Bool });

        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { find, ifNode }, returnNode);
    }

    [Fact]
    public void CapabilityAnalyzer_FindsToolsInsideIfBranch()
    {
        var capabilities = CapabilityAnalyzer.Analyze(BuildWorkflow());

        Assert.Equal(new HashSet<string> { "crm.findCustomer", "billing.createRefund" }, capabilities);
    }

    [Fact]
    public void EffectAnalyzer_ClassifiesReadsAndWrites()
    {
        var summary = EffectAnalyzer.Analyze(BuildWorkflow(), Catalog());

        Assert.Contains("crm.findCustomer", summary.Reads);
        Assert.Contains("billing.createRefund", summary.Writes);
    }
}
