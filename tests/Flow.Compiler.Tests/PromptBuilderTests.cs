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
    public void BuildGeneratePrompt_SystemPromptWarnsAgainstListIndexPathSyntax()
    {
        var (system, _) = PromptBuilder.BuildGeneratePrompt(Source(), Catalog());

        Assert.Contains("input.fieldName", system);
        Assert.Contains("someList.0.field", system);
        Assert.Contains("\"First\"", system);
    }

    [Fact]
    public void BuildGeneratePrompt_SystemPromptWarnsAboutAggregateFirstNullability()
    {
        // Diagnosed live against the real Level 4 ("refund the oldest invoice") failure: the model
        // consistently produced an unguarded "oldestInvoice.id" access after AggregateNode(First),
        // tripping NullabilityValidator's T103 -- and only an IfNode's own bare "!= null"/"== null"
        // condition narrows nullability (AssertNode does not), so the guidance must say exactly that.
        var (system, _) = PromptBuilder.BuildGeneratePrompt(Source(), Catalog());

        Assert.Contains("!= null", system);
        Assert.Contains("assert", system.ToLowerInvariant());
    }

    [Fact]
    public void BuildGeneratePrompt_SystemPromptWarnsAgainstDotValueNodeReference()
    {
        // Diagnosed live against the real Level 4 failure: the model referenced an if-node's result
        // as "refundDecision.value" (T104, unresolvable) instead of the bare node id "refundDecision".
        var (system, _) = PromptBuilder.BuildGeneratePrompt(Source(), Catalog());

        Assert.Contains(".value", system);
    }

    [Fact]
    public void BuildGeneratePrompt_SystemPromptClarifiesLiteralComparisonIsNotAmbiguity()
    {
        // Diagnosed live: Claude flagged comparing a status field against a literal value stated in
        // the prompt (e.g. "keep only UNPAID invoices") as "unresolved" (undocumented enum values),
        // where GPT treated the identical instruction as a normal PROMPT-sourced constant comparison
        // and recorded it as an assumption instead. The rule already requires a "source" on every
        // constant -- this just makes explicit that satisfying it is not itself grounds for
        // "unresolved" when the prompt states the exact value to compare against.
        var (system, _) = PromptBuilder.BuildGeneratePrompt(Source(), Catalog());

        Assert.Contains("unresolved", system.ToLowerInvariant());
        Assert.Contains("literal value", system);
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

    [Fact]
    public void BuildGeneratePrompt_ExpandsToolOutputObjectTypeFields()
    {
        var invoiceType = new Flow.TypeSystem.ObjectType("Invoice", new Dictionary<string, Flow.TypeSystem.FlowType>
        {
            ["amount"] = Flow.TypeSystem.PrimitiveType.Decimal,
            ["status"] = Flow.TypeSystem.PrimitiveType.String
        });
        var tools = new ToolCatalog(new[]
        {
            new ToolDefinition("billing.listInvoices",
                new Flow.TypeSystem.ObjectType("In", new Dictionary<string, Flow.TypeSystem.FlowType> { ["customerId"] = Flow.TypeSystem.PrimitiveType.String }),
                new Flow.TypeSystem.ListType(invoiceType), Flow.Contracts.ToolEffect.Read, Flow.Contracts.ToolRetryPolicy.Safe)
        });
        var source = new WorkflowSource(
            "irrelevant for this test",
            new Flow.TypeSystem.ObjectType("Request", new Dictionary<string, Flow.TypeSystem.FlowType>()),
            new Flow.TypeSystem.ObjectType("Result", new Dictionary<string, Flow.TypeSystem.FlowType>()));

        var (_, user) = PromptBuilder.BuildGeneratePrompt(source, tools);

        Assert.Contains("amount", user);
        Assert.Contains("status", user);
    }
}
