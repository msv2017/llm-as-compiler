using Flow.IR;
using Flow.Validation;

namespace Flow.Compiler;

public enum CompilationStatus { Success, Uncompilable }

public sealed record CompilerAssumption(string Description);

public sealed record UnresolvedSemantic(string Code, string Description);

public sealed record CompilationResult(
    CompilationStatus Status,
    WorkflowDefinition? Workflow,
    IReadOnlyList<ValidationDiagnostic> Diagnostics,
    IReadOnlyList<CompilerAssumption> Assumptions,
    IReadOnlyList<UnresolvedSemantic> Unresolved);
