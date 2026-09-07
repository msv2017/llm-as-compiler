using Flow.Contracts;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class ToolCatalogTests
{
    [Fact]
    public void TryGet_ReturnsTrueAndTool_WhenNameExists()
    {
        var tool = new ToolDefinition(
            "crm.findCustomer",
            new ObjectType("FindCustomerInput", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new OptionalType(new ObjectType("Customer", new Dictionary<string, FlowType>
            {
                ["id"] = PrimitiveType.String,
                ["name"] = PrimitiveType.String
            })),
            ToolEffect.Read,
            ToolRetryPolicy.Safe);

        var catalog = new ToolCatalog(new[] { tool });

        var found = catalog.TryGet("crm.findCustomer", out var result);

        Assert.True(found);
        Assert.Same(tool, result);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenNameMissing()
    {
        var catalog = new ToolCatalog(Array.Empty<ToolDefinition>());

        var found = catalog.TryGet("nonexistent.tool", out var result);

        Assert.False(found);
        Assert.Null(result);
    }
}
