using Flow.IR.Expressions;

namespace Flow.IR.Nodes;

public sealed record AssertNode(string Id, FlowExpression Condition, string FailureCode) : WorkflowNode(Id);
