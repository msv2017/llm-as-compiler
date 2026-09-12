using Flow.Compiler.Candidate;
using Flow.Compiler.Providers;
using Flow.Contracts;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Compiler.Tests;

public class PromptBuilderTests
{
    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition(
            "crm.getCustomerById",
            new ObjectType("In", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            new ObjectType("Customer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String }),
            ToolEffect.Read, ToolRetryPolicy.Safe)
    });

    private static WorkflowSource Source() => new(
        "Find the customer's name given their id.",
        new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
        new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String }));

    [Fact]
    public void BuildGeneratePrompt_IncludesPromptInputOutputAndToolSignatures()
    {
        var (system, user) = PromptBuilder.BuildGeneratePrompt(Source(), Catalog());

        Assert.Contains("deterministic workflow compiler", system);
        Assert.Contains("Find the customer's name given their id.", user);
        Assert.Contains("customerId", user);
        Assert.Contains("customerName", user);
        Assert.Contains("crm.getCustomerById", user);
        Assert.Contains("Read", user);
    }

    [Fact]
    public void BuildRepairPrompt_IncludesPriorCandidateAndDiagnostics()
    {
        var priorCandidate = new CandidateWorkflowAst(
            new CandidateWorkflowBody("FindCustomerName", Array.Empty<CandidateNode>(), new Dictionary<string, CandidateExpression>()),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        var diagnostics = new[] { new ValidationDiagnostic("F001", "Unknown tool 'crm.doesNotExist'.", "customer") };

        var (system, user) = PromptBuilder.BuildRepairPrompt(Source(), Catalog(), priorCandidate, diagnostics);

        Assert.Contains("deterministic workflow compiler", system);
        Assert.Contains("FindCustomerName", user);
        Assert.Contains("F001", user);
        Assert.Contains("Unknown tool 'crm.doesNotExist'.", user);
        Assert.Contains("customer", user);
    }
}
