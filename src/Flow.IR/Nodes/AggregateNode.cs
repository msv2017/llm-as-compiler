using Flow.IR.Expressions;

namespace Flow.IR.Nodes;

public enum AggregateOperation { Sum, Count, First }

public sealed record AggregateNode(
    string Id,
    FlowExpression Source,
    AggregateOperation Operation,
    string? ParameterName,
    FlowExpression? Selector) : WorkflowNode(Id);
