namespace Flow.IR.Expressions;

public sealed record ConstantExpression(object? Value, ConstantProvenance Provenance) : FlowExpression;
