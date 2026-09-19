using System.Text;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;

namespace Flow.Cli;

public static class WorkflowPrinter
{
    public static string Print(WorkflowDefinition workflow)
    {
        var builder = new StringBuilder();
        PrintNodes(workflow.Nodes, builder, indent: 1);
        PrintNode(workflow.Return, builder, indent: 1);
        return builder.ToString();
    }

    private static void PrintNodes(IReadOnlyList<WorkflowNode> nodes, StringBuilder builder, int indent)
    {
        foreach (var node in nodes)
            PrintNode(node, builder, indent);
    }

    private static void PrintNode(WorkflowNode node, StringBuilder builder, int indent)
    {
        var pad = new string(' ', indent * 2);
        var innerPad = new string(' ', (indent + 1) * 2);

        switch (node)
        {
            case CallNode call:
                var args = string.Join(", ", call.Arguments.Select(a => $"{a.Key}: {PrintExpression(a.Value)}"));
                builder.AppendLine($"{pad}call {call.Id} = {call.ToolName}({args})");
                break;

            case IfNode ifNode:
                builder.AppendLine($"{pad}if {PrintExpression(ifNode.Condition)} -> {ifNode.Id}:");
                PrintNodes(ifNode.TrueBranch.Nodes, builder, indent + 1);
                builder.AppendLine($"{innerPad}=> {PrintExpression(ifNode.TrueBranch.Value)}");
                builder.AppendLine($"{pad}else:");
                PrintNodes(ifNode.FalseBranch.Nodes, builder, indent + 1);
                builder.AppendLine($"{innerPad}=> {PrintExpression(ifNode.FalseBranch.Value)}");
                break;

            case FilterNode filter:
                builder.AppendLine(
                    $"{pad}filter {filter.Id} = {PrintExpression(filter.Source)}.where({filter.ParameterName} => {PrintExpression(filter.Predicate)})");
                break;

            case SortNode sort:
                builder.AppendLine(
                    $"{pad}sort {sort.Id} = {PrintExpression(sort.Source)}.orderBy({sort.ParameterName} => {PrintExpression(sort.Key)}, {sort.Direction})");
                break;

            case AggregateNode aggregate:
                var selector = aggregate.Selector is null
                    ? ""
                    : $"({aggregate.ParameterName} => {PrintExpression(aggregate.Selector)})";
                builder.AppendLine($"{pad}aggregate {aggregate.Id} = {PrintExpression(aggregate.Source)}.{aggregate.Operation}{selector}");
                break;

            case ForeachNode foreachNode:
                builder.AppendLine(
                    $"{pad}foreach {foreachNode.ParameterName} in {PrintExpression(foreachNode.Source)} (limit {foreachNode.Limit}) -> {foreachNode.Id}:");
                PrintNodes(foreachNode.Body, builder, indent + 1);
                builder.AppendLine($"{innerPad}=> {PrintExpression(foreachNode.BodyValue)}");
                break;

            case AssertNode assertNode:
                builder.AppendLine($"{pad}assert {PrintExpression(assertNode.Condition)} else {assertNode.FailureCode}");
                break;

            case ReturnNode returnNode:
                var fields = string.Join(", ", returnNode.Fields.Select(f => $"{f.Key}: {PrintExpression(f.Value)}"));
                builder.AppendLine($"{pad}return {{ {fields} }}");
                break;
        }
    }

    private static string PrintExpression(FlowExpression expression) => expression switch
    {
        PathExpression path => path.Path,
        ConstantExpression constant => PrintConstant(constant.Value),
        BinaryExpression binary => $"({PrintExpression(binary.Left)} {PrintOperator(binary.Operator)} {PrintExpression(binary.Right)})",
        ObjectExpression obj => $"{{ {string.Join(", ", obj.Fields.Select(f => $"{f.Key}: {PrintExpression(f.Value)}"))} }}",
        _ => expression.ToString() ?? ""
    };

    private static string PrintConstant(object? value) => value switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        string s => $"\"{s}\"",
        _ => value.ToString() ?? ""
    };

    private static string PrintOperator(BinaryOperator op) => op switch
    {
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessThanOrEqual => "<=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterThanOrEqual => ">=",
        BinaryOperator.And => "&&",
        BinaryOperator.Or => "||",
        _ => op.ToString()
    };
}
