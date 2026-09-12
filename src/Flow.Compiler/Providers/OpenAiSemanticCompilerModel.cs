using System.Text.Json;
using Flow.Compiler.Candidate;

namespace Flow.Compiler.Providers;

public sealed class OpenAiSemanticCompilerModel : ISemanticCompilerModel
{
    private readonly IChatCompletionClient _client;

    public OpenAiSemanticCompilerModel(IChatCompletionClient client)
    {
        _client = client;
    }

    public async Task<CandidateWorkflowAst> GenerateCandidateAsync(
        SemanticCompilationRequest request, CancellationToken cancellationToken)
    {
        var (system, user) = PromptBuilder.BuildGeneratePrompt(request.Source, request.Tools);
        return await CompleteAndParseAsync(system, user, cancellationToken);
    }

    public async Task<CandidateWorkflowAst> RepairCandidateAsync(
        SemanticRepairRequest request, CancellationToken cancellationToken)
    {
        var (system, user) = PromptBuilder.BuildRepairPrompt(
            request.Source, request.Tools, request.PriorCandidate, request.Diagnostics);
        return await CompleteAndParseAsync(system, user, cancellationToken);
    }

    private async Task<CandidateWorkflowAst> CompleteAndParseAsync(
        string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var rawJson = await _client.CompleteAsync(systemPrompt, userPrompt, CandidateSchema.Json, cancellationToken);
        var rewrittenJson = ResponseJsonRewriter.RewriteDictionaryShapedFields(rawJson);
        return JsonSerializer.Deserialize<CandidateWorkflowAst>(rewrittenJson, CandidateJson.Options)
            ?? throw new JsonException("Model response deserialized to null.");
    }
}
