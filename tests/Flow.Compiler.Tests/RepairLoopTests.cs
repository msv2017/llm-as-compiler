using System.Text.Json;
using Flow.Compiler.Candidate;
using Flow.Compiler.Tests.Fakes;
using Flow.Contracts;
using Flow.IR.Expressions;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Compiler.Tests;

public class RepairLoopTests
{
    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition("crm.getCustomerById",
            new ObjectType("In", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            new ObjectType("Customer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String }),
            ToolEffect.Read, ToolRetryPolicy.Safe)
    });

    private static WorkflowSource Source() => new(
        "Find the customer's name.",
        new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }),
        new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String }));

    private static CandidateWorkflowAst ValidCandidate() => new(
        new CandidateWorkflowBody(
            "FindCustomerName",
            new CandidateNode[]
            {
                new CandidateCallNode("customer", "crm.getCustomerById",
                    new Dictionary<string, CandidateExpression> { ["id"] = new CandidatePathExpression("input.customerId") })
            },
            new Dictionary<string, CandidateExpression> { ["customerName"] = new CandidatePathExpression("customer.name") }),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    private static CandidateWorkflowAst BrokenCandidate() => new(
        new CandidateWorkflowBody(
            "FindCustomerName",
            new CandidateNode[]
            {
                new CandidateCallNode("customer", "crm.doesNotExist",
                    new Dictionary<string, CandidateExpression> { ["id"] = new CandidatePathExpression("input.customerId") })
            },
            new Dictionary<string, CandidateExpression> { ["customerName"] = new CandidatePathExpression("customer.name") }),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    private static CandidateWorkflowAst CandidateWithBadBinaryOperator() => new(
        new CandidateWorkflowBody(
            "FindCustomerName",
            new CandidateNode[]
            {
                new CandidateCallNode("customer", "crm.getCustomerById",
                    new Dictionary<string, CandidateExpression> { ["id"] = new CandidatePathExpression("input.customerId") }),
                new CandidateAssertNode("guard",
                    new CandidateBinaryExpression(new CandidatePathExpression("customer.name"), "gt", new CandidateConstantExpression("", "PROMPT")),
                    "NOT_OK")
            },
            new Dictionary<string, CandidateExpression> { ["customerName"] = new CandidatePathExpression("customer.name") }),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public async Task ValidFirstCandidate_SucceedsWithZeroRepairAttempts()
    {
        var model = new ScriptedSemanticCompilerModel(ValidCandidate());
        var loop = new RepairLoop(model, WorkflowValidator.CreateDefault());

        var outcome = await loop.RunAsync(Source(), Catalog(), ValidCandidate(), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(0, outcome.AttemptsUsed);
        Assert.NotNull(outcome.Workflow);
        Assert.Empty(model.RepairRequestsReceived);
    }

    [Fact]
    public async Task InvalidFirstCandidate_RepairsOnce_ThenSucceeds()
    {
        var model = new ScriptedSemanticCompilerModel(ValidCandidate());
        var loop = new RepairLoop(model, WorkflowValidator.CreateDefault(), maxAttempts: 3);

        var outcome = await loop.RunAsync(Source(), Catalog(), BrokenCandidate(), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(1, outcome.AttemptsUsed);
        var received = Assert.Single(model.RepairRequestsReceived);
        Assert.Contains(received.Diagnostics, d => d.Code == "F001");
    }

    [Fact]
    public async Task PersistentlyInvalidCandidate_ExhaustsAttempts_ReturnsFailure()
    {
        var model = new ScriptedSemanticCompilerModel(BrokenCandidate(), BrokenCandidate());
        var loop = new RepairLoop(model, WorkflowValidator.CreateDefault(), maxAttempts: 2);

        var outcome = await loop.RunAsync(Source(), Catalog(), BrokenCandidate(), CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal(2, outcome.AttemptsUsed);
        Assert.Null(outcome.Workflow);
        Assert.NotEmpty(outcome.Diagnostics);
        Assert.Equal(2, model.RepairRequestsReceived.Count);
    }

    [Fact]
    public async Task MalformedEnumInCandidate_ProducesC001Diagnostic_AndRepairsSuccessfully()
    {
        var model = new ScriptedSemanticCompilerModel(ValidCandidate());
        var loop = new RepairLoop(model, WorkflowValidator.CreateDefault());

        var outcome = await loop.RunAsync(Source(), Catalog(), CandidateWithBadBinaryOperator(), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal(1, outcome.AttemptsUsed);
        var received = Assert.Single(model.RepairRequestsReceived);
        Assert.Contains(received.Diagnostics, d => d.Code == "C001" && d.Message.Contains("gt"));
    }

    private sealed class ThrowingOnRepairModel : ISemanticCompilerModel
    {
        public Task<CandidateWorkflowAst> GenerateCandidateAsync(SemanticCompilationRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException("not used in this test");

        public Task<CandidateWorkflowAst> RepairCandidateAsync(SemanticRepairRequest request, CancellationToken cancellationToken)
            => throw new JsonException("truncated repair response");
    }

    [Fact]
    public async Task RepairCandidateAsync_ThrowsJsonException_FailsWithC001_InsteadOfPropagating()
    {
        var model = new ThrowingOnRepairModel();
        var loop = new RepairLoop(model, WorkflowValidator.CreateDefault());

        var outcome = await loop.RunAsync(Source(), Catalog(), BrokenCandidate(), CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Contains(outcome.Diagnostics, d => d.Code == "C001");
    }
}
