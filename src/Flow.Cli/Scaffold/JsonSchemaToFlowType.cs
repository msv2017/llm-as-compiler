using System.Text.Json;

namespace Flow.Cli.Scaffold;

internal static class JsonSchemaToFlowType
{
    public static FlowTypeJson Convert(JsonElement schema, string typeName, List<string> warnings, string warningPath)
    {
        if (!schema.TryGetProperty("type", out var typeProperty) || typeProperty.ValueKind != JsonValueKind.String)
            return Unsupported(warnings, warningPath);

        var type = typeProperty.GetString();

        if (type == "string" && schema.TryGetProperty("enum", out var enumProperty) && enumProperty.ValueKind == JsonValueKind.Array)
        {
            var values = new List<string>();
            var allStrings = true;
            foreach (var entry in enumProperty.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.String)
                {
                    allStrings = false;
                    break;
                }
                values.Add(entry.GetString()!);
            }
            if (allStrings)
                return new FlowTypeJson { Kind = "enum", Name = typeName, Values = values };
            warnings.Add($"{warningPath}: enum contains a non-string value; treating as a plain string instead of an enum.");
        }

        return type switch
        {
            "string" => new FlowTypeJson { Kind = "primitive", Name = "String" },
            "integer" => new FlowTypeJson { Kind = "primitive", Name = "Int" },
            "number" => new FlowTypeJson { Kind = "primitive", Name = "Decimal" },
            "boolean" => new FlowTypeJson { Kind = "primitive", Name = "Bool" },
            "object" => ConvertObject(schema, typeName, warnings, warningPath),
            "array" => ConvertArray(schema, typeName, warnings, warningPath),
            _ => Unsupported(warnings, warningPath)
        };
    }

    private static FlowTypeJson ConvertObject(JsonElement schema, string typeName, List<string> warnings, string warningPath)
    {
        var required = new HashSet<string>();
        if (schema.TryGetProperty("required", out var requiredProperty) && requiredProperty.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in requiredProperty.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String && entry.GetString() is { } name)
                    required.Add(name);
                else
                    warnings.Add($"{warningPath}: 'required' contains a non-string entry; ignoring it.");
            }
        }

        var fields = new Dictionary<string, FlowTypeJson>();
        if (schema.TryGetProperty("properties", out var propertiesProperty) && propertiesProperty.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesProperty.EnumerateObject())
            {
                var fieldType = Convert(property.Value, Capitalize(property.Name), warnings, $"{warningPath}.{property.Name}");
                fields[property.Name] = required.Contains(property.Name)
                    ? fieldType
                    : new FlowTypeJson { Kind = "optional", InnerType = fieldType };
            }
        }

        return new FlowTypeJson { Kind = "object", Name = typeName, Fields = fields };
    }

    private static FlowTypeJson ConvertArray(JsonElement schema, string typeName, List<string> warnings, string warningPath)
    {
        if (!schema.TryGetProperty("items", out var itemsProperty) || itemsProperty.ValueKind != JsonValueKind.Object)
        {
            warnings.Add($"{warningPath}: array schema is missing 'items'; using a String element placeholder.");
            return new FlowTypeJson { Kind = "list", ElementType = new FlowTypeJson { Kind = "primitive", Name = "String" } };
        }

        var elementType = Convert(itemsProperty, typeName, warnings, $"{warningPath}[]");
        return new FlowTypeJson { Kind = "list", ElementType = elementType };
    }

    private static FlowTypeJson Unsupported(List<string> warnings, string warningPath)
    {
        warnings.Add($"{warningPath}: unsupported schema shape (missing/non-string 'type', or a oneOf/anyOf/allOf/$ref schema); using a String placeholder.");
        return new FlowTypeJson { Kind = "primitive", Name = "String" };
    }

    private static string Capitalize(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
