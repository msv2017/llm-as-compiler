using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class WorkflowValidatorTests
{
    private static readonly IReadOnlyList<IWorkflowValidationPass> Slice1Passes = new IWorkflowValidationPass[]
    {
        new ToolResolutionValidator(),
        new DataflowValidator(),
        new TypeValidator(),
        new OutputValidator()
    };

    [Fact]
    public void AggregatesDiagnosticsFromEveryPass()
    {
        var call = new CallNode("customer", "crm.doesNotExist",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>()); // missing required "name" too
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { call }, returnNode);

        var validator = new WorkflowValidator(Slice1Passes);
        var diagnostics = validator.Validate(workflow, new ToolCatalog(Array.Empty<ToolDefinition>()));

        Assert.Contains(diagnostics, d => d.Code == "F001");
        Assert.Contains(diagnostics, d => d.Code == "O601");
    }

    [Fact]
    public void ValidWorkflow_ProducesNoDiagnostics()
    {
        var tool = new ToolDefinition(
            "crm.findCustomer",
            new ObjectType("FindCustomerInput", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new ObjectType("Customer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String }),
            ToolEffect.Read,
            ToolRetryPolicy.Safe);
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression("customer.name") });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { call }, returnNode);

        var validator = new WorkflowValidator(Slice1Passes);
        var diagnostics = validator.Validate(workflow, new ToolCatalog(new[] { tool }));

        Assert.Empty(diagnostics);
    }
}
