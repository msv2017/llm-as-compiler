using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.IR.Tests;

public class WorkflowDefinitionTests
{
    [Fact]
    public void CallNode_StoresToolNameAndArguments()
    {
        var node = new CallNode(
            "customer",
            "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });

        Assert.Equal("customer", node.Id);
        Assert.Equal("crm.findCustomer", node.ToolName);
        Assert.IsType<PathExpression>(node.Arguments["email"]);
    }

    [Fact]
    public void ReturnNode_HasFixedId()
    {
        var node = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("customer.name")
        });

        Assert.Equal("return", node.Id);
    }

    [Fact]
    public void WorkflowDefinition_HoldsNodesAndReturn()
    {
        var callNode = new CallNode(
            "customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("customer.name")
        });

        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });

        var workflow = new WorkflowDefinition("FindCustomerName", inputType, outputType, new WorkflowNode[] { callNode }, returnNode);

        Assert.Equal("FindCustomerName", workflow.Name);
        Assert.Single(workflow.Nodes);
        Assert.Same(returnNode, workflow.Return);
    }
}
