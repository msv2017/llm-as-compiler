using Flow.Compiler.Candidate;
using Flow.Compiler.Tests.Fakes;
using Flow.Contracts;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Compiler.Tests;

public class CompilationSessionTests
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

    private static CandidateWorkflowAst ValidCandidate(IReadOnlyList<string>? assumptions = null) => new(
        new CandidateWorkflowBody(
            "FindCustomerName",
            new CandidateNode[]
            {
                new CandidateCallNode("customer", "crm.getCustomerById",
                    new Dictionary<string, CandidateExpression> { ["id"] = new CandidatePathExpression("input.customerId") })
            },
            new Dictionary<string, CandidateExpression> { ["customerName"] = new CandidatePathExpression("customer.name") }),
        Array.Empty<string>(), assumptions ?? Array.Empty<string>(), Array.Empty<string>());

    private static CandidateWorkflowAst UnresolvedCandidate() => new(
        new CandidateWorkflowBody("Unresolvable", Array.Empty<CandidateNode>(), new Dictionary<string, CandidateExpression>()),
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { "S202: 'sounds angry' has no deterministic definition" });

    [Fact]
    public async Task ValidCandidate_CompilesSuccessfully_AndCarriesAssumptions()
    {
        var model = new ScriptedSemanticCompilerModel(ValidCandidate(new[] { "assumed 'name' means customer.name" }));
        var session = new CompilationSession(model, WorkflowValidator.CreateDefault());

        var result = await session.CompileAsync(Source(), Catalog(), CancellationToken.None);

        Assert.Equal(CompilationStatus.Success, result.Status);
        Assert.NotNull(result.Workflow);
        Assert.Empty(result.Diagnostics);
        Assert.Single(result.Assumptions);
        Assert.Equal("assumed 'name' means customer.name", result.Assumptions[0].Description);
    }

    [Fact]
    public async Task UnresolvedCandidate_FailsImmediately_WithoutAttemptingRepair()
    {
        var model = new ScriptedSemanticCompilerModel(UnresolvedCandidate());
        var session = new CompilationSession(model, WorkflowValidator.CreateDefault());

        var result = await session.CompileAsync(Source(), Catalog(), CancellationToken.None);

        Assert.Equal(CompilationStatus.Uncompilable, result.Status);
        Assert.Null(result.Workflow);
        Assert.Single(result.Unresolved);
        Assert.Empty(model.RepairRequestsReceived);
    }

    [Fact]
    public async Task RepairSucceeds_ReportsFinalCandidateAssumptions_NotFirstCandidateAssumptions()
    {
        var firstCandidate = new CandidateWorkflowAst(
            new CandidateWorkflowBody(
                "FindCustomerName",
                new CandidateNode[]
                {
                    new CandidateCallNode("customer", "crm.doesNotExist",
                        new Dictionary<string, CandidateExpression> { ["id"] = new CandidatePathExpression("input.customerId") })
                },
                new Dictionary<string, CandidateExpression> { ["customerName"] = new CandidatePathExpression("customer.name") }),
            Array.Empty<string>(), new[] { "first-candidate-assumption" }, Array.Empty<string>());
        var secondCandidate = ValidCandidate(new[] { "second-candidate-assumption" });

        var model = new ScriptedSemanticCompilerModel(firstCandidate, secondCandidate);
        var session = new CompilationSession(model, WorkflowValidator.CreateDefault());

        var result = await session.CompileAsync(Source(), Catalog(), CancellationToken.None);

        Assert.Equal(CompilationStatus.Success, result.Status);
        Assert.Contains(result.Assumptions, a => a.Description == "second-candidate-assumption");
        Assert.DoesNotContain(result.Assumptions, a => a.Description == "first-candidate-assumption");
    }

    [Fact]
    public async Task UnresolvedEntry_WithoutRecognizableCodePrefix_FallsBackToS203()
    {
        var candidate = new CandidateWorkflowAst(
            new CandidateWorkflowBody("Unresolvable", Array.Empty<CandidateNode>(), new Dictionary<string, CandidateExpression>()),
            Array.Empty<string>(), Array.Empty<string>(),
            new[] { "the phrase 'sounds angry' has no deterministic definition" });
        var model = new ScriptedSemanticCompilerModel(candidate);
        var session = new CompilationSession(model, WorkflowValidator.CreateDefault());

        var result = await session.CompileAsync(Source(), Catalog(), CancellationToken.None);

        var unresolved = Assert.Single(result.Unresolved);
        Assert.Equal("S203", unresolved.Code);
        Assert.Equal("the phrase 'sounds angry' has no deterministic definition", unresolved.Description);
    }

    [Fact]
    public async Task UnresolvedEntry_WithRecognizableCodePrefix_ParsesCodeAndDescription()
    {
        var candidate = new CandidateWorkflowAst(
            new CandidateWorkflowBody("Unresolvable", Array.Empty<CandidateNode>(), new Dictionary<string, CandidateExpression>()),
            Array.Empty<string>(), Array.Empty<string>(),
            new[] { "S202: no rule" });
        var model = new ScriptedSemanticCompilerModel(candidate);
        var session = new CompilationSession(model, WorkflowValidator.CreateDefault());

        var result = await session.CompileAsync(Source(), Catalog(), CancellationToken.None);

        var unresolved = Assert.Single(result.Unresolved);
        Assert.Equal("S202", unresolved.Code);
        Assert.Equal("no rule", unresolved.Description);
    }
}
