using Xunit;

namespace Flow.Runtime.Tests;

public class FlowRecordTests
{
    [Fact]
    public void Fields_ExposesTheSameEntriesPassedToTheConstructor()
    {
        var record = new FlowRecord(new Dictionary<string, object?> { ["name"] = "Ada", ["age"] = 30 });

        Assert.Equal(2, record.Fields.Count);
        Assert.Equal("Ada", record.Fields["name"]);
        Assert.Equal(30, record.Fields["age"]);
    }
}
