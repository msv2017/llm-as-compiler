using Flow.TypeSystem;

namespace Flow.Compiler;

public sealed record WorkflowSource(string Prompt, FlowType InputType, FlowType OutputType);
