using Flow.Contracts;

namespace Flow.Compiler;

public interface IPromptWorkflowCompiler
{
    Task<CompilationResult> CompileAsync(
        WorkflowSource source,
        ToolCatalog tools,
        CancellationToken cancellationToken = default);
}
