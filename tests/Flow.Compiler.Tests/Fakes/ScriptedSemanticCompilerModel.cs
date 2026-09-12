using Flow.Compiler.Candidate;

namespace Flow.Compiler.Tests.Fakes;

public sealed class ScriptedSemanticCompilerModel : ISemanticCompilerModel
{
    private readonly Queue<CandidateWorkflowAst> _responses;

    public ScriptedSemanticCompilerModel(params CandidateWorkflowAst[] responses)
        => _responses = new Queue<CandidateWorkflowAst>(responses);

    public List<SemanticRepairRequest> RepairRequestsReceived { get; } = new();

    public Task<CandidateWorkflowAst> GenerateCandidateAsync(SemanticCompilationRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Dequeue());

    public Task<CandidateWorkflowAst> RepairCandidateAsync(SemanticRepairRequest request, CancellationToken cancellationToken)
    {
        RepairRequestsReceived.Add(request);
        return Task.FromResult(Dequeue());
    }

    private CandidateWorkflowAst Dequeue() =>
        _responses.Count > 0 ? _responses.Dequeue() : throw new InvalidOperationException("No more scripted responses.");
}
