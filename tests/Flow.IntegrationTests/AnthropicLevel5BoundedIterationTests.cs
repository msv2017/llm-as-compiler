using Flow.Compiler;
using Flow.Compiler.Providers;
using Flow.Contracts;
using Flow.Runtime;
using Flow.TypeSystem;
using Xunit;

namespace Flow.IntegrationTests;

// Mirrors Level5BoundedIterationTests.cs (OpenAI), against the Anthropic provider.
public class AnthropicLevel5BoundedIterationTests
{
    private sealed class FakeInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            object? result = toolName switch
            {
                "subscriptions.listActive" => new List<object?>
                {
                    new FlowRecord(new Dictionary<string, object?> { ["id"] = "sub-1" }),
                    new FlowRecord(new Dictionary<string, object?> { ["id"] = "sub-2" })
                },
                "subscriptions.getRenewalDate" => new FlowRecord(new Dictionary<string, object?>
                {
                    ["date"] = arguments["subscriptionId"]!.Equals("sub-1") ? new DateTime(2026, 3, 1) : new DateTime(2026, 6, 1)
                }),
                _ => throw new InvalidOperationException($"Unexpected tool '{toolName}'.")
            };
            return Task.FromResult<object?>(result);
        }
    }

    [Fact]
    public async Task ActiveSubscriptionRenewalDates_CompilesAndExecutesCorrectly()
    {
        if (AnthropicTestEnvironment.ApiKey is null) return; // no ANTHROPIC_API_KEY configured

        var subscriptionType = new ObjectType("Subscription", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });
        var renewalInfoType = new ObjectType("RenewalInfo", new Dictionary<string, FlowType> { ["date"] = PrimitiveType.DateTime });
        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("subscriptions.listActive",
                new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
                new ListType(subscriptionType), ToolEffect.Read, ToolRetryPolicy.Safe),
            new ToolDefinition("subscriptions.getRenewalDate",
                new ObjectType("In", new Dictionary<string, FlowType> { ["subscriptionId"] = PrimitiveType.String }),
                renewalInfoType, ToolEffect.Read, ToolRetryPolicy.Safe)
        });
        var source = new WorkflowSource(
            "Given a customer id, list up to 20 of the customer's active subscriptions, and for each one, call " +
            "the renewal tool to get its renewal date. Return the list of renewal dates.",
            new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
            new ObjectType("Result", new Dictionary<string, FlowType> { ["renewalDates"] = new ListType(PrimitiveType.DateTime) }));

        var compiler = new PromptCompiler(AnthropicSemanticCompilerModel.FromEnvironment());
        var result = await compiler.CompileAsync(source, tools);

        Assert.Equal(CompilationStatus.Success, result.Status);

        var input = new FlowRecord(new Dictionary<string, object?> { ["customerId"] = "cust-1" });
        var executed = await new WorkflowExecutor().ExecuteAsync(result.Workflow!, input, new FakeInvoker());

        Assert.True(executed.Success);
        var dates = Assert.IsAssignableFrom<System.Collections.IEnumerable>(executed.Output!.Get("renewalDates"))
            .Cast<DateTime>().ToList();
        Assert.Equal(2, dates.Count);
        Assert.Contains(new DateTime(2026, 3, 1), dates);
        Assert.Contains(new DateTime(2026, 6, 1), dates);
    }
}
