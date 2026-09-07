using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class TypeValidatorTests
{
    private static ToolCatalog CatalogWithFindCustomer() => new(new[]
    {
        new ToolDefinition(
            "crm.findCustomer",
            new ObjectType("FindCustomerInput", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new ObjectType("Customer", new Dictionary<string, FlowType>
            {
                ["id"] = PrimitiveType.String,
                ["name"] = PrimitiveType.String
            }),
            ToolEffect.Read,
            ToolRetryPolicy.Safe)
    });

    [Fact]
    public void MismatchedArgumentType_ProducesT101()
    {
        var call = new CallNode("customer", "crm.findCustomer", new Dictionary<string, FlowExpression>
        {
            ["email"] = new ConstantExpression(42, new ConstantProvenance(ConstantProvenanceKind.HandWritten))
        });
        var workflow = WorkflowWith(call, "customer.name");

        var diagnostics = new TypeValidator().Validate(workflow, CatalogWithFindCustomer());

        Assert.Contains(diagnostics, d => d.Code == "T101" && d.NodeId == "customer");
    }

    [Fact]
    public void MismatchedReturnFieldType_ProducesT101()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var workflow = WorkflowWith(call, "customer.id"); // customer.id is String, "name" output field expects String too — force a mismatch instead:
        var badReturn = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["name"] = new ConstantExpression(true, new ConstantProvenance(ConstantProvenanceKind.HandWritten))
        });
        var badWorkflow = workflow with { Return = badReturn };

        var diagnostics = new TypeValidator().Validate(badWorkflow, CatalogWithFindCustomer());

        Assert.Contains(diagnostics, d => d.Code == "T101" && d.NodeId == "return");
    }

    [Fact]
    public void MatchingTypes_ProduceNoDiagnostics()
    {
        var call = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var workflow = WorkflowWith(call, "customer.name");

        var diagnostics = new TypeValidator().Validate(workflow, CatalogWithFindCustomer());

        Assert.Empty(diagnostics);
    }

    private static WorkflowDefinition WorkflowWith(CallNode call, string returnPath)
    {
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression(returnPath) });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { call }, returnNode);
    }
}
