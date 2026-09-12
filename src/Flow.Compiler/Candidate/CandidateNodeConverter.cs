using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow.Compiler.Candidate;

public class CandidateNodeConverter : JsonConverter<CandidateNode>
{
    public override CandidateNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("kind", out var kindElement))
            throw new JsonException("Missing 'kind' property");

        var kind = kindElement.GetString();
        var json = root.GetRawText();

        return kind switch
        {
            "call" => JsonSerializer.Deserialize<CandidateCallNode>(json, options) ?? throw new JsonException("Failed to deserialize CandidateCallNode"),
            "if" => JsonSerializer.Deserialize<CandidateIfNode>(json, options) ?? throw new JsonException("Failed to deserialize CandidateIfNode"),
            "filter" => JsonSerializer.Deserialize<CandidateFilterNode>(json, options) ?? throw new JsonException("Failed to deserialize CandidateFilterNode"),
            "sort" => JsonSerializer.Deserialize<CandidateSortNode>(json, options) ?? throw new JsonException("Failed to deserialize CandidateSortNode"),
            "aggregate" => JsonSerializer.Deserialize<CandidateAggregateNode>(json, options) ?? throw new JsonException("Failed to deserialize CandidateAggregateNode"),
            "foreach" => JsonSerializer.Deserialize<CandidateForeachNode>(json, options) ?? throw new JsonException("Failed to deserialize CandidateForeachNode"),
            "assert" => JsonSerializer.Deserialize<CandidateAssertNode>(json, options) ?? throw new JsonException("Failed to deserialize CandidateAssertNode"),
            _ => throw new JsonException($"Unknown kind: {kind}")
        };
    }

    public override void Write(Utf8JsonWriter writer, CandidateNode value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, options);
    }
}
