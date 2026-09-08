using Xunit;

namespace Flow.Runtime.Tests;

public class RuntimeGuardTests
{
    private sealed class FakeInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>("ok");
    }

    [Fact]
    public async Task AllowedTool_PassesThrough()
    {
        var guard = new RuntimeGuard(new FakeInvoker(), new HashSet<string> { "crm.findCustomer" });

        var result = await guard.InvokeAsync("crm.findCustomer", new Dictionary<string, object?>());

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task DisallowedTool_Throws()
    {
        var guard = new RuntimeGuard(new FakeInvoker(), new HashSet<string> { "crm.findCustomer" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => guard.InvokeAsync("billing.createRefund", new Dictionary<string, object?>()));
    }
}
