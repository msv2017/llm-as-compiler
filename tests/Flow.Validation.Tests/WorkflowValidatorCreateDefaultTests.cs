using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class WorkflowValidatorCreateDefaultTests
{
    [Fact]
    public void CreateDefault_RunsEveryValidationPass()
    {
        // An unknown tool (F001, from ToolResolutionValidator), a zero foreach limit (B501, from
        // BoundednessValidator), an unproven constant (S204, from ConstantProvenanceValidator), and a
        // suspicious semantic binding (S201, from SemanticBindingValidator) together prove CreateDefault()
        // wires up every pass, not just the ones already present before Tasks 7-8 added S201/S204.
        var customerType = new ObjectType("Customer", new Dictionary<string, FlowType>
        {
            ["id"] = new SemanticType("CustomerId", PrimitiveType.String)
        });
        var findCustomer = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var refund = new CallNode("refund", "billing.createRefund",
            new Dictionary<string, FlowExpression>
            {
                ["customerId"] = new PathExpression("customer.id"),
                ["note"] = new ConstantExpression("hi", new ConstantProvenance(ConstantProvenanceKind.Unproven))
            });
        var foreachNode = new ForeachNode(
            "results", new PathExpression("input.items"), "item", 0,
            new WorkflowNode[]
            {
                new CallNode("call", "does.not.exist", new Dictionary<string, FlowExpression>())
            },
            new PathExpression("call"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["results"] = new PathExpression("results") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>
        {
            ["email"] = PrimitiveType.String,
            ["items"] = new ListType(PrimitiveType.String)
        });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["results"] = new ListType(PrimitiveType.String) });
        var workflow = new WorkflowDefinition(
            "Test", inputType, outputType, new WorkflowNode[] { findCustomer, refund, foreachNode }, returnNode);

        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("crm.findCustomer",
                new ObjectType("In", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
                customerType, ToolEffect.Read, ToolRetryPolicy.Safe),
            new ToolDefinition("billing.createRefund",
                new ObjectType("In", new Dictionary<string, FlowType>
                {
                    ["customerId"] = new SemanticType("CustomerId", PrimitiveType.String),
                    ["note"] = PrimitiveType.String
                }),
                new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
                ToolEffect.Write, ToolRetryPolicy.Never)
        });

        var diagnostics = WorkflowValidator.CreateDefault().Validate(workflow, tools);

        Assert.Contains(diagnostics, d => d.Code == "F001");
        Assert.Contains(diagnostics, d => d.Code == "B501");
        Assert.Contains(diagnostics, d => d.Code == "S204");
    }

    [Fact]
    public void CreateDefault_RunsSemanticBindingValidator()
    {
        var customerType = new ObjectType("Customer", new Dictionary<string, FlowType>
        {
            ["id"] = new SemanticType("CustomerId", PrimitiveType.String),
            ["email"] = new SemanticType("Email", PrimitiveType.String)
        });
        var findCustomer = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var refund = new CallNode("refund", "billing.createRefund",
            new Dictionary<string, FlowExpression> { ["customerId"] = new PathExpression("customer.email") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["refundId"] = new PathExpression("refund.id") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["refundId"] = PrimitiveType.String });
        var workflow = new WorkflowDefinition("W", inputType, outputType, new WorkflowNode[] { findCustomer, refund }, returnNode);

        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("crm.findCustomer",
                new ObjectType("In", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
                customerType, ToolEffect.Read, ToolRetryPolicy.Safe),
            new ToolDefinition("billing.createRefund",
                new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = new SemanticType("CustomerId", PrimitiveType.String) }),
                new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
                ToolEffect.Write, ToolRetryPolicy.Never)
        });

        var diagnostics = WorkflowValidator.CreateDefault().Validate(workflow, tools);

        Assert.Contains(diagnostics, d => d.Code == "S201");
    }
}
