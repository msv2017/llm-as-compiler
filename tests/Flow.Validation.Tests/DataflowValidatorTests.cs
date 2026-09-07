using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class DataflowValidatorTests
{
    private static readonly ToolCatalog EmptyCatalog = new(Array.Empty<ToolDefinition>());

    [Fact]
    public void PathReferencingUndefinedNode_ProducesD301()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("doesNotExist.value") });
        var workflow = WorkflowWith(call, "customer.id");

        var diagnostics = new DataflowValidator().Validate(workflow, EmptyCatalog);

        Assert.Contains(diagnostics, d => d.Code == "D301");
    }

    [Fact]
    public void PathReferencingOwnNode_ProducesD302()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("customer.email") });
        var workflow = WorkflowWith(call, "customer.id");

        var diagnostics = new DataflowValidator().Validate(workflow, EmptyCatalog);

        Assert.Contains(diagnostics, d => d.Code == "D302");
    }

    [Fact]
    public void ValidPathsToInputAndPriorNode_ProduceNoDiagnostics()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var workflow = WorkflowWith(call, "customer.id");

        var diagnostics = new DataflowValidator().Validate(workflow, EmptyCatalog);

        Assert.Empty(diagnostics);
    }

    private static WorkflowDefinition WorkflowWith(CallNode call, string returnPath)
    {
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["id"] = new PathExpression(returnPath) });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });
        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { call }, returnNode);
    }
}
