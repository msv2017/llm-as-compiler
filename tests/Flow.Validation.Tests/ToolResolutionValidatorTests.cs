using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class ToolResolutionValidatorTests
{
    private static ToolCatalog CatalogWithFindCustomer() => new(new[]
    {
        new ToolDefinition(
            "crm.findCustomer",
            new ObjectType("FindCustomerInput", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new OptionalType(new ObjectType("Customer", new Dictionary<string, FlowType>
            {
                ["id"] = PrimitiveType.String,
                ["name"] = PrimitiveType.String
            })),
            ToolEffect.Read,
            ToolRetryPolicy.Safe)
    });

    [Fact]
    public void UnknownTool_ProducesF001()
    {
        var node = new CallNode("customer", "crm.doesNotExist",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var validator = new ToolResolutionValidator();

        var diagnostics = validator.Validate(WorkflowWith(node), CatalogWithFindCustomer());

        Assert.Contains(diagnostics, d => d.Code == "F001" && d.NodeId == "customer");
    }

    [Fact]
    public void UnknownArgument_ProducesF002()
    {
        var node = new CallNode("customer", "crm.findCustomer", new Dictionary<string, FlowExpression>
        {
            ["email"] = new PathExpression("input.email"),
            ["extra"] = new ConstantExpression("x", new ConstantProvenance(ConstantProvenanceKind.HandWritten))
        });
        var validator = new ToolResolutionValidator();

        var diagnostics = validator.Validate(WorkflowWith(node), CatalogWithFindCustomer());

        Assert.Contains(diagnostics, d => d.Code == "F002" && d.NodeId == "customer");
    }

    [Fact]
    public void MissingRequiredArgument_ProducesF003()
    {
        var node = new CallNode("customer", "crm.findCustomer", new Dictionary<string, FlowExpression>());
        var validator = new ToolResolutionValidator();

        var diagnostics = validator.Validate(WorkflowWith(node), CatalogWithFindCustomer());

        Assert.Contains(diagnostics, d => d.Code == "F003" && d.NodeId == "customer");
    }

    [Fact]
    public void ValidCall_ProducesNoDiagnostics()
    {
        var node = new CallNode("customer", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var validator = new ToolResolutionValidator();

        var diagnostics = validator.Validate(WorkflowWith(node), CatalogWithFindCustomer());

        Assert.Empty(diagnostics);
    }

    private static WorkflowDefinition WorkflowWith(CallNode node)
    {
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["customerName"] = new PathExpression("customer.name")
        });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });
        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { node }, returnNode);
    }
}
