using Flow.Contracts;
using Flow.Validation;

namespace Flow.Compiler;

public sealed class PromptCompiler : IPromptWorkflowCompiler
{
    private readonly CompilationSession _session;

    public PromptCompiler(ISemanticCompilerModel model, int maxRepairAttempts = 3)
    {
        _session = new CompilationSession(model, WorkflowValidator.CreateDefault(), maxRepairAttempts);
    }

    public Task<CompilationResult> CompileAsync(
        WorkflowSource source, ToolCatalog tools, CancellationToken cancellationToken = default)
        => _session.CompileAsync(source, tools, cancellationToken);
}
