using Flow.Compiler.Providers;
using Xunit;

namespace Flow.Compiler.Tests;

public class AnthropicChatCompletionClientConstructionTests
{
    [Fact]
    public void Constructor_DoesNotThrow_ForAnyNonEmptyApiKeyAndModel()
    {
        var client = new AnthropicChatCompletionClient("test-key-not-a-real-secret", "claude-haiku-4-5");

        Assert.NotNull(client);
    }

    [Fact]
    public void FromEnvironment_ThrowsClearException_WhenApiKeyMissing()
    {
        var original = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);

            var ex = Assert.Throws<InvalidOperationException>(() => AnthropicSemanticCompilerModel.FromEnvironment());
            Assert.Contains("ANTHROPIC_API_KEY", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", original);
        }
    }

    [Fact]
    public void FromEnvironment_Succeeds_WhenApiKeyPresent()
    {
        var original = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-key-not-a-real-secret");

            var model = AnthropicSemanticCompilerModel.FromEnvironment();

            Assert.NotNull(model);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", original);
        }
    }
}
