namespace Flow.IntegrationTests;

internal static class OpenAiTestEnvironment
{
    public static string? ApiKey => Environment.GetEnvironmentVariable("OPENAI_API_KEY");
}
