namespace Flow.IR.Expressions;

public sealed record ObjectExpression(IReadOnlyDictionary<string, FlowExpression> Fields) : FlowExpression;
