using Flow.Compiler.Candidate;
using Flow.Compiler.Tests.Fakes;
using Flow.Contracts;
using Flow.Runtime;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Compiler.Tests;

public class PromptCompilerCheckpointTests
{
    private static readonly ObjectType CustomerType = new("Customer", new Dictionary<string, FlowType>
    {
        ["id"] = new SemanticType("CustomerId", PrimitiveType.String),
        ["email"] = new SemanticType("Email", PrimitiveType.String)
    });

    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition("crm.findCustomer",
            new ObjectType("In", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            CustomerType, ToolEffect.Read, ToolRetryPolicy.Safe),
        new ToolDefinition("billing.createRefund",
            new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = new SemanticType("CustomerId", PrimitiveType.String) }),
            new ObjectType("Refund", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            ToolEffect.Write, ToolRetryPolicy.Never)
    });

    private static WorkflowSource Source() => new(
        "Find the customer by email and refund them.",
        new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
        new ObjectType("Result", new Dictionary<string, FlowType> { ["refundId"] = PrimitiveType.String }));

    private static CandidateWorkflowAst CandidateBinding(string boundField) => new(
        new CandidateWorkflowBody(
            "RefundCustomer",
            new CandidateNode[]
            {
                new CandidateCallNode("customer", "crm.findCustomer",
                    new Dictionary<string, CandidateExpression> { ["email"] = new CandidatePathExpression("input.email") }),
                new CandidateCallNode("refund", "billing.createRefund",
                    new Dictionary<string, CandidateExpression> { ["customerId"] = new CandidatePathExpression($"customer.{boundField}") })
            },
            new Dictionary<string, CandidateExpression> { ["refundId"] = new CandidatePathExpression("refund.id") }),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    private sealed class FakeInvoker : IMcpInvoker
    {
        public string? RefundedCustomerId { get; private set; }

        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            object? result = toolName switch
            {
                "crm.findCustomer" => new FlowRecord(new Dictionary<string, object?> { ["id"] = "cust-1", ["email"] = "ada@example.com" }),
                "billing.createRefund" => Handle(arguments),
                _ => throw new InvalidOperationException($"Unexpected tool '{toolName}'.")
            };
            return Task.FromResult(result);

            object? Handle(IReadOnlyDictionary<string, object?> args)
            {
                RefundedCustomerId = (string)args["customerId"]!;
                return new FlowRecord(new Dictionary<string, object?> { ["id"] = "refund-1" });
            }
        }
    }

    [Fact]
    public async Task SingleValidCandidate_CompilesAndExecutesCorrectly()
    {
        var model = new ScriptedSemanticCompilerModel(CandidateBinding("id"));
        var compiler = new PromptCompiler(model);

        var result = await compiler.CompileAsync(Source(), Catalog(), CancellationToken.None);

        Assert.Equal(CompilationStatus.Success, result.Status);
        Assert.NotNull(result.Workflow);

        var invoker = new FakeInvoker();
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "ada@example.com" });
        var executed = await new WorkflowExecutor().ExecuteAsync(result.Workflow!, input, invoker);

        Assert.True(executed.Success);
        Assert.Equal("refund-1", executed.Output!.Get("refundId"));
        Assert.Equal("cust-1", invoker.RefundedCustomerId);
    }

    [Fact]
    public async Task FirstCandidateHasSuspiciousBinding_RepairLoopFixesIt_BeforeExecuting()
    {
        var model = new ScriptedSemanticCompilerModel(CandidateBinding("email"), CandidateBinding("id"));
        var compiler = new PromptCompiler(model);

        var result = await compiler.CompileAsync(Source(), Catalog(), CancellationToken.None);

        Assert.Equal(CompilationStatus.Success, result.Status);
        var repairRequest = Assert.Single(model.RepairRequestsReceived);
        Assert.Contains(repairRequest.Diagnostics, d => d.Code == "S201");

        var invoker = new FakeInvoker();
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "ada@example.com" });
        var executed = await new WorkflowExecutor().ExecuteAsync(result.Workflow!, input, invoker);

        Assert.True(executed.Success);
        Assert.Equal("cust-1", invoker.RefundedCustomerId);
    }
}
