namespace Flow.IntegrationTests;

internal static class AnthropicTestEnvironment
{
    public static string? ApiKey => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
}
