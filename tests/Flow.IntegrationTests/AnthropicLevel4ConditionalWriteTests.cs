using Flow.Compiler;
using Flow.Compiler.Providers;
using Flow.Contracts;
using Flow.Runtime;
using Flow.TypeSystem;
using Xunit;

namespace Flow.IntegrationTests;

// Mirrors Level4ConditionalWriteTests.cs (OpenAI), against the Anthropic provider. OpenAI itself
// does not reliably pass this one -- the model doesn't always self-correct to AggregateNode(First)
// for "oldest" within the default repair attempts -- so this documents Anthropic's behavior on the
// same known-hard case rather than assuming it must pass.
public class AnthropicLevel4ConditionalWriteTests
{
    private static readonly ObjectType InvoiceType = new("Invoice", new Dictionary<string, FlowType>
    {
        ["id"] = PrimitiveType.String,
        ["amount"] = PrimitiveType.Decimal,
        ["status"] = PrimitiveType.String,
        ["createdAt"] = PrimitiveType.DateTime
    });

    private sealed class FakeInvoker : IMcpInvoker
    {
        public bool RefundCalled { get; private set; }

        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            object? result = toolName switch
            {
                "billing.listInvoices" => new List<object?>
                {
                    new FlowRecord(new Dictionary<string, object?> { ["id"] = "inv-1", ["amount"] = 40m, ["status"] = "UNPAID", ["createdAt"] = new DateTime(2026, 1, 1) })
                },
                "billing.createRefund" => Handle(),
                _ => throw new InvalidOperationException($"Unexpected tool '{toolName}'.")
            };
            return Task.FromResult(result);

            object? Handle()
            {
                RefundCalled = true;
                return new FlowRecord(new Dictionary<string, object?> { ["id"] = "refund-1" });
            }
        }
    }

    [Fact]
    public async Task RefundIfSmallBalance_CompilesAndExecutesCorrectly()
    {
        if (AnthropicTestEnvironment.ApiKey is null) return; // no ANTHROPIC_API_KEY configured

        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("billing.listInvoices",
                new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
                new ListType(InvoiceType), ToolEffect.Read, ToolRetryPolicy.Safe),
            new ToolDefinition("billing.createRefund",
                new ObjectType("In", new Dictionary<string, FlowType> { ["invoiceId"] = PrimitiveType.String }),
                new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
                ToolEffect.Write, ToolRetryPolicy.Never)
        });
        var source = new WorkflowSource(
            "Given a customer id, compute their outstanding balance as the sum of UNPAID invoice amounts. " +
            "If the balance is less than 100, refund the oldest unpaid invoice by calling the refund tool with " +
            "that invoice's id. Return whether a refund was made and the outstanding balance.",
            new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
            new ObjectType("Result", new Dictionary<string, FlowType>
            {
                ["refunded"] = PrimitiveType.Bool,
                ["outstanding"] = PrimitiveType.Decimal
            }));

        var compiler = new PromptCompiler(AnthropicSemanticCompilerModel.FromEnvironment());
        var result = await compiler.CompileAsync(source, tools);

        Assert.Equal(CompilationStatus.Success, result.Status);

        var invoker = new FakeInvoker();
        var input = new FlowRecord(new Dictionary<string, object?> { ["customerId"] = "cust-1" });
        var executed = await new WorkflowExecutor().ExecuteAsync(result.Workflow!, input, invoker);

        Assert.True(executed.Success);
        Assert.Equal(40m, executed.Output!.Get("outstanding"));
        Assert.Equal(true, executed.Output!.Get("refunded"));
        Assert.True(invoker.RefundCalled);
    }
}
