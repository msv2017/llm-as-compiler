namespace Flow.IR.Expressions;

public enum BinaryOperator { Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual, And, Or }

public sealed record BinaryExpression(FlowExpression Left, BinaryOperator Operator, FlowExpression Right) : FlowExpression;
