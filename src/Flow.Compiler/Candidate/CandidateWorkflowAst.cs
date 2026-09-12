namespace Flow.Compiler.Candidate;

public sealed record CandidateWorkflowBody(
    string Name,
    IReadOnlyList<CandidateNode> Nodes,
    IReadOnlyDictionary<string, CandidateExpression> Return);

public sealed record CandidateWorkflowAst(
    CandidateWorkflowBody Workflow,
    IReadOnlyList<string> Interpretations,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Unresolved);
