using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class SemanticBindingValidatorTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    private static readonly ObjectType CustomerType = new("Customer", new Dictionary<string, FlowType>
    {
        ["id"] = new SemanticType("CustomerId", PrimitiveType.String),
        ["email"] = new SemanticType("Email", PrimitiveType.String)
    });

    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition("crm.findCustomer",
            new ObjectType("In", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            CustomerType, ToolEffect.Read, ToolRetryPolicy.Safe),
        new ToolDefinition("billing.createRefund",
            new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = new SemanticType("CustomerId", PrimitiveType.String) }),
            new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            ToolEffect.Write, ToolRetryPolicy.Never)
    });

    private static WorkflowDefinition BuildWorkflow(string boundField)
    {
        var findCustomer = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var refund = new CallNode("refund", "billing.createRefund",
            new Dictionary<string, FlowExpression> { ["customerId"] = new PathExpression($"customer.{boundField}") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["refundId"] = new PathExpression("refund.id") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["refundId"] = PrimitiveType.String });

        return new WorkflowDefinition("W", inputType, outputType, new WorkflowNode[] { findCustomer, refund }, returnNode);
    }

    [Fact]
    public void BindingWrongSemanticTag_ProducesS201_WithLikelySourceSuggestion()
    {
        var workflow = BuildWorkflow(boundField: "email"); // WRONG: Email bound where CustomerId expected

        var diagnostics = new SemanticBindingValidator().Validate(workflow, Catalog());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("S201", diagnostic.Code);
        Assert.Equal("refund", diagnostic.NodeId);
        Assert.Contains("customer.id", diagnostic.Message);
    }

    [Fact]
    public void BindingCorrectSemanticTag_ProducesNoDiagnostic()
    {
        var workflow = BuildWorkflow(boundField: "id"); // correct: CustomerId -> CustomerId

        var diagnostics = new SemanticBindingValidator().Validate(workflow, Catalog());

        Assert.Empty(diagnostics);
    }
}
