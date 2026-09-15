using Flow.Cli.Runtime;
using Flow.Runtime;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Flow.Cli.Tests.Runtime;

public class McpToolInvokerTests
{
    [Fact]
    public void ConvertResult_IsError_ThrowsWithTextContentInMessage()
    {
        var result = new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock> { new TextContentBlock { Text = "customer not found" } }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => McpToolInvoker.ConvertResult(result, "crm.findCustomer"));
        Assert.Contains("crm.findCustomer", ex.Message);
        Assert.Contains("customer not found", ex.Message);
    }

    [Fact]
    public void ConvertResult_NoStructuredContent_Throws()
    {
        var result = new CallToolResult { IsError = false, Content = new List<ContentBlock>() };

        var ex = Assert.Throws<InvalidOperationException>(() => McpToolInvoker.ConvertResult(result, "crm.findCustomer"));
        Assert.Contains("structured content", ex.Message);
    }

    [Fact]
    public void ConvertResult_StructuredContent_ConvertsToFlowRecord()
    {
        using var document = System.Text.Json.JsonDocument.Parse("""{ "name": "Ada Lovelace" }""");
        var result = new CallToolResult
        {
            IsError = false,
            Content = new List<ContentBlock>(),
            StructuredContent = document.RootElement.Clone()
        };

        var value = Assert.IsType<FlowRecord>(McpToolInvoker.ConvertResult(result, "crm.findCustomer"));
        Assert.Equal("Ada Lovelace", value.Get("name"));
    }
}
