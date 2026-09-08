using Flow.IR.Expressions;

namespace Flow.IR.Nodes;

public sealed record ForeachNode(
    string Id,
    FlowExpression Source,
    string ParameterName,
    int Limit,
    IReadOnlyList<WorkflowNode> Body,
    FlowExpression BodyValue) : WorkflowNode(Id);
