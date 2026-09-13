namespace Flow.Cli;

public static class ScaffoldCommand
{
    private const string Usage = "Usage: flow-cli scaffold <output.json> --mcp-url <url> --prompt \"<text>\"";

    public static Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        stderr.WriteLine(Usage);
        return Task.FromResult(2);
    }
}
