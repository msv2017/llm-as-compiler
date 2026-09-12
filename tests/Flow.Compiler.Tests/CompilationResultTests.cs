using Flow.Compiler;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Compiler.Tests;

public class CompilationResultTests
{
    [Fact]
    public void UncompilableResult_CarriesDiagnosticsAssumptionsAndUnresolved()
    {
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        var source = new WorkflowSource("Find the customer by email.", inputType, outputType);

        Assert.Equal("Find the customer by email.", source.Prompt);
        Assert.Same(inputType, source.InputType);
        Assert.Same(outputType, source.OutputType);

        var result = new CompilationResult(
            CompilationStatus.Uncompilable,
            Workflow: null,
            Diagnostics: new[] { new ValidationDiagnostic("T101", "mismatch") },
            Assumptions: new[] { new CompilerAssumption("assumed X means Y") },
            Unresolved: new[] { new UnresolvedSemantic("S202", "no deterministic rule") });

        Assert.Equal(CompilationStatus.Uncompilable, result.Status);
        Assert.Null(result.Workflow);
        Assert.Single(result.Diagnostics);
        Assert.Single(result.Assumptions);
        Assert.Single(result.Unresolved);
        Assert.Equal("S202", result.Unresolved[0].Code);
    }
}
