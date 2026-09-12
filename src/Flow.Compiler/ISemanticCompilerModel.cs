using Flow.Compiler.Candidate;
using Flow.Contracts;
using Flow.Validation;

namespace Flow.Compiler;

public sealed record SemanticCompilationRequest(WorkflowSource Source, ToolCatalog Tools);

public sealed record SemanticRepairRequest(
    WorkflowSource Source,
    ToolCatalog Tools,
    CandidateWorkflowAst PriorCandidate,
    IReadOnlyList<ValidationDiagnostic> Diagnostics);

public interface ISemanticCompilerModel
{
    Task<CandidateWorkflowAst> GenerateCandidateAsync(SemanticCompilationRequest request, CancellationToken cancellationToken);

    Task<CandidateWorkflowAst> RepairCandidateAsync(SemanticRepairRequest request, CancellationToken cancellationToken);
}
