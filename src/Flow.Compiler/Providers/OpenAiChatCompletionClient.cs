using System.Text;
using OpenAI.Chat;

namespace Flow.Compiler.Providers;

public sealed class OpenAiChatCompletionClient : IChatCompletionClient
{
    private readonly ChatClient _client;

    public OpenAiChatCompletionClient(string apiKey, string model)
    {
        _client = new ChatClient(model, apiKey);
    }

    public async Task<string> CompleteAsync(
        string systemPrompt, string userPrompt, string jsonSchema, CancellationToken cancellationToken)
    {
        List<ChatMessage> messages =
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        ];

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "candidate_workflow",
                jsonSchema: BinaryData.FromBytes(Encoding.UTF8.GetBytes(jsonSchema)),
                jsonSchemaIsStrict: true)
        };

        var result = await _client.CompleteChatAsync(messages, options, cancellationToken);
        return result.Value.Content[0].Text;
    }
}
