using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class NullabilityValidatorTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    private static ToolCatalog CatalogWithFindCustomer() => new(new[]
    {
        new ToolDefinition(
            "crm.findCustomer",
            new ObjectType("FindCustomerInput", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new OptionalType(new ObjectType("Customer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String })),
            ToolEffect.Read,
            ToolRetryPolicy.Safe)
    });

    [Fact]
    public void DereferenceWithoutGuard_ProducesT103()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression("customer.name") });
        var workflow = WorkflowWith(new WorkflowNode[] { call }, returnNode);

        var diagnostics = new NullabilityValidator().Validate(workflow, CatalogWithFindCustomer());

        Assert.Contains(diagnostics, d => d.Code == "T103");
    }

    [Fact]
    public void DereferenceInNullCheckedFalseBranch_ProducesNoDiagnostics()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var guarded = new IfNode(
            "customerName",
            new BinaryExpression(new PathExpression("customer"), BinaryOperator.Equal, new ConstantExpression(null, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("", Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new PathExpression("customer.name")));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression("customerName") });
        var workflow = WorkflowWith(new WorkflowNode[] { call, guarded }, returnNode);

        var diagnostics = new NullabilityValidator().Validate(workflow, CatalogWithFindCustomer());

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void DereferenceInNullCheckedTrueBranch_ProducesT103()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var guarded = new IfNode(
            "customerName",
            new BinaryExpression(new PathExpression("customer"), BinaryOperator.Equal, new ConstantExpression(null, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new PathExpression("customer.name")), // wrong branch — customer IS null here
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("fallback", Provenance)));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression("customerName") });
        var workflow = WorkflowWith(new WorkflowNode[] { call, guarded }, returnNode);

        var diagnostics = new NullabilityValidator().Validate(workflow, CatalogWithFindCustomer());

        Assert.Contains(diagnostics, d => d.Code == "T103");
    }

    private static WorkflowDefinition WorkflowWith(IReadOnlyList<WorkflowNode> nodes, ReturnNode returnNode)
    {
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        return new WorkflowDefinition("Test", inputType, outputType, nodes, returnNode);
    }
}
