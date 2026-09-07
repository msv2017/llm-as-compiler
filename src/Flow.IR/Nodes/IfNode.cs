using Flow.IR.Expressions;

namespace Flow.IR.Nodes;

public sealed record IfBranch(IReadOnlyList<WorkflowNode> Nodes, FlowExpression Value);

public sealed record IfNode(string Id, FlowExpression Condition, IfBranch TrueBranch, IfBranch FalseBranch) : WorkflowNode(Id);
