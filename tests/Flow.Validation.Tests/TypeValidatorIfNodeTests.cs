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

    private static ToolCatalog CatalogWithLookup() => new(new[]
    {
        new ToolDefinition(
            "crm.lookup",
            new ObjectType("LookupInput", new Dictionary<string, FlowType>()),
            new ObjectType("LookupResult", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String }),
            ToolEffect.Read,
            ToolRetryPolicy.Safe)
    });

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

    [Fact]
    public void BranchLocalNodeOutputType_ThreadedIntoBranchValue_ProducesNoDiagnostics()
    {
        var call = new CallNode("lookup", "crm.lookup", new Dictionary<string, FlowExpression>());
        var ifNode = new IfNode(
            "value",
            new BinaryExpression(new PathExpression("input.flag"), BinaryOperator.Equal, new ConstantExpression(true, Provenance)),
            new IfBranch(new WorkflowNode[] { call }, new PathExpression("lookup.name")),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("fallback", Provenance)));

        var diagnostics = new TypeValidator().Validate(WorkflowWith(ifNode), CatalogWithLookup());

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
