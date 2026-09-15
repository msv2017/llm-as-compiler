using System.Text.Json;
using Flow.Compiler.Candidate;
using Flow.IR;
using Flow.TypeSystem;

namespace Flow.Cli;

internal sealed class CompiledWorkflowFileJson
{
    public FlowTypeJson? InputType { get; set; }
    public FlowTypeJson? OutputType { get; set; }
    public CandidateWorkflowBody? Workflow { get; set; }
}

public static class CompiledWorkflowJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new(Options) { WriteIndented = true };

    public static string Write(WorkflowDefinition workflow)
    {
        var file = new CompiledWorkflowFileJson
        {
            InputType = ScenarioJson.FromFlowType(workflow.InputType),
            OutputType = ScenarioJson.FromFlowType(workflow.OutputType),
            Workflow = IrToCandidateConverter.Convert(workflow)
        };
        return JsonSerializer.Serialize(file, WriteOptions);
    }

    public static WorkflowDefinition Parse(string json)
    {
        CompiledWorkflowFileJson? file;
        try
        {
            file = JsonSerializer.Deserialize<CompiledWorkflowFileJson>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ScenarioParseException($"Compiled workflow file is not valid JSON: {ex.Message}");
        }

        if (file is null)
            throw new ScenarioParseException("Compiled workflow file is empty.");
        if (file.InputType is null)
            throw new ScenarioParseException("Compiled workflow file is missing 'inputType'.");
        if (file.OutputType is null)
            throw new ScenarioParseException("Compiled workflow file is missing 'outputType'.");
        if (file.Workflow is null)
            throw new ScenarioParseException("Compiled workflow file is missing 'workflow'.");

        var inputType = ScenarioJson.ToFlowType(file.InputType, "inputType");
        var outputType = ScenarioJson.ToFlowType(file.OutputType, "outputType");
        return CandidateToIrConverter.Convert(file.Workflow, inputType, outputType);
    }
}
