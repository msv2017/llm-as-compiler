using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Runtime.Tests;

public class Slice2_FindCustomerByEmailOrEmptyTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    private sealed class FakeInvoker : IMcpInvoker
    {
        private readonly FlowRecord? _customer;
        public FakeInvoker(FlowRecord? customer) => _customer = customer;
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(_customer);
    }

    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition(
            "crm.findCustomer",
            new ObjectType("FindCustomerInput", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new OptionalType(new ObjectType("Customer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String })),
            ToolEffect.Read,
            ToolRetryPolicy.Safe)
    });

    private static WorkflowDefinition BuildWorkflow()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var guarded = new IfNode(
            "customerName",
            new BinaryExpression(new PathExpression("customer"), BinaryOperator.Equal, new ConstantExpression(null, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("", Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new PathExpression("customer.name")));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["customerName"] = new PathExpression("customerName") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });
        return new WorkflowDefinition("FindCustomerByEmailOrEmpty", inputType, outputType, new WorkflowNode[] { call, guarded }, returnNode);
    }

    private static WorkflowValidator BuildValidator() => new(new IWorkflowValidationPass[]
    {
        new ToolResolutionValidator(),
        new DataflowValidator(),
        new TypeValidator(),
        new NullabilityValidator(),
        new OutputValidator()
    });

    [Fact]
    public async Task CustomerFound_ReturnsName()
    {
        var workflow = BuildWorkflow();
        Assert.Empty(BuildValidator().Validate(workflow, Catalog()));

        var invoker = new FakeInvoker(new FlowRecord(new Dictionary<string, object?> { ["name"] = "Grace Hopper" }));
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "grace@example.com" });

        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, invoker);

        Assert.Equal("Grace Hopper", result.Output!.Get("customerName"));
    }

    [Fact]
    public async Task CustomerNotFound_ReturnsEmptyString()
    {
        var workflow = BuildWorkflow();
        var invoker = new FakeInvoker(null);
        var input = new FlowRecord(new Dictionary<string, object?> { ["email"] = "missing@example.com" });

        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, invoker);

        Assert.Equal("", result.Output!.Get("customerName"));
    }

    [Fact]
    public void UnguardedDereference_FailsValidation_BeforeExecution()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("customer.name") // no null check — must be rejected
        });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });
        var brokenWorkflow = new WorkflowDefinition("Broken", inputType, outputType, new WorkflowNode[] { call }, returnNode);

        var diagnostics = BuildValidator().Validate(brokenWorkflow, Catalog());

        Assert.Contains(diagnostics, d => d.Code == "T103");
    }
}
