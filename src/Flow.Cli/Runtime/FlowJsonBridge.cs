using System.Text.Json;
using System.Text.Json.Nodes;
using Flow.Runtime;

namespace Flow.Cli.Runtime;

public static class FlowJsonBridge
{
    public static object? ToFlowValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? (object)integer : (object)element.GetDecimal(),
        JsonValueKind.Array => element.EnumerateArray().Select(ToFlowValue).ToList(),
        JsonValueKind.Object => new FlowRecord(
            element.EnumerateObject().ToDictionary(property => property.Name, property => ToFlowValue(property.Value))),
        _ => throw new NotSupportedException($"Unsupported JSON value kind '{element.ValueKind}'.")
    };

    public static JsonNode? ToJsonValue(object? value) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        string s => JsonValue.Create(s),
        long l => JsonValue.Create(l),
        int i => JsonValue.Create(i),
        decimal d => JsonValue.Create(d),
        double d => JsonValue.Create(d),
        List<object?> list => new JsonArray(list.Select(ToJsonValue).ToArray()),
        FlowRecord record => new JsonObject(
            record.Fields.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, ToJsonValue(kv.Value)))),
        _ => throw new NotSupportedException($"Unsupported value type '{value.GetType().Name}' for JSON conversion.")
    };
}
