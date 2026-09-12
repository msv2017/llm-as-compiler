using Flow.Compiler.Providers;
using Flow.Compiler.Tests.Fakes;
using Xunit;

namespace Flow.Compiler.Tests;

public class FakeChatCompletionClientTests
{
    [Fact]
    public async Task CompleteAsync_ReturnsFirstScriptedResponse_AndRecordsTheCall()
    {
        var client = new FakeChatCompletionClient("{\"first\":true}", "{\"second\":true}");

        var result = await client.CompleteAsync("system-1", "user-1", "schema-1", CancellationToken.None);

        Assert.Equal("{\"first\":true}", result);
        var call = Assert.Single(client.CallsReceived);
        Assert.Equal("system-1", call.SystemPrompt);
        Assert.Equal("user-1", call.UserPrompt);
        Assert.Equal("schema-1", call.JsonSchema);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsResponsesInOrder()
    {
        var client = new FakeChatCompletionClient("{\"first\":true}", "{\"second\":true}");

        await client.CompleteAsync("s", "u", "j", CancellationToken.None);
        var second = await client.CompleteAsync("s", "u", "j", CancellationToken.None);

        Assert.Equal("{\"second\":true}", second);
        Assert.Equal(2, client.CallsReceived.Count);
    }

    [Fact]
    public async Task CompleteAsync_PastScriptedResponses_Throws()
    {
        var client = new FakeChatCompletionClient("{\"only\":true}");

        await client.CompleteAsync("s", "u", "j", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CompleteAsync("s", "u", "j", CancellationToken.None));
    }
}
