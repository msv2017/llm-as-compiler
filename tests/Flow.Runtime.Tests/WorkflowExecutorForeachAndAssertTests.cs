using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Runtime.Tests;

public class WorkflowExecutorForeachAndAssertTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    private sealed class BalanceInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            var customerId = (string)arguments["customerId"]!;
            return Task.FromResult<object?>(new FlowRecord(new Dictionary<string, object?> { ["amount"] = customerId == "1" ? 10m : 20m }));
        }
    }

    [Fact]
    public async Task Foreach_CallsToolPerItem_AndCollectsResults()
    {
        var body = new WorkflowNode[]
        {
            new CallNode("balanceCall", "billing.getBalance",
                new Dictionary<string, FlowExpression> { ["customerId"] = new PathExpression("customer.id") })
        };
        var foreachNode = new ForeachNode(
            "balances", new PathExpression("input.customers"), "customer", 100, body, new PathExpression("balanceCall.amount"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["balances"] = new PathExpression("balances") });

        var customerType = new ObjectType("Customer", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customers"] = new ListType(customerType) });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["balances"] = new ListType(PrimitiveType.Decimal) });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { foreachNode }, returnNode);

        var customers = new List<object?>
        {
            new FlowRecord(new Dictionary<string, object?> { ["id"] = "1" }),
            new FlowRecord(new Dictionary<string, object?> { ["id"] = "2" })
        };
        var input = new FlowRecord(new Dictionary<string, object?> { ["customers"] = customers });

        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, new BalanceInvoker());

        Assert.Equal(new object?[] { 10m, 20m }, (List<object?>)result.Output!.Get("balances")!);
    }

    [Fact]
    public async Task Assert_FailingCondition_ReturnsFailureResult_InsteadOfThrowing()
    {
        var assertNode = new AssertNode("check",
            new BinaryExpression(new PathExpression("input.total"), BinaryOperator.GreaterThanOrEqual, new ConstantExpression(0m, Provenance)),
            "INVALID_TOTAL");
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new PathExpression("check") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["total"] = PrimitiveType.Decimal });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["ok"] = PrimitiveType.Bool });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { assertNode }, returnNode);

        var input = new FlowRecord(new Dictionary<string, object?> { ["total"] = -5m });
        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, new NoOpInvoker());

        Assert.False(result.Success);
        Assert.Equal("INVALID_TOTAL", result.FailureReason);
    }

    private sealed class NoOpInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This test does not expect any tool calls.");
    }
}
