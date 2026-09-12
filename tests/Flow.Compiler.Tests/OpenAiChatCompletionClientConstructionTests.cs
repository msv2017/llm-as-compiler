using Flow.Compiler.Providers;
using Xunit;

namespace Flow.Compiler.Tests;

public class OpenAiChatCompletionClientConstructionTests
{
    [Fact]
    public void Constructor_DoesNotThrow_ForAnyNonEmptyApiKeyAndModel()
    {
        var client = new OpenAiChatCompletionClient("test-key-not-a-real-secret", "gpt-4.1-mini");

        Assert.NotNull(client);
    }

    [Fact]
    public void FromEnvironment_ThrowsClearException_WhenApiKeyMissing()
    {
        var original = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

            var ex = Assert.Throws<InvalidOperationException>(() => OpenAiSemanticCompilerModel.FromEnvironment());
            Assert.Contains("OPENAI_API_KEY", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", original);
        }
    }

    [Fact]
    public void FromEnvironment_Succeeds_WhenApiKeyPresent()
    {
        var original = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key-not-a-real-secret");

            var model = OpenAiSemanticCompilerModel.FromEnvironment();

            Assert.NotNull(model);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", original);
        }
    }
}
