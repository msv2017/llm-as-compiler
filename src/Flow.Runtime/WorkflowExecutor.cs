using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;

namespace Flow.Runtime;

public sealed record ExecutionResult(bool Success, FlowRecord? Output, string? FailureReason = null);

public sealed class WorkflowExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(
        WorkflowDefinition workflow,
        object? input,
        IMcpInvoker invoker,
        CancellationToken cancellationToken = default)
    {
        var context = new WorkflowExecutionContext(input);

        foreach (var node in workflow.Nodes)
        {
            await ExecuteNodeAsync(node, context, invoker, cancellationToken);
        }

        var outputFields = workflow.Return.Fields.ToDictionary(
            kv => kv.Key, kv => Evaluate(kv.Value, context));

        return new ExecutionResult(true, new FlowRecord(outputFields));
    }

    // Extend this switch whenever a new WorkflowNode kind gains runtime support.
    private static async Task ExecuteNodeAsync(
        WorkflowNode node, WorkflowExecutionContext context, IMcpInvoker invoker, CancellationToken cancellationToken)
    {
        switch (node)
        {
            case CallNode call:
                var arguments = call.Arguments.ToDictionary(kv => kv.Key, kv => Evaluate(kv.Value, context));
                var result = await invoker.InvokeAsync(call.ToolName, arguments, cancellationToken);
                context.Bind(call.Id, result);
                break;

            case IfNode ifNode:
                var conditionValue = Evaluate(ifNode.Condition, context);
                var branch = conditionValue is true ? ifNode.TrueBranch : ifNode.FalseBranch;
                foreach (var branchNode in branch.Nodes)
                    await ExecuteNodeAsync(branchNode, context, invoker, cancellationToken);
                context.Bind(ifNode.Id, Evaluate(branch.Value, context));
                break;

            default:
                throw new NotSupportedException($"Node kind '{node.GetType().Name}' is not supported yet.");
        }
    }

    private static object? Evaluate(FlowExpression expression, WorkflowExecutionContext context) => expression switch
    {
        PathExpression path => context.Resolve(path.Path),
        ConstantExpression constant => constant.Value,
        BinaryExpression binary => EvaluateBinary(binary, context),
        _ => throw new NotSupportedException($"Expression kind '{expression.GetType().Name}' is not supported yet.")
    };

    private static object EvaluateBinary(BinaryExpression binary, WorkflowExecutionContext context)
    {
        var left = Evaluate(binary.Left, context);
        var right = Evaluate(binary.Right, context);

        return binary.Operator switch
        {
            BinaryOperator.Equal => Equals(left, right),
            BinaryOperator.NotEqual => !Equals(left, right),
            BinaryOperator.LessThan => Comparer<object>.Default.Compare(left, right) < 0,
            BinaryOperator.LessThanOrEqual => Comparer<object>.Default.Compare(left, right) <= 0,
            BinaryOperator.GreaterThan => Comparer<object>.Default.Compare(left, right) > 0,
            BinaryOperator.GreaterThanOrEqual => Comparer<object>.Default.Compare(left, right) >= 0,
            BinaryOperator.And => left is true && right is true,
            BinaryOperator.Or => left is true || right is true,
            _ => throw new NotSupportedException($"Operator '{binary.Operator}' is not supported yet.")
        };
    }
}
