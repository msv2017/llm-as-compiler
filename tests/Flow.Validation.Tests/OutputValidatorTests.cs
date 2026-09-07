using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class OutputValidatorTests
{
    private static readonly ToolCatalog EmptyCatalog = new(Array.Empty<ToolDefinition>());

    [Fact]
    public void MissingRequiredField_ProducesO601()
    {
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType>
        {
            ["customerName"] = PrimitiveType.String,
            ["outstanding"] = PrimitiveType.Decimal
        });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("input.email")
        });
        var workflow = new WorkflowDefinition("Test", outputType, outputType, Array.Empty<WorkflowNode>(), returnNode);

        var diagnostics = new OutputValidator().Validate(workflow, EmptyCatalog);

        Assert.Contains(diagnostics, d => d.Code == "O601");
    }

    [Fact]
    public void UnknownField_ProducesO602()
    {
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("input.email"),
            ["unexpected"] = new PathExpression("input.email")
        });
        var workflow = new WorkflowDefinition("Test", outputType, outputType, Array.Empty<WorkflowNode>(), returnNode);

        var diagnostics = new OutputValidator().Validate(workflow, EmptyCatalog);

        Assert.Contains(diagnostics, d => d.Code == "O602");
    }

    [Fact]
    public void ExactMatch_ProducesNoDiagnostics()
    {
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("input.email")
        });
        var workflow = new WorkflowDefinition("Test", outputType, outputType, Array.Empty<WorkflowNode>(), returnNode);

        var diagnostics = new OutputValidator().Validate(workflow, EmptyCatalog);

        Assert.Empty(diagnostics);
    }
}
