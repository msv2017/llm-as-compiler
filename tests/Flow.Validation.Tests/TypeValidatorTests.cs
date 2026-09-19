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

    [Fact]
    public void CountAggregateWithSelector_ProducesT102()
    {
        // WorkflowExecutor's Count case never evaluates Selector -- it silently behaves like a plain
        // list-length count regardless of what the selector says. This exact shape (Count with a
        // selector) was found in a real compiled workflow (examples/07-bounded-foreach) before this
        // check existed.
        var invoiceType = new ObjectType("Invoice", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });
        var listInvoices = new CallNode("invoices", "billing.listInvoices", new Dictionary<string, FlowExpression>());
        var count = new AggregateNode("count", new PathExpression("invoices"), AggregateOperation.Count, "x",
            new ConstantExpression(true, new ConstantProvenance(ConstantProvenanceKind.HandWritten)));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["count"] = new PathExpression("count") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["count"] = PrimitiveType.Int });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { listInvoices, count }, returnNode);
        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("billing.listInvoices",
                new ObjectType("In", new Dictionary<string, FlowType>()),
                new ListType(invoiceType), ToolEffect.Read, ToolRetryPolicy.Safe)
        });

        var diagnostics = new TypeValidator().Validate(workflow, tools);

        Assert.Contains(diagnostics, d => d.Code == "T102" && d.NodeId == "count");
    }

    [Fact]
    public void FirstAggregateWithSelector_ProducesT102()
    {
        // Same gap as Count: WorkflowExecutor's First case never evaluates Selector either.
        var invoiceType = new ObjectType("Invoice", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });
        var listInvoices = new CallNode("invoices", "billing.listInvoices", new Dictionary<string, FlowExpression>());
        var first = new AggregateNode("first", new PathExpression("invoices"), AggregateOperation.First, "x",
            new ConstantExpression(true, new ConstantProvenance(ConstantProvenanceKind.HandWritten)));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["count"] = new PathExpression("invoices") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["count"] = new ListType(invoiceType) });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { listInvoices, first }, returnNode);
        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("billing.listInvoices",
                new ObjectType("In", new Dictionary<string, FlowType>()),
                new ListType(invoiceType), ToolEffect.Read, ToolRetryPolicy.Safe)
        });

        var diagnostics = new TypeValidator().Validate(workflow, tools);

        Assert.Contains(diagnostics, d => d.Code == "T102" && d.NodeId == "first");
    }

    [Fact]
    public void SumAggregateWithSelector_ProducesNoT102()
    {
        // Sum is the one operation that genuinely evaluates its selector -- must not be flagged.
        var invoiceType = new ObjectType("Invoice", new Dictionary<string, FlowType> { ["amount"] = PrimitiveType.Decimal });
        var listInvoices = new CallNode("invoices", "billing.listInvoices", new Dictionary<string, FlowExpression>());
        var sum = new AggregateNode("total", new PathExpression("invoices"), AggregateOperation.Sum, "x", new PathExpression("x.amount"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["total"] = new PathExpression("total") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["total"] = PrimitiveType.Decimal });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { listInvoices, sum }, returnNode);
        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("billing.listInvoices",
                new ObjectType("In", new Dictionary<string, FlowType>()),
                new ListType(invoiceType), ToolEffect.Read, ToolRetryPolicy.Safe)
        });

        var diagnostics = new TypeValidator().Validate(workflow, tools);

        Assert.DoesNotContain(diagnostics, d => d.Code == "T102");
    }

    [Fact]
    public void CountAggregateWithoutSelector_ProducesNoT102()
    {
        // The correct, already-working pattern (e.g. examples/07-bounded-foreach's countTrue node)
        // must not regress.
        var invoiceType = new ObjectType("Invoice", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });
        var listInvoices = new CallNode("invoices", "billing.listInvoices", new Dictionary<string, FlowExpression>());
        var count = new AggregateNode("count", new PathExpression("invoices"), AggregateOperation.Count, null, null);
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["count"] = new PathExpression("count") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["count"] = PrimitiveType.Int });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { listInvoices, count }, returnNode);
        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("billing.listInvoices",
                new ObjectType("In", new Dictionary<string, FlowType>()),
                new ListType(invoiceType), ToolEffect.Read, ToolRetryPolicy.Safe)
        });

        var diagnostics = new TypeValidator().Validate(workflow, tools);

        Assert.DoesNotContain(diagnostics, d => d.Code == "T102");
    }
}
