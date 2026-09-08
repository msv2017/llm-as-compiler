using Flow.Contracts;
using Flow.IR;

namespace Flow.Validation;

public sealed class WorkflowValidator
{
    private readonly IReadOnlyList<IWorkflowValidationPass> _passes;

    public WorkflowValidator(IReadOnlyList<IWorkflowValidationPass> passes)
    {
        _passes = passes;
    }

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
        => _passes.SelectMany(pass => pass.Validate(workflow, tools)).ToList();

    public static WorkflowValidator CreateDefault() => new(new IWorkflowValidationPass[]
    {
        new ToolResolutionValidator(),
        new DataflowValidator(),
        new TypeValidator(),
        new NullabilityValidator(),
        new OutputValidator(),
        new BoundednessValidator()
    });
}
