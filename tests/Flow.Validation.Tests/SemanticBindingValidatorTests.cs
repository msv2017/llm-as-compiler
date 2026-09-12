using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class SemanticBindingValidatorTests
{
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

    [Fact]
    public void BindingWrongSemanticTag_ThroughOptionalWrapper_StillProducesS201()
    {
        var optionalCustomerType = new ObjectType("Customer", new Dictionary<string, FlowType>
        {
            ["id"] = new OptionalType(new SemanticType("CustomerId", PrimitiveType.String)),
            ["email"] = new OptionalType(new SemanticType("Email", PrimitiveType.String))
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
                optionalCustomerType, ToolEffect.Read, ToolRetryPolicy.Safe),
            new ToolDefinition("billing.createRefund",
                new ObjectType("In", new Dictionary<string, FlowType>
                {
                    ["customerId"] = new OptionalType(new SemanticType("CustomerId", PrimitiveType.String))
                }),
                new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
                ToolEffect.Write, ToolRetryPolicy.Never)
        });

        var diagnostics = new SemanticBindingValidator().Validate(workflow, tools);

        Assert.Contains(diagnostics, d => d.Code == "S201");
    }

    [Fact]
    public void BindingWrongSemanticTag_ThroughFilterNodeOutput_IsNotDetected()
    {
        // KNOWN SCOPE LIMIT: SemanticBindingValidator only tracks CallNode outputs (see CheckNodes in
        // SemanticBindingValidator.cs). A binding sourced from a FilterNode/SortNode/AggregateNode output
        // can't be resolved, so the check is silently skipped here -- not silently passed, but there is
        // no diagnostic either way. This mirrors ToolResolutionValidator's existing recursion scope in
        // this codebase. Extending coverage to collection nodes is tracked as follow-up work, not a bug.
        var customerType = new ObjectType("Customer", new Dictionary<string, FlowType>
        {
            ["id"] = new SemanticType("CustomerId", PrimitiveType.String),
            ["email"] = new SemanticType("Email", PrimitiveType.String)
        });
        var listCustomers = new CallNode("customers", "crm.listCustomers", new Dictionary<string, FlowExpression>());
        var filtered = new FilterNode("filtered", new PathExpression("customers"), "c",
            new BinaryExpression(new PathExpression("c.id"), BinaryOperator.NotEqual, new ConstantExpression(null, new ConstantProvenance(ConstantProvenanceKind.HandWritten))));
        var refund = new CallNode("refund", "billing.createRefund",
            new Dictionary<string, FlowExpression> { ["customerId"] = new PathExpression("filtered.email") }); // WRONG, but unreachable by this validator
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["refundId"] = new PathExpression("refund.id") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["refundId"] = PrimitiveType.String });
        var workflow = new WorkflowDefinition(
            "W", inputType, outputType, new WorkflowNode[] { listCustomers, filtered, refund }, returnNode);

        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("crm.listCustomers",
                new ObjectType("In", new Dictionary<string, FlowType>()),
                new ListType(customerType), ToolEffect.Read, ToolRetryPolicy.Safe),
            new ToolDefinition("billing.createRefund",
                new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = new SemanticType("CustomerId", PrimitiveType.String) }),
                new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
                ToolEffect.Write, ToolRetryPolicy.Never)
        });

        var diagnostics = new SemanticBindingValidator().Validate(workflow, tools);

        Assert.Empty(diagnostics); // documents the gap -- update this test if the scope is ever extended
    }
}
