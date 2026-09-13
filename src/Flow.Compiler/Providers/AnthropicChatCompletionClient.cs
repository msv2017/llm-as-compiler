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
        // Anthropic's structured-output schema validator (output_config.format) rejects the
        // recursive $ref shape CandidateSchema.Json requires (expression -> binaryExpression ->
        // expression, node -> ifNode -> node), unlike OpenAI's strict json_schema mode. Fall back to
        // describing the schema in the prompt and parsing/repairing like any other malformed
        // response -- RepairLoop already treats unparseable candidates as a C001 diagnostic rather
        // than a crash.
        var system = systemPrompt +
            "\n\nRespond with a single JSON object that matches this JSON Schema exactly. " +
            "Output ONLY the raw JSON object -- no markdown code fences, no explanation before or after.\n\n" +
            "Schema:\n" + jsonSchema;

        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 16000,
            System = system,
            Messages = [new() { Role = Role.User, Content = userPrompt }]
        };

        var response = await _client.Messages.Create(parameters, cancellationToken: cancellationToken);

        var text = response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(block => block.Text)
            .FirstOrDefault()
            ?? throw new NotSupportedException("Anthropic response did not contain a text block.");

        return StripMarkdownFences(text);
    }

    private static string StripMarkdownFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return text;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
            return text;

        var withoutOpeningFence = trimmed[(firstNewline + 1)..];
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return (closingFenceIndex >= 0 ? withoutOpeningFence[..closingFenceIndex] : withoutOpeningFence).Trim();
    }
}
