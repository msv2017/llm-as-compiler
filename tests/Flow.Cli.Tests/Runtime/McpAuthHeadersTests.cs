using Flow.Cli.Runtime;
using Xunit;

namespace Flow.Cli.Tests.Runtime;

[Collection("EnvironmentVariableTests")]
public class McpAuthHeadersTests
{
    [Fact]
    public void TryResolve_NoEnvVarNoFlag_ReturnsTrueWithNullHeaders()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, null);

            var ok = McpAuthHeaders.TryResolve(null, out var headers, out var error);

            Assert.True(ok);
            Assert.Null(headers);
            Assert.Null(error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }

    [Fact]
    public void TryResolve_NoEnvVarWithFlag_ReturnsFalseWithError()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, null);

            var ok = McpAuthHeaders.TryResolve("X-Api-Key", out var headers, out var error);

            Assert.False(ok);
            Assert.Null(headers);
            Assert.Contains("MCP_AUTH_TOKEN", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }

    [Fact]
    public void TryResolve_EnvVarSetNoFlag_DefaultsToAuthorizationHeader()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, "Bearer test-token-not-a-real-secret");

            var ok = McpAuthHeaders.TryResolve(null, out var headers, out var error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.NotNull(headers);
            Assert.Equal("Bearer test-token-not-a-real-secret", headers!["Authorization"]);
            Assert.Single(headers);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }

    [Fact]
    public void TryResolve_EnvVarSetWithFlag_UsesGivenHeaderName()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, "test-key-not-a-real-secret");

            var ok = McpAuthHeaders.TryResolve("X-Api-Key", out var headers, out var error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.NotNull(headers);
            Assert.Equal("test-key-not-a-real-secret", headers!["X-Api-Key"]);
            Assert.Single(headers);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }

    [Fact]
    public void TryResolve_EmptyEnvVarWithFlag_ReturnsFalseWithError()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, "");

            var ok = McpAuthHeaders.TryResolve("X-Api-Key", out var headers, out var error);

            Assert.False(ok);
            Assert.Null(headers);
            Assert.Contains("MCP_AUTH_TOKEN", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }

    [Fact]
    public void TryResolve_InvalidHeaderName_ReturnsFalseWithoutLeakingToken()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, "Bearer super-secret-token-value");

            var ok = McpAuthHeaders.TryResolve("X-Api Key", out var headers, out var error);

            Assert.False(ok);
            Assert.Null(headers);
            Assert.DoesNotContain("super-secret-token-value", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }

    [Fact]
    public void TryResolve_TokenWithControlCharacter_ReturnsFalseWithoutLeakingToken()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, "secret-token\nX-Injected: yes");

            var ok = McpAuthHeaders.TryResolve(null, out var headers, out var error);

            Assert.False(ok);
            Assert.Null(headers);
            Assert.DoesNotContain("secret-token", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }

    [Fact]
    public void TryResolve_HeaderNameWithTrailingNewline_ReturnsFalse()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, "Bearer super-secret-token-value");

            var ok = McpAuthHeaders.TryResolve("X-Api-Key\n", out var headers, out var error);

            Assert.False(ok);
            Assert.Null(headers);
            Assert.DoesNotContain("super-secret-token-value", error);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }

    [Fact]
    public void RedactToken_ReplacesTokenValueWithPlaceholder()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, "super-secret-token-value");

            var redacted = McpAuthHeaders.RedactToken("Failed to add header 'X' with value 'super-secret-token-value' from AdditionalHeaders.");

            Assert.DoesNotContain("super-secret-token-value", redacted);
            Assert.Contains("<redacted>", redacted);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }

    [Fact]
    public void RedactToken_NoTokenSet_ReturnsMessageUnchanged()
    {
        var original = Environment.GetEnvironmentVariable(McpAuthHeaders.EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, null);

            var redacted = McpAuthHeaders.RedactToken("some message");

            Assert.Equal("some message", redacted);
        }
        finally
        {
            Environment.SetEnvironmentVariable(McpAuthHeaders.EnvVarName, original);
        }
    }
}
