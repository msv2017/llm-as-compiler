using Flow.IR.Expressions;

namespace Flow.IR.Nodes;

public enum SortDirection { Ascending, Descending }

public sealed record SortNode(
    string Id,
    FlowExpression Source,
    string ParameterName,
    FlowExpression Key,
    SortDirection Direction) : WorkflowNode(Id);
