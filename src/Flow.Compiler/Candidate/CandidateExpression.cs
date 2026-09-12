using System.Text.Json.Serialization;

namespace Flow.Compiler.Candidate;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CandidatePathExpression), "path")]
[JsonDerivedType(typeof(CandidateConstantExpression), "constant")]
[JsonDerivedType(typeof(CandidateBinaryExpression), "binary")]
[JsonDerivedType(typeof(CandidateObjectExpression), "object")]
public abstract record CandidateExpression;

public sealed record CandidatePathExpression(string Path) : CandidateExpression;

public sealed record CandidateConstantExpression(object? Value, string? Source, string? Detail = null) : CandidateExpression;

public sealed record CandidateBinaryExpression(CandidateExpression Left, string Operator, CandidateExpression Right) : CandidateExpression;

public sealed record CandidateObjectExpression(IReadOnlyDictionary<string, CandidateExpression> Fields) : CandidateExpression;
