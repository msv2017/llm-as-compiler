using Flow.Compiler.Candidate;
using Flow.Compiler.Providers;
using Flow.Compiler.Tests.Fakes;
using Flow.Contracts;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Compiler.Tests;

public class OpenAiSemanticCompilerModelTests
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

    private const string ArrayOfPairsCandidateJson = """
    {
      "workflow": {
        "name": "FindCustomerName",
        "nodes": [
          {
            "kind": "call", "id": "customer", "tool": "crm.getCustomerById",
            "arguments": [ { "name": "id", "value": { "kind": "path", "path": "input.customerId" } } ]
          }
        ],
        "return": [ { "name": "customerName", "value": { "kind": "path", "path": "customer.name" } } ]
      },
      "interpretations": [], "assumptions": ["assumed id maps directly"], "unresolved": []
    }
    """;

    [Fact]
    public async Task GenerateCandidateAsync_SendsSchemaAndPrompt_AndParsesArrayOfPairsResponse()
    {
        var client = new FakeChatCompletionClient(ArrayOfPairsCandidateJson);
        var model = new OpenAiSemanticCompilerModel(client);

        var candidate = await model.GenerateCandidateAsync(
            new SemanticCompilationRequest(Source(), Catalog()), CancellationToken.None);

        Assert.Equal("FindCustomerName", candidate.Workflow.Name);
        var call = Assert.IsType<CandidateCallNode>(Assert.Single(candidate.Workflow.Nodes));
        var idArg = Assert.IsType<CandidatePathExpression>(call.Arguments["id"]);
        Assert.Equal("input.customerId", idArg.Path);
        Assert.Single(candidate.Assumptions);

        var sentCall = Assert.Single(client.CallsReceived);
        Assert.Equal(CandidateSchema.Json, sentCall.JsonSchema);
        Assert.Contains("Find the customer's name given their id.", sentCall.UserPrompt);
    }

    [Fact]
    public async Task RepairCandidateAsync_SendsPriorCandidateAndDiagnostics_AndParsesResponse()
    {
        var client = new FakeChatCompletionClient(ArrayOfPairsCandidateJson);
        var model = new OpenAiSemanticCompilerModel(client);
        var priorCandidate = new CandidateWorkflowAst(
            new CandidateWorkflowBody("Broken", Array.Empty<CandidateNode>(), new Dictionary<string, CandidateExpression>()),
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        var diagnostics = new[] { new ValidationDiagnostic("F001", "Unknown tool.", "customer") };

        var candidate = await model.RepairCandidateAsync(
            new SemanticRepairRequest(Source(), Catalog(), priorCandidate, diagnostics), CancellationToken.None);

        Assert.Equal("FindCustomerName", candidate.Workflow.Name);
        var sentCall = Assert.Single(client.CallsReceived);
        Assert.Contains("F001", sentCall.UserPrompt);
        Assert.Contains("Broken", sentCall.UserPrompt);
    }
}
