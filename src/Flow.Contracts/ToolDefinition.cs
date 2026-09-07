using Flow.TypeSystem;

namespace Flow.Contracts;

public sealed record ToolDefinition(
    string Name,
    ObjectType InputType,
    FlowType OutputType,
    ToolEffect Effect,
    ToolRetryPolicy Retry);
