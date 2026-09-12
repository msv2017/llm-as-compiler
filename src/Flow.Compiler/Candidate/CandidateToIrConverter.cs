using System.Text.Json;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.Compiler.Candidate;

public static class CandidateToIrConverter
{
    public static WorkflowDefinition Convert(CandidateWorkflowAst ast, FlowType inputType, FlowType outputType)
    {
        var nodes = ConvertNodes(ast.Workflow.Nodes);
        var returnFields = ConvertExpressionMap(ast.Workflow.Return);
        return new WorkflowDefinition(ast.Workflow.Name, inputType, outputType, nodes, new ReturnNode(returnFields));
    }

    private static IReadOnlyList<WorkflowNode> ConvertNodes(IReadOnlyList<CandidateNode> nodes) =>
        nodes.Select(ConvertNode).ToList();

    private static WorkflowNode ConvertNode(CandidateNode node) => node switch
    {
        CandidateCallNode call => new CallNode(call.Id, call.Tool, ConvertExpressionMap(call.Arguments)),
        CandidateIfNode ifNode => new IfNode(
            ifNode.Id, ConvertExpression(ifNode.Condition), ConvertBranch(ifNode.TrueBranch), ConvertBranch(ifNode.FalseBranch)),
        CandidateFilterNode filter => new FilterNode(
            filter.Id, ConvertExpression(filter.Source), filter.ParameterName, ConvertExpression(filter.Predicate)),
        CandidateSortNode sort => new SortNode(
            sort.Id, ConvertExpression(sort.Source), sort.ParameterName, ConvertExpression(sort.Key), ParseDirection(sort.Direction)),
        CandidateAggregateNode aggregate => new AggregateNode(
            aggregate.Id, ConvertExpression(aggregate.Source), ParseOperation(aggregate.Operation),
            aggregate.ParameterName, aggregate.Selector is null ? null : ConvertExpression(aggregate.Selector)),
        CandidateForeachNode foreachNode => new ForeachNode(
            foreachNode.Id, ConvertExpression(foreachNode.Source), foreachNode.ParameterName, foreachNode.Limit,
            ConvertNodes(foreachNode.Body), ConvertExpression(foreachNode.BodyValue)),
        CandidateAssertNode assert => new AssertNode(assert.Id, ConvertExpression(assert.Condition), assert.FailureCode),
        _ => throw new NotSupportedException($"Unsupported candidate node kind '{node.GetType().Name}'.")
    };

    private static IfBranch ConvertBranch(CandidateIfBranch branch) =>
        new(ConvertNodes(branch.Nodes), ConvertExpression(branch.Value));

    private static IReadOnlyDictionary<string, FlowExpression> ConvertExpressionMap(
        IReadOnlyDictionary<string, CandidateExpression> map) =>
        map.ToDictionary(kv => kv.Key, kv => ConvertExpression(kv.Value));

    private static FlowExpression ConvertExpression(CandidateExpression expression) => expression switch
    {
        CandidatePathExpression path => new PathExpression(path.Path),
        CandidateConstantExpression constant => new ConstantExpression(
            NormalizeValue(constant.Value), ParseProvenance(constant.Source, constant.Detail)),
        CandidateBinaryExpression binary => new BinaryExpression(
            ConvertExpression(binary.Left), ParseOperator(binary.Operator), ConvertExpression(binary.Right)),
        CandidateObjectExpression obj => new ObjectExpression(ConvertExpressionMap(obj.Fields)),
        _ => throw new NotSupportedException($"Unsupported candidate expression kind '{expression.GetType().Name}'.")
    };

    private static BinaryOperator ParseOperator(string op) => Enum.Parse<BinaryOperator>(op, ignoreCase: true);

    private static SortDirection ParseDirection(string direction) => direction.ToUpperInvariant() switch
    {
        "ASCENDING" => SortDirection.Ascending,
        "DESCENDING" => SortDirection.Descending,
        _ => throw new ArgumentException($"Unrecognized sort direction '{direction}'.")
    };

    private static AggregateOperation ParseOperation(string operation) => Enum.Parse<AggregateOperation>(operation, ignoreCase: true);

    private static ConstantProvenance ParseProvenance(string? source, string? detail) => source?.ToUpperInvariant() switch
    {
        "PROMPT" => new ConstantProvenance(ConstantProvenanceKind.Prompt, detail),
        "TOOL_SCHEMA" => new ConstantProvenance(ConstantProvenanceKind.ToolSchema, detail),
        "SYSTEM_POLICY" => new ConstantProvenance(ConstantProvenanceKind.SystemPolicy, detail),
        "EXPLICIT_COMPILER_POLICY" => new ConstantProvenance(ConstantProvenanceKind.ExplicitCompilerPolicy, detail),
        _ => new ConstantProvenance(ConstantProvenanceKind.Unproven, detail)
    };

    private static object? NormalizeValue(object? raw) => raw is JsonElement element ? NormalizeJsonElement(element) : raw;

    private static object? NormalizeJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDecimal(),
        _ => throw new NotSupportedException($"Unsupported constant JSON kind '{element.ValueKind}'.")
    };
}
