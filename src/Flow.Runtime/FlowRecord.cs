namespace Flow.Runtime;

public sealed class FlowRecord
{
    private readonly IReadOnlyDictionary<string, object?> _fields;

    public FlowRecord(IReadOnlyDictionary<string, object?> fields) => _fields = fields;

    public object? Get(string fieldName) =>
        _fields.TryGetValue(fieldName, out var value)
            ? value
            : throw new InvalidOperationException($"Field '{fieldName}' not found.");
}
