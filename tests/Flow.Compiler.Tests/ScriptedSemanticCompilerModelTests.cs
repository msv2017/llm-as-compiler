using Flow.Compiler;
using Flow.Compiler.Candidate;
using Flow.Compiler.Tests.Fakes;
using Flow.Contracts;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Compiler.Tests;

public class ScriptedSemanticCompilerModelTests
{
    private static CandidateWorkflowAst EmptyCandidate(string name) => new(
        new CandidateWorkflowBody(name, Array.Empty<CandidateNode>(), new Dictionary<string, CandidateExpression>()),
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public async Task GenerateCandidateAsync_ReturnsFirstScriptedResponse()
    {
        var model = new ScriptedSemanticCompilerModel(EmptyCandidate("First"), EmptyCandidate("Second"));
        var source = new WorkflowSource("prompt", PrimitiveType.String, PrimitiveType.String);
        var tools = new ToolCatalog(Array.Empty<ToolDefinition>());

        var candidate = await model.GenerateCandidateAsync(new SemanticCompilationRequest(source, tools), CancellationToken.None);

        Assert.Equal("First", candidate.Workflow.Name);
    }

    [Fact]
    public async Task RepairCandidateAsync_ReturnsNextScriptedResponse_AndRecordsRequest()
    {
        var first = EmptyCandidate("First");
        var second = EmptyCandidate("Second");
        var model = new ScriptedSemanticCompilerModel(first, second);
        var source = new WorkflowSource("prompt", PrimitiveType.String, PrimitiveType.String);
        var tools = new ToolCatalog(Array.Empty<ToolDefinition>());

        await model.GenerateCandidateAsync(new SemanticCompilationRequest(source, tools), CancellationToken.None);
        var diagnostics = new[] { new Flow.Validation.ValidationDiagnostic("T101", "mismatch") };
        var repaired = await model.RepairCandidateAsync(
            new SemanticRepairRequest(source, tools, first, diagnostics), CancellationToken.None);

        Assert.Equal("Second", repaired.Workflow.Name);
        var received = Assert.Single(model.RepairRequestsReceived);
        Assert.Same(first, received.PriorCandidate);
        Assert.Same(diagnostics, received.Diagnostics);
    }

    [Fact]
    public async Task Dequeuing_PastScriptedResponses_Throws()
    {
        var model = new ScriptedSemanticCompilerModel(EmptyCandidate("Only"));
        var source = new WorkflowSource("prompt", PrimitiveType.String, PrimitiveType.String);
        var tools = new ToolCatalog(Array.Empty<ToolDefinition>());

        await model.GenerateCandidateAsync(new SemanticCompilationRequest(source, tools), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            model.GenerateCandidateAsync(new SemanticCompilationRequest(source, tools), CancellationToken.None));
    }
}
