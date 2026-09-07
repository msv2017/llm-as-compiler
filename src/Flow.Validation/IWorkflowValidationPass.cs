using Flow.Contracts;
using Flow.IR;

namespace Flow.Validation;

public interface IWorkflowValidationPass
{
    string Name { get; }
    IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools);
}
