namespace Flow.Runtime;

public sealed class RuntimeGuard : IMcpInvoker
{
    private readonly IMcpInvoker _inner;
    private readonly IReadOnlySet<string> _allowedTools;

    public RuntimeGuard(IMcpInvoker inner, IReadOnlySet<string> allowedTools)
    {
        _inner = inner;
        _allowedTools = allowedTools;
    }

    public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        if (!_allowedTools.Contains(toolName))
            throw new InvalidOperationException($"Tool '{toolName}' is not in the compiled capability manifest.");

        return _inner.InvokeAsync(toolName, arguments, cancellationToken);
    }
}
