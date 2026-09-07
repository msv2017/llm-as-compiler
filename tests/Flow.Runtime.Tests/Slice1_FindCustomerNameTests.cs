using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Runtime.Tests;

public class Slice1_FindCustomerNameTests
{
    private sealed class FakeInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            Assert.Equal("crm.getCustomerById", toolName);
            return Task.FromResult<object?>(new FlowRecord(new Dictionary<string, object?>
            {
                ["id"] = arguments["id"],
                ["name"] = "Ada Lovelace"
            }));
        }
    }

    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition(
            "crm.getCustomerById",
            new ObjectType("GetCustomerByIdInput", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            new ObjectType("Customer", new Dictionary<string, FlowType>
            {
                ["id"] = PrimitiveType.String,
                ["name"] = PrimitiveType.String
            }),
            ToolEffect.Read,
            ToolRetryPolicy.Safe)
    });

    private static WorkflowDefinition BuildWorkflow()
    {
        var call = new CallNode("customer", "crm.getCustomerById",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("customer.name")
        });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });

        return new WorkflowDefinition("FindCustomerName", inputType, outputType, new WorkflowNode[] { call }, returnNode);
    }

    private static WorkflowValidator BuildValidator() => new(new IWorkflowValidationPass[]
    {
        new ToolResolutionValidator(),
        new DataflowValidator(),
        new TypeValidator(),
        new OutputValidator()
    });

    [Fact]
    public async Task ValidWorkflow_PassesValidation_AndExecutesToExpectedOutput()
    {
        var workflow = BuildWorkflow();
        var catalog = Catalog();

        var diagnostics = BuildValidator().Validate(workflow, catalog);
        Assert.Empty(diagnostics);

        var input = new FlowRecord(new Dictionary<string, object?> { ["customerId"] = "42" });
        var result = await new WorkflowExecutor().ExecuteAsync(workflow, input, new FakeInvoker());

        Assert.True(result.Success);
        Assert.Equal("Ada Lovelace", result.Output!.Get("customerName"));
    }

    [Fact]
    public void WorkflowCallingUnknownTool_FailsValidation_BeforeExecution()
    {
        var call = new CallNode("customer", "crm.doesNotExist",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("customer.name")
        });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });
        var brokenWorkflow = new WorkflowDefinition("Broken", inputType, outputType, new WorkflowNode[] { call }, returnNode);

        var diagnostics = BuildValidator().Validate(brokenWorkflow, Catalog());

        Assert.Contains(diagnostics, d => d.Code == "F001");
    }
}
