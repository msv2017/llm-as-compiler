using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class ObjectExpressionTypeValidationTests
{
    private static readonly ToolCatalog EmptyCatalog = new(Array.Empty<ToolDefinition>());
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    [Fact]
    public void IfNode_BranchesWithMismatchedObjectExpressionShapes_ProducesT101()
    {
        var ifNode = new IfNode(
            "result",
            new ConstantExpression(true, Provenance),
            new IfBranch(Array.Empty<WorkflowNode>(), new ObjectExpression(new Dictionary<string, FlowExpression>
            {
                ["a"] = new ConstantExpression("x", Provenance),
                ["b"] = new ConstantExpression(1, Provenance)
            })),
            new IfBranch(Array.Empty<WorkflowNode>(), new ObjectExpression(new Dictionary<string, FlowExpression>
            {
                ["a"] = new ConstantExpression("x", Provenance)
                // missing "b" — mismatched shape
            })));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["out"] = new PathExpression("result") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["out"] = PrimitiveType.String });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { ifNode }, returnNode);

        var diagnostics = new TypeValidator().Validate(workflow, EmptyCatalog);

        Assert.Contains(diagnostics, d => d.Code == "T101");
    }

    [Fact]
    public void IfNode_BranchesWithMatchingObjectExpressionShapes_ProducesNoDiagnostics()
    {
        var ifNode = new IfNode(
            "result",
            new ConstantExpression(true, Provenance),
            new IfBranch(Array.Empty<WorkflowNode>(), new ObjectExpression(new Dictionary<string, FlowExpression>
            {
                ["a"] = new ConstantExpression("x", Provenance),
                ["b"] = new ConstantExpression(1, Provenance)
            })),
            new IfBranch(Array.Empty<WorkflowNode>(), new ObjectExpression(new Dictionary<string, FlowExpression>
            {
                ["a"] = new ConstantExpression("y", Provenance),
                ["b"] = new ConstantExpression(2, Provenance)
            })));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["a"] = new PathExpression("result.a") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["a"] = PrimitiveType.String });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { ifNode }, returnNode);

        var diagnostics = new TypeValidator().Validate(workflow, EmptyCatalog);

        Assert.Empty(diagnostics);
    }
}
