namespace Flow.Cli.Runtime;

public static class McpAuthHeaders
{
    public const string EnvVarName = "MCP_AUTH_TOKEN";

    public static bool TryResolve(string? headerNameFlag, out IReadOnlyDictionary<string, string>? headers, out string? error)
    {
        var token = Environment.GetEnvironmentVariable(EnvVarName);

        if (token is null)
        {
            if (headerNameFlag is not null)
            {
                headers = null;
                error = $"--mcp-auth-header was given but {EnvVarName} is not set.";
                return false;
            }

            headers = null;
            error = null;
            return true;
        }

        headers = new Dictionary<string, string> { [headerNameFlag ?? "Authorization"] = token };
        error = null;
        return true;
    }
}
