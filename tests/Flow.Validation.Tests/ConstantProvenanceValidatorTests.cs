using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class ConstantProvenanceValidatorTests
{
    private static ToolCatalog EmptyCatalog() => new(Array.Empty<ToolDefinition>());

    private static WorkflowDefinition WorkflowWithConstant(ConstantProvenance provenance)
    {
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["value"] = new ConstantExpression(30, provenance)
        });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["value"] = PrimitiveType.Int });
        return new WorkflowDefinition("W", inputType, outputType, Array.Empty<WorkflowNode>(), returnNode);
    }

    [Fact]
    public void UnprovenConstant_InReturnField_ProducesS204()
    {
        var workflow = WorkflowWithConstant(new ConstantProvenance(ConstantProvenanceKind.Unproven));
        var diagnostics = new ConstantProvenanceValidator().Validate(workflow, EmptyCatalog());

        Assert.Contains(diagnostics, d => d.Code == "S204");
    }

    [Theory]
    [InlineData(ConstantProvenanceKind.Prompt)]
    [InlineData(ConstantProvenanceKind.ToolSchema)]
    [InlineData(ConstantProvenanceKind.SystemPolicy)]
    [InlineData(ConstantProvenanceKind.ExplicitCompilerPolicy)]
    [InlineData(ConstantProvenanceKind.HandWritten)]
    public void ProvenConstant_DoesNotProduceS204(ConstantProvenanceKind kind)
    {
        var workflow = WorkflowWithConstant(new ConstantProvenance(kind));
        var diagnostics = new ConstantProvenanceValidator().Validate(workflow, EmptyCatalog());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void UnprovenConstant_NestedInsideBinaryInsideIfCondition_IsDetected()
    {
        var ifNode = new IfNode(
            "check",
            new BinaryExpression(new PathExpression("input.x"), BinaryOperator.GreaterThan,
                new ConstantExpression(5, new ConstantProvenance(ConstantProvenanceKind.Unproven))),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(true, new ConstantProvenance(ConstantProvenanceKind.Prompt))),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(false, new ConstantProvenance(ConstantProvenanceKind.Prompt))));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["flag"] = new PathExpression("check") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["x"] = PrimitiveType.Int });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["flag"] = PrimitiveType.Bool });
        var workflow = new WorkflowDefinition("W", inputType, outputType, new WorkflowNode[] { ifNode }, returnNode);

        var diagnostics = new ConstantProvenanceValidator().Validate(workflow, EmptyCatalog());

        Assert.Contains(diagnostics, d => d.Code == "S204" && d.NodeId == "check");
    }
}
