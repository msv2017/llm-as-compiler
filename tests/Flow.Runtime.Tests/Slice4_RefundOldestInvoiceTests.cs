using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Runtime.Tests;

public class Slice4_RefundOldestInvoiceTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);
    private static readonly ObjectType InvoiceType = new("Invoice", new Dictionary<string, FlowType>
    {
        ["id"] = PrimitiveType.String,
        ["amount"] = PrimitiveType.Decimal,
        ["status"] = PrimitiveType.String,
        ["createdAt"] = PrimitiveType.DateTime
    });

    private static List<object?> DefaultInvoices() => new()
    {
        new FlowRecord(new Dictionary<string, object?> { ["id"] = "inv-2", ["amount"] = 120m, ["status"] = "UNPAID", ["createdAt"] = new DateTime(2026, 2, 1) }),
        new FlowRecord(new Dictionary<string, object?> { ["id"] = "inv-1", ["amount"] = 80m, ["status"] = "UNPAID", ["createdAt"] = new DateTime(2026, 1, 1) }),
        new FlowRecord(new Dictionary<string, object?> { ["id"] = "inv-0", ["amount"] = 999m, ["status"] = "PAID", ["createdAt"] = new DateTime(2025, 1, 1) })
    };

    private sealed class FakeInvoker : IMcpInvoker
    {
        private readonly FlowRecord? _customer;
        private readonly List<object?> _invoices;

        public FakeInvoker(FlowRecord? customer, List<object?>? invoices = null)
        {
            _customer = customer;
            _invoices = invoices ?? DefaultInvoices();
        }

        public bool RefundCalled { get; private set; }
        public string? RefundedInvoiceId { get; private set; }

        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            object? result = toolName switch
            {
                "crm.findCustomer" => _customer,
                "billing.listInvoices" => _invoices,
                "billing.createRefund" => Handle(arguments),
                _ => throw new InvalidOperationException($"Unexpected tool '{toolName}'.")
            };
            return Task.FromResult(result);

            object? Handle(IReadOnlyDictionary<string, object?> args)
            {
                RefundCalled = true;
                RefundedInvoiceId = (string)args["invoiceId"]!;
                return new FlowRecord(new Dictionary<string, object?> { ["id"] = "refund-1" });
            }
        }
    }

    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition("crm.findCustomer",
            new ObjectType("In", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new OptionalType(new ObjectType("Customer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String })),
            ToolEffect.Read, ToolRetryPolicy.Safe),
        new ToolDefinition("billing.listInvoices",
            new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
            new ListType(InvoiceType),
            ToolEffect.Read, ToolRetryPolicy.Safe),
        new ToolDefinition("billing.createRefund",
            new ObjectType("In", new Dictionary<string, FlowType> { ["invoiceId"] = PrimitiveType.String }),
            new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            ToolEffect.Write, ToolRetryPolicy.Never)
    });

    private static WorkflowDefinition BuildWorkflow()
    {
        var findCustomer = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var isNull = new BinaryExpression(new PathExpression("customer"), BinaryOperator.Equal, new ConstantExpression(null, Provenance));

        var listInvoices = new CallNode("invoices", "billing.listInvoices",
            new Dictionary<string, FlowExpression> { ["customerId"] = new ConstantExpression("does-not-matter", Provenance) });
        var unpaid = new FilterNode("unpaid", new PathExpression("invoices"), "x",
            new BinaryExpression(new PathExpression("x.status"), BinaryOperator.Equal, new ConstantExpression("UNPAID", Provenance)));
        var ordered = new SortNode("ordered", new PathExpression("unpaid"), "x", new PathExpression("x.createdAt"), SortDirection.Ascending);
        var total = new AggregateNode("total", new PathExpression("ordered"), AggregateOperation.Sum, "x", new PathExpression("x.amount"));
        var count = new AggregateNode("unpaidCount", new PathExpression("ordered"), AggregateOperation.Count, null, null);
        var oldest = new AggregateNode("oldest", new PathExpression("ordered"), AggregateOperation.First, null, null);

        var shouldRefund = new BinaryExpression(
            new BinaryExpression(new PathExpression("total"), BinaryOperator.LessThan, new ConstantExpression(500m, Provenance)),
            BinaryOperator.And,
            new BinaryExpression(new PathExpression("unpaidCount"), BinaryOperator.GreaterThan, new ConstantExpression(0, Provenance)));

        var refundCall = new CallNode("refundCall", "billing.createRefund",
            new Dictionary<string, FlowExpression> { ["invoiceId"] = new PathExpression("oldest.id") });
        // `oldest` is Optional (aggregate `first` over a possibly-empty list), so dereferencing
        // `oldest.id` needs an explicit null guard. At runtime `oldest` can only be null when
        // unpaidCount == 0, which already makes shouldRefund false — but that is an arithmetic
        // implication the nullability pass cannot infer, so the check is stated in the IR.
        var refundIfPresent = new IfNode(
            "refundOutcome",
            new BinaryExpression(new PathExpression("oldest"), BinaryOperator.NotEqual, new ConstantExpression(null, Provenance)),
            new IfBranch(new WorkflowNode[] { refundCall }, new ConstantExpression(true, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(false, Provenance)));
        var refundGuard = new IfNode(
            "refundedFlag", shouldRefund,
            new IfBranch(new WorkflowNode[] { refundIfPresent }, new PathExpression("refundOutcome")),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(false, Provenance)));

        var customerFoundPipeline = new WorkflowNode[] { listInvoices, unpaid, ordered, total, count, oldest, refundGuard };

        var resultBundle = new IfNode(
            "resultBundle",
            isNull,
            new IfBranch(
                Array.Empty<WorkflowNode>(),
                new ObjectExpression(new Dictionary<string, FlowExpression>
                {
                    ["customerName"] = new ConstantExpression("", Provenance),
                    ["outstanding"] = new ConstantExpression(0m, Provenance),
                    ["refunded"] = new ConstantExpression(false, Provenance)
                })),
            new IfBranch(
                customerFoundPipeline,
                new ObjectExpression(new Dictionary<string, FlowExpression>
                {
                    ["customerName"] = new PathExpression("customer.name"),
                    ["outstanding"] = new PathExpression("total"),
                    ["refunded"] = new PathExpression("refundedFlag")
                })));

        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("resultBundle.customerName"),
            ["outstanding"] = new PathExpression("resultBundle.outstanding"),
            ["refunded"] = new PathExpression("resultBundle.refunded")
        });

        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType>
        {
            ["customerName"] = PrimitiveType.String,
            ["outstanding"] = PrimitiveType.Decimal,
            ["refunded"] = PrimitiveType.Bool
        });

        return new WorkflowDefinition(
            "RefundOldestInvoice", inputType, outputType,
            new WorkflowNode[] { findCustomer, resultBundle }, returnNode);
    }

    private static WorkflowValidator BuildValidator() => new(new IWorkflowValidationPass[]
    {
        new ToolResolutionValidator(),
        new DataflowValidator(),
        new TypeValidator(),
        new NullabilityValidator(),
        new OutputValidator(),
        new BoundednessValidator()
    });

    [Fact]
    public async Task CustomerWithLowBalance_RefundsOldestUnpaidInvoice()
    {
        var workflow = BuildWorkflow();
        Assert.Empty(BuildValidator().Validate(workflow, Catalog()));

        var invoker = new FakeInvoker(new FlowRecord(new Dictionary<string, object?> { ["name"] = "Ada Lovelace" }));
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "ada@example.com" });

        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, invoker);

        Assert.True(result.Success);
        Assert.Equal("Ada Lovelace", result.Output!.Get("customerName"));
        Assert.Equal(200m, result.Output!.Get("outstanding")); // 120 + 80, PAID invoice excluded
        Assert.Equal(true, result.Output!.Get("refunded"));
        Assert.True(invoker.RefundCalled);
        Assert.Equal("inv-1", invoker.RefundedInvoiceId); // the older of the two UNPAID invoices
    }

    [Fact]
    public async Task CustomerNotFound_ReturnsEmptyResult_WithoutCallingRefund()
    {
        var workflow = BuildWorkflow();
        var invoker = new FakeInvoker(null);
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "missing@example.com" });

        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, invoker);

        Assert.Equal("", result.Output!.Get("customerName"));
        Assert.Equal(0m, result.Output!.Get("outstanding"));
        Assert.Equal(false, result.Output!.Get("refunded"));
        Assert.False(invoker.RefundCalled);
    }

    [Fact]
    public async Task CustomerWithNoUnpaidInvoices_ReturnsZeroOutstanding_WithoutCallingRefund()
    {
        // `oldest` (aggregate `first`) has an empty source here — it must bind null instead of throwing.
        var workflow = BuildWorkflow();
        Assert.Empty(BuildValidator().Validate(workflow, Catalog()));

        var allPaid = new List<object?>
        {
            new FlowRecord(new Dictionary<string, object?> { ["id"] = "inv-0", ["amount"] = 999m, ["status"] = "PAID", ["createdAt"] = new DateTime(2025, 1, 1) }),
            new FlowRecord(new Dictionary<string, object?> { ["id"] = "inv-3", ["amount"] = 10m, ["status"] = "PAID", ["createdAt"] = new DateTime(2026, 3, 1) })
        };
        var invoker = new FakeInvoker(new FlowRecord(new Dictionary<string, object?> { ["name"] = "Ada Lovelace" }), allPaid);
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "ada@example.com" });

        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, invoker);

        Assert.True(result.Success);
        Assert.Equal(0m, result.Output!.Get("outstanding"));
        Assert.Equal(false, result.Output!.Get("refunded"));
        Assert.False(invoker.RefundCalled);
    }
}
