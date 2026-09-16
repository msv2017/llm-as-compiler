namespace Flow.Cli.Runtime;

public static class McpAuthHeaders
{
    public const string EnvVarName = "MCP_AUTH_TOKEN";

    private static readonly System.Text.RegularExpressions.Regex ValidHeaderName =
        new(@"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static bool TryResolve(string? headerNameFlag, out IReadOnlyDictionary<string, string>? headers, out string? error)
    {
        var token = Environment.GetEnvironmentVariable(EnvVarName);

        if (string.IsNullOrEmpty(token))
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

        var headerName = headerNameFlag ?? "Authorization";
        if (!ValidHeaderName.IsMatch(headerName))
        {
            headers = null;
            error = "--mcp-auth-header is not a valid HTTP header name (expected a token such as 'Authorization' or 'X-Api-Key').";
            return false;
        }

        if (token.Any(char.IsControl))
        {
            headers = null;
            error = $"{EnvVarName} contains a control character (a stray newline?) and can't be sent as a header value.";
            return false;
        }

        headers = new Dictionary<string, string> { [headerName] = token };
        error = null;
        return true;
    }
}
