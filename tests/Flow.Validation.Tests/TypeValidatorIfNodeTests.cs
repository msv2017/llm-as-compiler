using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class TypeValidatorIfNodeTests
{
    private static readonly ToolCatalog EmptyCatalog = new(Array.Empty<ToolDefinition>());
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    [Fact]
    public void MismatchedBranchTypes_ProduceT101()
    {
        var ifNode = new IfNode(
            "value",
            new BinaryExpression(new PathExpression("input.flag"), BinaryOperator.Equal, new ConstantExpression(true, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(1, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("text", Provenance)));

        var diagnostics = new TypeValidator().Validate(WorkflowWith(ifNode), EmptyCatalog);

        Assert.Contains(diagnostics, d => d.Code == "T101" && d.NodeId == "value");
    }

    [Fact]
    public void MatchingBranchTypes_ProduceNoDiagnostics()
    {
        var ifNode = new IfNode(
            "value",
            new BinaryExpression(new PathExpression("input.flag"), BinaryOperator.Equal, new ConstantExpression(true, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("a", Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("b", Provenance)));

        var diagnostics = new TypeValidator().Validate(WorkflowWith(ifNode), EmptyCatalog);

        Assert.Empty(diagnostics);
    }

    private static WorkflowDefinition WorkflowWith(IfNode ifNode)
    {
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression("value") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["flag"] = PrimitiveType.Bool });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { ifNode }, returnNode);
    }
}
