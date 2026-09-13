using Flow.Compiler.Candidate;

namespace Flow.Compiler.Providers;

public sealed class AnthropicSemanticCompilerModel : ISemanticCompilerModel
{
    private readonly GenericSemanticCompilerModel _inner;

    public AnthropicSemanticCompilerModel(IChatCompletionClient client)
    {
        _inner = new GenericSemanticCompilerModel(client);
    }

    public static AnthropicSemanticCompilerModel FromEnvironment(string model = "claude-haiku-4-5")
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set.");
        return new AnthropicSemanticCompilerModel(new AnthropicChatCompletionClient(apiKey, model));
    }

    public Task<CandidateWorkflowAst> GenerateCandidateAsync(
        SemanticCompilationRequest request, CancellationToken cancellationToken)
        => _inner.GenerateCandidateAsync(request, cancellationToken);

    public Task<CandidateWorkflowAst> RepairCandidateAsync(
        SemanticRepairRequest request, CancellationToken cancellationToken)
        => _inner.RepairCandidateAsync(request, cancellationToken);
}
