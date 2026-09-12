namespace Flow.Compiler.Providers;

public interface IChatCompletionClient
{
    Task<string> CompleteAsync(
        string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken);
}
