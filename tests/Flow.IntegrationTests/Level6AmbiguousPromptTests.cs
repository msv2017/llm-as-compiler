using Flow.Compiler;
using Flow.Compiler.Providers;
using Flow.Contracts;
using Flow.TypeSystem;
using Xunit;

namespace Flow.IntegrationTests;

public class Level6AmbiguousPromptTests
{
    [Fact]
    public async Task MostAppropriateAccount_FailsToCompile()
    {
        if (OpenAiTestEnvironment.ApiKey is null) return; // no OPENAI_API_KEY configured

        var accountType = new ObjectType("Account", new Dictionary<string, FlowType>
        {
            ["id"] = PrimitiveType.String,
            ["name"] = PrimitiveType.String,
            ["status"] = PrimitiveType.String,
            ["createdAt"] = PrimitiveType.DateTime
        });
        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("accounts.listByCustomer",
                new ObjectType("In", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
                new ListType(accountType), ToolEffect.Read, ToolRetryPolicy.Safe)
        });
        var source = new WorkflowSource(
            "Given a customer id, find the most appropriate account for this customer and return its id.",
            new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
            new ObjectType("Result", new Dictionary<string, FlowType> { ["accountId"] = PrimitiveType.String }));

        var compiler = new PromptCompiler(OpenAiSemanticCompilerModel.FromEnvironment());
        var result = await compiler.CompileAsync(source, tools);

        Assert.Equal(CompilationStatus.Uncompilable, result.Status);
    }

    [Fact]
    public async Task EscalateIfTicketSoundsAngry_FailsToCompile()
    {
        if (OpenAiTestEnvironment.ApiKey is null) return; // no OPENAI_API_KEY configured

        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("ticket.get",
                new ObjectType("In", new Dictionary<string, FlowType> { ["ticketId"] = PrimitiveType.String }),
                new ObjectType("Ticket", new Dictionary<string, FlowType> { ["subject"] = PrimitiveType.String, ["body"] = PrimitiveType.String }),
                ToolEffect.Read, ToolRetryPolicy.Safe),
            new ToolDefinition("ticket.escalate",
                new ObjectType("In", new Dictionary<string, FlowType> { ["ticketId"] = PrimitiveType.String }),
                new ObjectType("EscalationResult", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
                ToolEffect.Write, ToolRetryPolicy.Never)
        });
        var source = new WorkflowSource(
            "Read the ticket and determine whether the customer sounds angry. If so, escalate it.",
            new ObjectType("Request", new Dictionary<string, FlowType> { ["ticketId"] = PrimitiveType.String }),
            new ObjectType("Result", new Dictionary<string, FlowType> { ["escalated"] = PrimitiveType.Bool }));

        var compiler = new PromptCompiler(OpenAiSemanticCompilerModel.FromEnvironment());
        var result = await compiler.CompileAsync(source, tools);

        Assert.Equal(CompilationStatus.Uncompilable, result.Status);
    }
}
