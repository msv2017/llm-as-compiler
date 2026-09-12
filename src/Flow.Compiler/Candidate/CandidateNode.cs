using System.Text.Json.Serialization;

namespace Flow.Compiler.Candidate;

[JsonConverter(typeof(CandidateNodeConverter))]
public abstract record CandidateNode
{
    public required string Id { get; init; }

    protected CandidateNode() { }

    protected CandidateNode(string id) => Id = id;
}

public sealed record CandidateCallNode(
    string Id, string Tool, IReadOnlyDictionary<string, CandidateExpression> Arguments) : CandidateNode(Id);

public sealed record CandidateIfBranch(IReadOnlyList<CandidateNode> Nodes, CandidateExpression Value);

public sealed record CandidateIfNode(
    string Id, CandidateExpression Condition, CandidateIfBranch TrueBranch, CandidateIfBranch FalseBranch) : CandidateNode(Id);

public sealed record CandidateFilterNode(
    string Id, CandidateExpression Source, string ParameterName, CandidateExpression Predicate) : CandidateNode(Id);

public sealed record CandidateSortNode(
    string Id, CandidateExpression Source, string ParameterName, CandidateExpression Key, string Direction) : CandidateNode(Id);

public sealed record CandidateAggregateNode(
    string Id, CandidateExpression Source, string Operation, string? ParameterName, CandidateExpression? Selector) : CandidateNode(Id);

public sealed record CandidateForeachNode(
    string Id, CandidateExpression Source, string ParameterName, int Limit,
    IReadOnlyList<CandidateNode> Body, CandidateExpression BodyValue) : CandidateNode(Id);

public sealed record CandidateAssertNode(string Id, CandidateExpression Condition, string FailureCode) : CandidateNode(Id);
