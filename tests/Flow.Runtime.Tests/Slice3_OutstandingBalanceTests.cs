using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Runtime.Tests;

public class Slice3_OutstandingBalanceTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);
    private static readonly ObjectType InvoiceType = new("Invoice", new Dictionary<string, FlowType>
    {
        ["amount"] = PrimitiveType.Decimal,
        ["createdAt"] = PrimitiveType.DateTime
    });

    private sealed class FakeInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            object? result = toolName switch
            {
                "crm.getCustomerById" => new FlowRecord(new Dictionary<string, object?> { ["id"] = arguments["id"], ["name"] = "Ada Lovelace" }),
                "billing.listInvoices" => new List<object?>
                {
                    new FlowRecord(new Dictionary<string, object?> { ["amount"] = 100m, ["createdAt"] = new DateTime(2026, 2, 1) }),
                    new FlowRecord(new Dictionary<string, object?> { ["amount"] = 0m, ["createdAt"] = new DateTime(2026, 1, 1) }),
                    new FlowRecord(new Dictionary<string, object?> { ["amount"] = 50m, ["createdAt"] = new DateTime(2026, 1, 15) })
                },
                _ => throw new InvalidOperationException($"Unexpected tool call '{toolName}'.")
            };
            return Task.FromResult<object?>(result);
        }
    }

    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition(
            "crm.getCustomerById",
            new ObjectType("GetCustomerByIdInput", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            new ObjectType("Customer", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String, ["name"] = PrimitiveType.String }),
            ToolEffect.Read,
            ToolRetryPolicy.Safe),
        new ToolDefinition(
            "billing.listInvoices",
            new ObjectType("ListInvoicesInput", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
            new ListType(InvoiceType),
            ToolEffect.Read,
            ToolRetryPolicy.Safe)
    });

    private static WorkflowDefinition BuildWorkflow()
    {
        var getCustomer = new CallNode("customer", "crm.getCustomerById",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var listInvoices = new CallNode("invoices", "billing.listInvoices",
            new Dictionary<string, FlowExpression> { ["customerId"] = new PathExpression("customer.id") });
        var filter = new FilterNode("unpaid", new PathExpression("invoices"), "x",
            new BinaryExpression(new PathExpression("x.amount"), BinaryOperator.GreaterThan, new ConstantExpression(0m, Provenance)));
        var sort = new SortNode("ordered", new PathExpression("unpaid"), "x", new PathExpression("x.createdAt"), SortDirection.Ascending);
        var sum = new AggregateNode("total", new PathExpression("ordered"), AggregateOperation.Sum, "x", new PathExpression("x.amount"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("customer.name"),
            ["outstanding"] = new PathExpression("total")
        });

        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType>
        {
            ["customerName"] = PrimitiveType.String,
            ["outstanding"] = PrimitiveType.Decimal
        });

        return new WorkflowDefinition(
            "OutstandingBalance", inputType, outputType,
            new WorkflowNode[] { getCustomer, listInvoices, filter, sort, sum }, returnNode);
    }

    private static WorkflowValidator BuildValidator() => new(new IWorkflowValidationPass[]
    {
        new ToolResolutionValidator(),
        new DataflowValidator(),
        new TypeValidator(),
        new NullabilityValidator(),
        new OutputValidator()
    });

    [Fact]
    public async Task ValidatesAndExecutes_ToExpectedOutstandingBalance()
    {
        var workflow = BuildWorkflow();

        var diagnostics = BuildValidator().Validate(workflow, Catalog());
        Assert.Empty(diagnostics);

        var input = new FlowRecord(new Dictionary<string, object?> { ["customerId"] = "42" });
        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, new FakeInvoker());

        Assert.True(result.Success);
        Assert.Equal("Ada Lovelace", result.Output!.Get("customerName"));
        Assert.Equal(150m, result.Output!.Get("outstanding"));
    }
}
