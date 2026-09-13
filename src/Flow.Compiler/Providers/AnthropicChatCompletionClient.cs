using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace Flow.Compiler.Providers;

public sealed class AnthropicChatCompletionClient : IChatCompletionClient
{
    private readonly AnthropicClient _client;
    private readonly string _model;

    public AnthropicChatCompletionClient(string apiKey, string model)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _model = model;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken)
    {
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonSchema)
            ?? throw new JsonException("JSON schema deserialized to null.");

        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 16000,
            System = systemPrompt,
            Messages = [new() { Role = Role.User, Content = userPrompt }],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = schema }
            }
        };

        var response = await _client.Messages.Create(parameters, cancellationToken: cancellationToken);

        return response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(block => block.Text)
            .FirstOrDefault()
            ?? throw new NotSupportedException("Anthropic response did not contain a text block.");
    }
}
