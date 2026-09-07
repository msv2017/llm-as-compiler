using Flow.Contracts;
using Flow.IR;
using Flow.TypeSystem;

namespace Flow.Validation;

public sealed class OutputValidator : IWorkflowValidationPass
{
    public string Name => "Output";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        if (workflow.OutputType is not ObjectType outputType)
            return diagnostics;

        foreach (var requiredField in outputType.Fields.Keys)
        {
            if (!workflow.Return.Fields.ContainsKey(requiredField))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    "O601", $"Missing required output field '{requiredField}'.", workflow.Return.Id));
            }
        }

        foreach (var providedField in workflow.Return.Fields.Keys)
        {
            if (!outputType.Fields.ContainsKey(providedField))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    "O602", $"'{providedField}' is not a field of the declared output type.", workflow.Return.Id));
            }
        }

        return diagnostics;
    }
}
