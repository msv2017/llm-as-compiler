using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;

namespace Flow.Compiler.Candidate;

public static class IrToCandidateConverter
{
    public static CandidateWorkflowBody Convert(WorkflowDefinition workflow) =>
        new(workflow.Name, ConvertNodes(workflow.Nodes), ConvertExpressionMap(workflow.Return.Fields));

    private static IReadOnlyList<CandidateNode> ConvertNodes(IReadOnlyList<WorkflowNode> nodes) =>
        nodes.Select(ConvertNode).ToList();

    private static CandidateNode ConvertNode(WorkflowNode node) => node switch
    {
        CallNode call => new CandidateCallNode(call.Id, call.ToolName, ConvertExpressionMap(call.Arguments)),
        IfNode ifNode => new CandidateIfNode(
            ifNode.Id, ConvertExpression(ifNode.Condition), ConvertBranch(ifNode.TrueBranch), ConvertBranch(ifNode.FalseBranch)),
        FilterNode filter => new CandidateFilterNode(
            filter.Id, ConvertExpression(filter.Source), filter.ParameterName, ConvertExpression(filter.Predicate)),
        SortNode sort => new CandidateSortNode(
            sort.Id, ConvertExpression(sort.Source), sort.ParameterName, ConvertExpression(sort.Key), ToDirectionString(sort.Direction)),
        AggregateNode aggregate => new CandidateAggregateNode(
            aggregate.Id, ConvertExpression(aggregate.Source), aggregate.Operation.ToString(),
            aggregate.ParameterName, aggregate.Selector is null ? null : ConvertExpression(aggregate.Selector)),
        ForeachNode foreachNode => new CandidateForeachNode(
            foreachNode.Id, ConvertExpression(foreachNode.Source), foreachNode.ParameterName, foreachNode.Limit,
            ConvertNodes(foreachNode.Body), ConvertExpression(foreachNode.BodyValue)),
        AssertNode assert => new CandidateAssertNode(assert.Id, ConvertExpression(assert.Condition), assert.FailureCode),
        _ => throw new NotSupportedException($"Unsupported node kind '{node.GetType().Name}'.")
    };

    private static CandidateIfBranch ConvertBranch(IfBranch branch) =>
        new(ConvertNodes(branch.Nodes), ConvertExpression(branch.Value));

    private static IReadOnlyDictionary<string, CandidateExpression> ConvertExpressionMap(
        IReadOnlyDictionary<string, FlowExpression> map) =>
        map.ToDictionary(kv => kv.Key, kv => ConvertExpression(kv.Value));

    private static CandidateExpression ConvertExpression(FlowExpression expression) => expression switch
    {
        PathExpression path => new CandidatePathExpression(path.Path),
        ConstantExpression constant => new CandidateConstantExpression(
            constant.Value, ToProvenanceSourceString(constant.Provenance.Kind), constant.Provenance.Detail),
        BinaryExpression binary => new CandidateBinaryExpression(
            ConvertExpression(binary.Left), binary.Operator.ToString(), ConvertExpression(binary.Right)),
        ObjectExpression obj => new CandidateObjectExpression(ConvertExpressionMap(obj.Fields)),
        _ => throw new NotSupportedException($"Unsupported expression kind '{expression.GetType().Name}'.")
    };

    private static string ToDirectionString(SortDirection direction) => direction switch
    {
        SortDirection.Ascending => "ASCENDING",
        SortDirection.Descending => "DESCENDING",
        _ => throw new NotSupportedException($"Unsupported sort direction '{direction}'.")
    };

    // Mirrors CandidateToIrConverter.ParseProvenance's string vocabulary exactly, except HandWritten:
    // that kind is only ever assigned by hand-written IR fixtures in tests, never by ParseProvenance
    // itself, so it can never appear in a WorkflowDefinition produced by an actual `compile` run --
    // the only kind of WorkflowDefinition this converter is ever asked to convert.
    private static string ToProvenanceSourceString(ConstantProvenanceKind kind) => kind switch
    {
        ConstantProvenanceKind.Prompt => "PROMPT",
        ConstantProvenanceKind.ToolSchema => "TOOL_SCHEMA",
        ConstantProvenanceKind.SystemPolicy => "SYSTEM_POLICY",
        ConstantProvenanceKind.ExplicitCompilerPolicy => "EXPLICIT_COMPILER_POLICY",
        ConstantProvenanceKind.HandWritten => "HAND_WRITTEN",
        ConstantProvenanceKind.Unproven => "UNPROVEN",
        _ => throw new NotSupportedException($"Unsupported provenance kind '{kind}'.")
    };
}
