using System.Text.Json;
using System.Text.Json.Nodes;

namespace Flow.Compiler.Providers;

public static class ResponseJsonRewriter
{
    private static readonly string[] DictionaryShapedProperties = { "arguments", "return", "fields" };

    public static string RewriteDictionaryShapedFields(string rawJson)
    {
        var root = JsonNode.Parse(rawJson) ?? throw new JsonException("Response body was empty.");
        RewriteInPlace(root);
        return root.ToJsonString();
    }

    private static void RewriteInPlace(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var propertyName in obj.Select(kv => kv.Key).ToList())
                {
                    if (DictionaryShapedProperties.Contains(propertyName) && obj[propertyName] is JsonArray pairs)
                    {
                        obj[propertyName] = RewritePairsToObject(pairs);
                    }
                    else
                    {
                        RewriteInPlace(obj[propertyName]);
                    }
                }
                break;

            case JsonArray array:
                foreach (var item in array)
                    RewriteInPlace(item);
                break;
        }
    }

    private static JsonObject RewritePairsToObject(JsonArray pairs)
    {
        var result = new JsonObject();
        foreach (var pair in pairs)
        {
            var pairObject = (JsonObject)pair!;
            var name = pairObject["name"]!.GetValue<string>();
            var value = pairObject["value"]?.DeepClone();
            RewriteInPlace(value);
            result[name] = value;
        }
        return result;
    }
}
