using System.Text.Json;
using Flow.Compiler.Candidate;
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
        CandidateWorkflowAst candidate;
        try
        {
            candidate = await _model.GenerateCandidateAsync(new SemanticCompilationRequest(source, tools), cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return new CompilationResult(
                CompilationStatus.Uncompilable, null,
                new[] { new ValidationDiagnostic("C001", $"Model response could not be parsed: {ex.Message}") },
                Array.Empty<CompilerAssumption>(), Array.Empty<UnresolvedSemantic>());
        }

        var assumptions = candidate.Assumptions.Select(a => new CompilerAssumption(a)).ToList();

        if (candidate.Unresolved.Count > 0)
        {
            var unresolved = candidate.Unresolved.Select(ParseUnresolved).ToList();
            return new CompilationResult(
                CompilationStatus.Uncompilable, null, Array.Empty<ValidationDiagnostic>(), assumptions, unresolved);
        }

        var outcome = await _repairLoop.RunAsync(source, tools, candidate, cancellationToken);

        if (outcome.Success)
        {
            var finalAssumptions = outcome.FinalCandidate!.Assumptions.Select(a => new CompilerAssumption(a)).ToList();
            return new CompilationResult(
                CompilationStatus.Success, outcome.Workflow, Array.Empty<ValidationDiagnostic>(), finalAssumptions, Array.Empty<UnresolvedSemantic>());
        }

        return new CompilationResult(
            CompilationStatus.Uncompilable, null, outcome.Diagnostics, assumptions, Array.Empty<UnresolvedSemantic>());
    }

    private static UnresolvedSemantic ParseUnresolved(string entry)
    {
        var separatorIndex = entry.IndexOf(':');
        if (separatorIndex > 0)
        {
            var prefix = entry[..separatorIndex].Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(prefix, @"^S\d{3}$"))
                return new UnresolvedSemantic(prefix, entry[(separatorIndex + 1)..].Trim());
        }
        return new UnresolvedSemantic("S203", entry);
    }
}
