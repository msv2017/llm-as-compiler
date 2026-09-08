using Flow.Contracts;
using Flow.IR;

namespace Flow.Analysis;

public sealed record EffectSummary(
    IReadOnlyList<string> Reads,
    IReadOnlyList<string> Writes,
    IReadOnlyList<string> Deletes,
    IReadOnlyList<string> External);

public static class EffectAnalyzer
{
    public static EffectSummary Analyze(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var capabilities = CapabilityAnalyzer.Analyze(workflow);
        var reads = new List<string>();
        var writes = new List<string>();
        var deletes = new List<string>();
        var external = new List<string>();

        foreach (var toolName in capabilities)
        {
            if (!tools.TryGet(toolName, out var tool) || tool is null)
                continue;

            var bucket = tool.Effect switch
            {
                ToolEffect.Read => reads,
                ToolEffect.Write => writes,
                ToolEffect.Delete => deletes,
                ToolEffect.External => external,
                _ => null
            };
            bucket?.Add(toolName);
        }

        return new EffectSummary(reads, writes, deletes, external);
    }
}
