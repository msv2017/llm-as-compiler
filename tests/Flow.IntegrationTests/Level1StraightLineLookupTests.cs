using Flow.Compiler;
using Flow.Compiler.Providers;
using Flow.Contracts;
using Flow.Runtime;
using Flow.TypeSystem;
using Xunit;

namespace Flow.IntegrationTests;

public class Level1StraightLineLookupTests
{
    private sealed class FakeInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(new FlowRecord(new Dictionary<string, object?> { ["name"] = "Ada Lovelace" }));
    }

    [Fact]
    public async Task FindCustomerName_CompilesAndExecutesCorrectly()
    {
        if (OpenAiTestEnvironment.ApiKey is null) return; // no OPENAI_API_KEY configured

        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("crm.getCustomerById",
                new ObjectType("In", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
                new ObjectType("Customer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String }),
                ToolEffect.Read, ToolRetryPolicy.Safe)
        });
        var source = new WorkflowSource(
            "Given a customer id, look up and return the customer's name.",
            new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
            new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String }));

        var compiler = new PromptCompiler(OpenAiSemanticCompilerModel.FromEnvironment());
        var result = await compiler.CompileAsync(source, tools);

        Assert.Equal(CompilationStatus.Success, result.Status);

        var input = new FlowRecord(new Dictionary<string, object?> { ["customerId"] = "42" });
        var executed = await new WorkflowExecutor().ExecuteAsync(result.Workflow!, input, new FakeInvoker());

        Assert.True(executed.Success);
        Assert.Equal("Ada Lovelace", executed.Output!.Get("customerName"));
    }
}
