using Flow.Cli;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Cli.Tests;

public class CompiledWorkflowJsonTests
{
    private static WorkflowDefinition Fixture()
    {
        var call = new CallNode("customer", "crm.getCustomerById",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["customerName"] = new PathExpression("customer.name") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });
        return new WorkflowDefinition("FindCustomerName", inputType, outputType, new WorkflowNode[] { call }, returnNode);
    }

    [Fact]
    public void Write_ThenParse_RoundTripsNodesAndTypes()
    {
        var workflow = Fixture();

        var json = CompiledWorkflowJson.Write(workflow);
        var parsed = CompiledWorkflowJson.Parse(json);

        Assert.Equal("FindCustomerName", parsed.Name);
        var call = Assert.IsType<CallNode>(Assert.Single(parsed.Nodes));
        Assert.Equal("crm.getCustomerById", call.ToolName);
        var arg = Assert.IsType<PathExpression>(call.Arguments["id"]);
        Assert.Equal("input.customerId", arg.Path);
        var returnExpr = Assert.IsType<PathExpression>(parsed.Return.Fields["customerName"]);
        Assert.Equal("customer.name", returnExpr.Path);
        Assert.IsType<ObjectType>(parsed.InputType);
        Assert.Equal("Request", ((ObjectType)parsed.InputType).Name);
        Assert.IsType<ObjectType>(parsed.OutputType);
        Assert.Equal("Result", ((ObjectType)parsed.OutputType).Name);
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsScenarioParseException()
    {
        Assert.Throws<ScenarioParseException>(() => CompiledWorkflowJson.Parse("{ not valid json"));
    }

    [Fact]
    public void Parse_MissingWorkflow_ThrowsScenarioParseException()
    {
        var json = """{ "inputType": { "kind": "primitive", "name": "String" }, "outputType": { "kind": "primitive", "name": "String" } }""";

        var ex = Assert.Throws<ScenarioParseException>(() => CompiledWorkflowJson.Parse(json));
        Assert.Contains("workflow", ex.Message);
    }
}
