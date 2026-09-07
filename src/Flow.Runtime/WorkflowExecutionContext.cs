namespace Flow.Runtime;

public sealed class WorkflowExecutionContext
{
    private readonly Dictionary<string, object?> _values = new();

    public WorkflowExecutionContext(object? input) => Input = input;

    public object? Input { get; }

    public void Bind(string nodeId, object? value) => _values[nodeId] = value;

    public object? Resolve(string path)
    {
        var segments = path.Split('.');
        object? current = segments[0] == "input" ? Input : _values[segments[0]];

        for (var i = 1; i < segments.Length; i++)
        {
            current = current switch
            {
                FlowRecord record => record.Get(segments[i]),
                null => throw new InvalidOperationException(
                    $"Cannot access '{segments[i]}' on null value while resolving '{path}'."),
                _ => throw new InvalidOperationException(
                    $"Cannot navigate into '{current.GetType().Name}' while resolving '{path}'.")
            };
        }

        return current;
    }
}
