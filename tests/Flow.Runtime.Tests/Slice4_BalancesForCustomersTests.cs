using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Runtime.Tests;

public class Slice4_BalancesForCustomersTests
{
    private static readonly ObjectType CustomerRefType = new("CustomerRef", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });

    private sealed class BalanceInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            Assert.Equal("billing.getBalance", toolName);
            var customerId = (string)arguments["customerId"]!;
            return Task.FromResult<object?>(customerId == "1" ? 10m : 25m);
        }
    }

    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition(
            "billing.getBalance",
            new ObjectType("GetBalanceInput", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
            PrimitiveType.Decimal,
            ToolEffect.Read,
            ToolRetryPolicy.Safe)
    });

    private static WorkflowDefinition BuildWorkflow(int limit)
    {
        var body = new WorkflowNode[]
        {
            new CallNode("balanceCall", "billing.getBalance",
                new Dictionary<string, FlowExpression> { ["customerId"] = new PathExpression("customer.id") })
        };
        var foreachNode = new ForeachNode(
            "balances", new PathExpression("input.customers"), "customer", limit, body, new PathExpression("balanceCall"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["balances"] = new PathExpression("balances") });

        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customers"] = new ListType(CustomerRefType) });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["balances"] = new ListType(PrimitiveType.Decimal) });

        return new WorkflowDefinition("BalancesForCustomers", inputType, outputType, new WorkflowNode[] { foreachNode }, returnNode);
    }

    [Fact]
    public async Task ValidatesAndExecutes_ReturningOneBalancePerCustomer()
    {
        var workflow = BuildWorkflow(limit: 100);
        Assert.Empty(WorkflowValidator.CreateDefault().Validate(workflow, Catalog()));

        var customers = new List<object?>
        {
            new FlowRecord(new Dictionary<string, object?> { ["id"] = "1" }),
            new FlowRecord(new Dictionary<string, object?> { ["id"] = "2" }),
            new FlowRecord(new Dictionary<string, object?> { ["id"] = "3" })
        };
        var input = new FlowRecord(new Dictionary<string, object?> { ["customers"] = customers });

        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, new BalanceInvoker());

        Assert.True(result.Success);
        Assert.Equal(new object?[] { 10m, 25m, 25m }, (List<object?>)result.Output!.Get("balances")!);
    }

    [Fact]
    public void UnboundedForeach_FailsValidation_BeforeExecution()
    {
        var workflow = BuildWorkflow(limit: 0);

        var diagnostics = WorkflowValidator.CreateDefault().Validate(workflow, Catalog());

        Assert.Contains(diagnostics, d => d.Code == "B501");
    }
}
