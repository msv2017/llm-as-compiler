using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Runtime.Tests;

public class WorkflowExecutorTests
{
    private sealed class FakeInvoker : IMcpInvoker
    {
        public IReadOnlyDictionary<string, object?>? ReceivedArguments { get; private set; }

        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            ReceivedArguments = arguments;
            return Task.FromResult<object?>(new FlowRecord(new Dictionary<string, object?> { ["name"] = "Ada Lovelace" }));
        }
    }

    [Fact]
    public async Task ExecuteAsync_PassesEvaluatedArguments_AndBindsCallResult()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression("customer.name") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { call }, returnNode);

        var invoker = new FakeInvoker();
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "ada@example.com" });

        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, invoker);

        Assert.Equal("ada@example.com", invoker.ReceivedArguments!["email"]);
        Assert.True(result.Success);
        Assert.Equal("Ada Lovelace", result.Output!.Get("name"));
    }
}
