using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class TypeValidatorTests
{
    private static ToolCatalog CatalogWithFindCustomer() => new(new[]
    {
        new ToolDefinition(
            "crm.findCustomer",
            new ObjectType("FindCustomerInput", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new ObjectType("Customer", new Dictionary<string, FlowType>
            {
                ["id"] = PrimitiveType.String,
                ["name"] = PrimitiveType.String
            }),
            ToolEffect.Read,
            ToolRetryPolicy.Safe)
    });

    [Fact]
    public void MismatchedArgumentType_ProducesT101()
    {
        var call = new CallNode("customer", "crm.findCustomer", new Dictionary<string, FlowExpression>
        {
            ["email"] = new ConstantExpression(42, new ConstantProvenance(ConstantProvenanceKind.HandWritten))
        });
        var workflow = WorkflowWith(call, "customer.name");

        var diagnostics = new TypeValidator().Validate(workflow, CatalogWithFindCustomer());

        Assert.Contains(diagnostics, d => d.Code == "T101" && d.NodeId == "customer");
    }

    [Fact]
    public void MismatchedReturnFieldType_ProducesT101()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var workflow = WorkflowWith(call, "customer.id"); // customer.id is String, "name" output field expects String too — force a mismatch instead:
        var badReturn = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["name"] = new ConstantExpression(true, new ConstantProvenance(ConstantProvenanceKind.HandWritten))
        });
        var badWorkflow = workflow with { Return = badReturn };

        var diagnostics = new TypeValidator().Validate(badWorkflow, CatalogWithFindCustomer());

        Assert.Contains(diagnostics, d => d.Code == "T101" && d.NodeId == "return");
    }

    [Fact]
    public void MatchingTypes_ProduceNoDiagnostics()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var workflow = WorkflowWith(call, "customer.name");

        var diagnostics = new TypeValidator().Validate(workflow, CatalogWithFindCustomer());

        Assert.Empty(diagnostics);
    }

    private static WorkflowDefinition WorkflowWith(CallNode call, string returnPath)
    {
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression(returnPath) });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { call }, returnNode);
    }

    [Fact]
    public void NumericPathSegmentIndexingIntoList_WithKnownRoot_ProducesT104()
    {
        // A real model generated exactly this shape ("sortedList.0.id") to mean "the first item" --
        // this IR has no list-indexing path syntax (the correct construct is an aggregate node with
        // operation "First"). The root ("sorted") is a perfectly valid, known prior node, so
        // DataflowValidator's D301 (undefined root) never fires; this must be caught here instead.
        var invoiceType = new ObjectType("Invoice", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });
        var listInvoices = new CallNode("invoices", "billing.listInvoices", new Dictionary<string, FlowExpression>());
        var sorted = new SortNode("sorted", new PathExpression("invoices"), "x", new PathExpression("x.id"), SortDirection.Ascending);
        var refund = new CallNode("refund", "billing.createRefund",
            new Dictionary<string, FlowExpression> { ["invoiceId"] = new PathExpression("sorted.0.id") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["refundId"] = new PathExpression("refund.id") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["refundId"] = PrimitiveType.String });
        var workflow = new WorkflowDefinition(
            "Test", inputType, outputType, new WorkflowNode[] { listInvoices, sorted, refund }, returnNode);

        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("billing.listInvoices",
                new ObjectType("In", new Dictionary<string, FlowType>()),
                new ListType(invoiceType), ToolEffect.Read, ToolRetryPolicy.Safe),
            new ToolDefinition("billing.createRefund",
                new ObjectType("In", new Dictionary<string, FlowType> { ["invoiceId"] = PrimitiveType.String }),
                new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
                ToolEffect.Write, ToolRetryPolicy.Never)
        });

        var diagnostics = new TypeValidator().Validate(workflow, tools);

        Assert.Contains(diagnostics, d => d.Code == "T104" && d.NodeId == "refund");
    }
}
