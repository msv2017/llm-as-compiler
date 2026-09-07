using Flow.IR.Expressions;

namespace Flow.IR.Nodes;

public sealed record CallNode(
    string Id,
    string ToolName,
    IReadOnlyDictionary<string, FlowExpression> Arguments) : WorkflowNode(Id);
