using Flow.Compiler.Providers;

namespace Flow.Compiler.Tests.Fakes;

public sealed record ChatCompletionCall(string SystemPrompt, string UserPrompt, string JsonSchema);

public sealed class FakeChatCompletionClient : IChatCompletionClient
{
    private readonly Queue<string> _responses;

    public FakeChatCompletionClient(params string[] responses) => _responses = new Queue<string>(responses);

    public List<ChatCompletionCall> CallsReceived { get; } = new();

    public Task<string> CompleteAsync(
        string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken)
    {
        CallsReceived.Add(new ChatCompletionCall(systemPrompt, userPrompt, jsonSchema));
        return Task.FromResult(
            _responses.Count > 0 ? _responses.Dequeue() : throw new InvalidOperationException("No more scripted responses."));
    }
}
