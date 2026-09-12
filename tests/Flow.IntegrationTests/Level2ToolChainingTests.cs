using Flow.Compiler;
using Flow.Compiler.Providers;
using Flow.Contracts;
using Flow.Runtime;
using Flow.TypeSystem;
using Xunit;

namespace Flow.IntegrationTests;

public class Level2ToolChainingTests
{
    private sealed class FakeInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            object? result = toolName switch
            {
                "crm.findCustomerByEmail" => new FlowRecord(new Dictionary<string, object?> { ["id"] = "cust-1", ["email"] = "ada@example.com" }),
                "loyalty.getTier" => new FlowRecord(new Dictionary<string, object?> { ["tier"] = "Gold" }),
                _ => throw new InvalidOperationException($"Unexpected tool '{toolName}'.")
            };
            return Task.FromResult<object?>(result);
        }
    }

    [Fact]
    public async Task FindLoyaltyTierByEmail_CompilesAndExecutesCorrectly()
    {
        if (OpenAiTestEnvironment.ApiKey is null) return; // no OPENAI_API_KEY configured

        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("crm.findCustomerByEmail",
                new ObjectType("In", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
                new ObjectType("Customer", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String, ["email"] = PrimitiveType.String }),
                ToolEffect.Read, ToolRetryPolicy.Safe),
            new ToolDefinition("loyalty.getTier",
                new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
                new ObjectType("LoyaltyInfo", new Dictionary<string, FlowType> { ["tier"] = PrimitiveType.String }),
                ToolEffect.Read, ToolRetryPolicy.Safe)
        });
        var source = new WorkflowSource(
            "Given a customer's email, find the customer, then look up their loyalty tier using the customer's id. Return the tier.",
            new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new ObjectType("Result", new Dictionary<string, FlowType> { ["tier"] = PrimitiveType.String }));

        var compiler = new PromptCompiler(OpenAiSemanticCompilerModel.FromEnvironment());
        var result = await compiler.CompileAsync(source, tools);

        Assert.Equal(CompilationStatus.Success, result.Status);

        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "ada@example.com" });
        var executed = await new WorkflowExecutor().ExecuteAsync(result.Workflow!, input, new FakeInvoker());

        Assert.True(executed.Success);
        Assert.Equal("Gold", executed.Output!.Get("tier"));
    }
}
