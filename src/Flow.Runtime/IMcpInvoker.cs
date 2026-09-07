namespace Flow.Runtime;

public interface IMcpInvoker
{
    Task<object?> InvokeAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);
}
