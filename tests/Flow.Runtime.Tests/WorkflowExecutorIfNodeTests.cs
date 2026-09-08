using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Runtime.Tests;

public class WorkflowExecutorIfNodeTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    private sealed class FakeInvoker : IMcpInvoker
    {
        private readonly object? _result;
        public FakeInvoker(object? result) => _result = result;
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private static WorkflowDefinition BuildWorkflow()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var guarded = new IfNode(
            "customerName",
            new BinaryExpression(new PathExpression("customer"), BinaryOperator.Equal, new ConstantExpression(null, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("", Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new PathExpression("customer.name")));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression("customerName") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { call, guarded }, returnNode);
    }

    [Fact]
    public async Task CustomerFound_TakesFalseBranch_ReturnsName()
    {
        var invoker = new FakeInvoker(new FlowRecord(new Dictionary<string, object?> { ["name"] = "Ada Lovelace" }));
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "ada@example.com" });

        var result = await new WorkflowExecutor().ExecuteAsync(BuildWorkflow(), input, invoker);

        Assert.Equal("Ada Lovelace", result.Output!.Get("name"));
    }

    [Fact]
    public async Task CustomerNotFound_TakesTrueBranch_ReturnsEmptyString()
    {
        var invoker = new FakeInvoker(null);
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "missing@example.com" });

        var result = await new WorkflowExecutor().ExecuteAsync(BuildWorkflow(), input, invoker);

        Assert.Equal("", result.Output!.Get("name"));
    }
}
