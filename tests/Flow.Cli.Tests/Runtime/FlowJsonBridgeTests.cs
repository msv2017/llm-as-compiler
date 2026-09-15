using System.Text.Json;
using Flow.Cli.Runtime;
using Flow.Runtime;
using Xunit;

namespace Flow.Cli.Tests.Runtime;

public class FlowJsonBridgeTests
{
    [Fact]
    public void ToFlowValue_ConvertsObjectArrayAndPrimitives()
    {
        using var document = JsonDocument.Parse("""
        { "name": "Ada", "age": 30, "balance": 12.5, "active": true, "tags": ["a", "b"], "note": null }
        """);

        var value = Assert.IsType<FlowRecord>(FlowJsonBridge.ToFlowValue(document.RootElement));

        Assert.Equal("Ada", value.Get("name"));
        Assert.Equal(30L, value.Get("age"));
        Assert.Equal(12.5m, value.Get("balance"));
        Assert.Equal(true, value.Get("active"));
        Assert.Equal(new List<object?> { "a", "b" }, value.Get("tags"));
        Assert.Null(value.Get("note"));
    }

    [Fact]
    public void ToJsonValue_ConvertsFlowRecordAndListBack()
    {
        var record = new FlowRecord(new Dictionary<string, object?>
        {
            ["name"] = "Ada",
            ["tags"] = new List<object?> { "a", "b" },
            ["age"] = 30L,
            ["balance"] = 12.5m,
            ["active"] = true,
            ["note"] = null
        });

        var node = FlowJsonBridge.ToJsonValue(record);
        var json = node!.ToJsonString();
        var roundTripped = FlowJsonBridge.ToFlowValue(JsonDocument.Parse(json).RootElement);

        var roundTrippedRecord = Assert.IsType<FlowRecord>(roundTripped);
        Assert.Equal("Ada", roundTrippedRecord.Get("name"));
        Assert.Equal(new List<object?> { "a", "b" }, roundTrippedRecord.Get("tags"));
        Assert.Equal(30L, roundTrippedRecord.Get("age"));
        Assert.Equal(12.5m, roundTrippedRecord.Get("balance"));
        Assert.Equal(true, roundTrippedRecord.Get("active"));
        Assert.Null(roundTrippedRecord.Get("note"));
    }
}
