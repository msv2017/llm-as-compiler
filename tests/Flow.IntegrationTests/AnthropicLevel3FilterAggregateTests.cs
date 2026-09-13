using Flow.Compiler;
using Flow.Compiler.Providers;
using Flow.Contracts;
using Flow.Runtime;
using Flow.TypeSystem;
using Xunit;

namespace Flow.IntegrationTests;

// Mirrors Level3FilterAggregateTests.cs (OpenAI), against the Anthropic provider.
public class AnthropicLevel3FilterAggregateTests
{
    private static readonly ObjectType InvoiceType = new("Invoice", new Dictionary<string, FlowType>
    {
        ["id"] = PrimitiveType.String,
        ["amount"] = PrimitiveType.Decimal,
        ["status"] = PrimitiveType.String
    });

    private sealed class FakeInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            var invoices = new List<object?>
            {
                new FlowRecord(new Dictionary<string, object?> { ["id"] = "inv-1", ["amount"] = 120m, ["status"] = "UNPAID" }),
                new FlowRecord(new Dictionary<string, object?> { ["id"] = "inv-2", ["amount"] = 80m, ["status"] = "UNPAID" }),
                new FlowRecord(new Dictionary<string, object?> { ["id"] = "inv-3", ["amount"] = 999m, ["status"] = "PAID" })
            };
            return Task.FromResult<object?>(invoices);
        }
    }

    [Fact]
    public async Task OutstandingBalance_CompilesAndExecutesCorrectly()
    {
        if (AnthropicTestEnvironment.ApiKey is null) return; // no ANTHROPIC_API_KEY configured

        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("billing.listInvoices",
                new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
                new ListType(InvoiceType), ToolEffect.Read, ToolRetryPolicy.Safe)
        });
        var source = new WorkflowSource(
            "Given a customer id, list their invoices, keep only the ones with status UNPAID, and return the " +
            "sum of their amounts as the outstanding balance.",
            new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
            new ObjectType("Result", new Dictionary<string, FlowType> { ["outstanding"] = PrimitiveType.Decimal }));

        var compiler = new PromptCompiler(AnthropicSemanticCompilerModel.FromEnvironment());
        var result = await compiler.CompileAsync(source, tools);

        Assert.Equal(CompilationStatus.Success, result.Status);

        var input = new FlowRecord(new Dictionary<string, object?> { ["customerId"] = "cust-1" });
        var executed = await new WorkflowExecutor().ExecuteAsync(result.Workflow!, input, new FakeInvoker());

        Assert.True(executed.Success);
        Assert.Equal(200m, executed.Output!.Get("outstanding"));
    }
}
