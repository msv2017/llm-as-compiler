using Flow.Compiler.Candidate;
using Flow.Contracts;
using Flow.IR;
using Flow.Validation;

namespace Flow.Compiler;

public sealed record RepairOutcome(
    bool Success,
    WorkflowDefinition? Workflow,
    CandidateWorkflowAst? FinalCandidate,
    IReadOnlyList<ValidationDiagnostic> Diagnostics,
    int AttemptsUsed)
{
    public static RepairOutcome Succeeded(WorkflowDefinition workflow, CandidateWorkflowAst candidate, int attemptsUsed) =>
        new(true, workflow, candidate, Array.Empty<ValidationDiagnostic>(), attemptsUsed);

    public static RepairOutcome Failed(IReadOnlyList<ValidationDiagnostic> diagnostics, int attemptsUsed) =>
        new(false, null, null, diagnostics, attemptsUsed);
}

public sealed class RepairLoop
{
    private readonly ISemanticCompilerModel _model;
    private readonly WorkflowValidator _validator;
    private readonly int _maxAttempts;

    public RepairLoop(ISemanticCompilerModel model, WorkflowValidator validator, int maxAttempts = 3)
    {
        _model = model;
        _validator = validator;
        _maxAttempts = maxAttempts;
    }

    public async Task<RepairOutcome> RunAsync(
        WorkflowSource source, ToolCatalog tools, CandidateWorkflowAst initialCandidate, CancellationToken cancellationToken)
    {
        var candidate = initialCandidate;
        var attempt = 0;

        while (true)
        {
            WorkflowDefinition? workflow;
            IReadOnlyList<ValidationDiagnostic> diagnostics;
            try
            {
                workflow = CandidateToIrConverter.Convert(candidate, source.InputType, source.OutputType);
                diagnostics = _validator.Validate(workflow, tools);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                workflow = null;
                diagnostics = new[] { new ValidationDiagnostic("C001", $"Candidate could not be converted to IR: {ex.Message}") };
            }

            if (workflow is not null && diagnostics.Count == 0)
                return RepairOutcome.Succeeded(workflow, candidate, attempt);

            if (attempt >= _maxAttempts)
                return RepairOutcome.Failed(diagnostics, attempt);

            attempt++;
            candidate = await _model.RepairCandidateAsync(
                new SemanticRepairRequest(source, tools, candidate, diagnostics), cancellationToken);
        }
    }
}
