using Flow.Compiler;
using Flow.Compiler.Providers;
using Flow.Contracts;
using Flow.Runtime;
using Flow.TypeSystem;
using Xunit;

namespace Flow.IntegrationTests;

// Not yet live-verified against the real Anthropic API (no ANTHROPIC_API_KEY was available when
// this was written -- see docs/superpowers/specs/2026-09-13-anthropic-provider-design.md). This
// exists so that a future session with a real key can run it immediately, the same way
// Level1StraightLineLookupTests.cs did for the OpenAI provider's first live run -- which found two
// real bugs (DataflowValidator, T104) that no amount of scripted-fake testing caught.
public class AnthropicProviderTests
{
    private sealed class FakeInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(new FlowRecord(new Dictionary<string, object?> { ["name"] = "Ada Lovelace" }));
    }

    [Fact]
    public async Task FindCustomerName_CompilesAndExecutesCorrectly()
    {
        if (AnthropicTestEnvironment.ApiKey is null) return; // no ANTHROPIC_API_KEY configured

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

        var compiler = new PromptCompiler(AnthropicSemanticCompilerModel.FromEnvironment());
        var result = await compiler.CompileAsync(source, tools);

        Assert.Equal(CompilationStatus.Success, result.Status);

        var input = new FlowRecord(new Dictionary<string, object?> { ["customerId"] = "42" });
        var executed = await new WorkflowExecutor().ExecuteAsync(result.Workflow!, input, new FakeInvoker());

        Assert.True(executed.Success);
        Assert.Equal("Ada Lovelace", executed.Output!.Get("customerName"));
    }
}
