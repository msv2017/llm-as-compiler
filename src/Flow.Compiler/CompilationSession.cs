using Flow.Contracts;
using Flow.Validation;

namespace Flow.Compiler;

public sealed class CompilationSession
{
    private readonly ISemanticCompilerModel _model;
    private readonly RepairLoop _repairLoop;

    public CompilationSession(ISemanticCompilerModel model, WorkflowValidator validator, int maxRepairAttempts = 3)
    {
        _model = model;
        _repairLoop = new RepairLoop(model, validator, maxRepairAttempts);
    }

    public async Task<CompilationResult> CompileAsync(
        WorkflowSource source, ToolCatalog tools, CancellationToken cancellationToken = default)
    {
        var candidate = await _model.GenerateCandidateAsync(new SemanticCompilationRequest(source, tools), cancellationToken);
        var assumptions = candidate.Assumptions.Select(a => new CompilerAssumption(a)).ToList();

        if (candidate.Unresolved.Count > 0)
        {
            var unresolved = candidate.Unresolved.Select(ParseUnresolved).ToList();
            return new CompilationResult(
                CompilationStatus.Uncompilable, null, Array.Empty<ValidationDiagnostic>(), assumptions, unresolved);
        }

        var outcome = await _repairLoop.RunAsync(source, tools, candidate, cancellationToken);

        return outcome.Success
            ? new CompilationResult(
                CompilationStatus.Success, outcome.Workflow, Array.Empty<ValidationDiagnostic>(), assumptions, Array.Empty<UnresolvedSemantic>())
            : new CompilationResult(
                CompilationStatus.Uncompilable, null, outcome.Diagnostics, assumptions, Array.Empty<UnresolvedSemantic>());
    }

    private static UnresolvedSemantic ParseUnresolved(string entry)
    {
        var separatorIndex = entry.IndexOf(':');
        return separatorIndex > 0
            ? new UnresolvedSemantic(entry[..separatorIndex].Trim(), entry[(separatorIndex + 1)..].Trim())
            : new UnresolvedSemantic("S203", entry);
    }
}
