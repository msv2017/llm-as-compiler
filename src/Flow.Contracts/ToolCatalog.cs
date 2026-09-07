namespace Flow.Contracts;

public sealed class ToolCatalog
{
    private readonly Dictionary<string, ToolDefinition> _tools;

    public ToolCatalog(IEnumerable<ToolDefinition> tools)
    {
        _tools = tools.ToDictionary(t => t.Name);
    }

    public IReadOnlyCollection<ToolDefinition> Tools => _tools.Values;

    public bool TryGet(string name, out ToolDefinition? tool) => _tools.TryGetValue(name, out tool);
}
