using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Runtime.Tests;

public class WorkflowExecutorCollectionTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    private static FlowRecord Invoice(decimal amount, DateTime createdAt) =>
        new(new Dictionary<string, object?> { ["amount"] = amount, ["createdAt"] = createdAt });

    private sealed class NoOpInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This test does not expect any tool calls.");
    }

    [Fact]
    public async Task Filter_Sort_Aggregate_ProduceExpectedTotal()
    {
        var filter = new FilterNode("unpaid", new PathExpression("input.invoices"), "x",
            new BinaryExpression(new PathExpression("x.amount"), BinaryOperator.GreaterThan, new ConstantExpression(0m, Provenance)));
        var sort = new SortNode("ordered", new PathExpression("unpaid"), "x", new PathExpression("x.createdAt"), SortDirection.Ascending);
        var sum = new AggregateNode("total", new PathExpression("ordered"), AggregateOperation.Sum, "x", new PathExpression("x.amount"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["outstanding"] = new PathExpression("total") });

        var invoiceType = new ObjectType("Invoice", new Dictionary<string, FlowType>
        {
            ["amount"] = PrimitiveType.Decimal,
            ["createdAt"] = PrimitiveType.DateTime
        });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["invoices"] = new ListType(invoiceType) });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["outstanding"] = PrimitiveType.Decimal });
        var workflow = new WorkflowDefinition(
            "Test", inputType, outputType, new WorkflowNode[] { filter, sort, sum }, returnNode);

        var invoices = new List<object?>
        {
            Invoice(100m, new DateTime(2026, 2, 1)),
            Invoice(0m, new DateTime(2026, 1, 1)),   // filtered out (amount not > 0)
            Invoice(50m, new DateTime(2026, 1, 15))
        };
        var input = new FlowRecord(new Dictionary<string, object?> { ["invoices"] = invoices });

        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, new NoOpInvoker());

        Assert.Equal(150m, result.Output!.Get("outstanding"));
    }
}
