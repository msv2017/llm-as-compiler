namespace Flow.Cli;

public static class CliDispatcher
{
    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine("Usage: flow-cli <compile|scaffold> ...");
            return 2;
        }

        var subcommand = args[0];
        var rest = args[1..];

        return subcommand switch
        {
            "compile" => await CompileCommand.RunAsync(rest, stdout, stderr),
            "scaffold" => await ScaffoldCommand.RunAsync(rest, stdout, stderr),
            _ => Unknown(subcommand, stderr)
        };
    }

    private static int Unknown(string subcommand, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown command '{subcommand}'. Expected 'compile' or 'scaffold'.");
        return 2;
    }
}
