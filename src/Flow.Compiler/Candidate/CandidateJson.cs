using System.Text.Json;

namespace Flow.Compiler.Candidate;

public static class CandidateJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
