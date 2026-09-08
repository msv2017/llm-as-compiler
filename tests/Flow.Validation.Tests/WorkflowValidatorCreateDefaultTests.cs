using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class WorkflowValidatorCreateDefaultTests
{
    [Fact]
    public void CreateDefault_RunsEveryPhase1Pass()
    {
        // An unknown tool (F001, from ToolResolutionValidator) AND a zero foreach limit (B501, from
        // BoundednessValidator) together prove CreateDefault() wires up passes from both "ends" of the list.
        var foreachNode = new ForeachNode(
            "results", new PathExpression("input.items"), "item", 0,
            new WorkflowNode[]
            {
                new CallNode("call", "does.not.exist", new Dictionary<string, FlowExpression>())
            },
            new PathExpression("call"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["results"] = new PathExpression("results") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["items"] = new ListType(PrimitiveType.String) });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["results"] = new ListType(PrimitiveType.String) });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { foreachNode }, returnNode);

        var diagnostics = WorkflowValidator.CreateDefault().Validate(workflow, new ToolCatalog(Array.Empty<ToolDefinition>()));

        Assert.Contains(diagnostics, d => d.Code == "F001");
        Assert.Contains(diagnostics, d => d.Code == "B501");
    }
}
