using Flow.IR.Expressions;

namespace Flow.IR.Nodes;

public sealed record ReturnNode(IReadOnlyDictionary<string, FlowExpression> Fields) : WorkflowNode("return");
