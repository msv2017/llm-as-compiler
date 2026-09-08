using Flow.IR.Expressions;

namespace Flow.IR.Nodes;

public sealed record FilterNode(
    string Id,
    FlowExpression Source,
    string ParameterName,
    FlowExpression Predicate) : WorkflowNode(Id);
