using System.Text.Json;
using Flow.Compiler;
using Flow.Contracts;
using Flow.TypeSystem;

namespace Flow.Cli;

public sealed class ScenarioParseException : Exception
{
    public ScenarioParseException(string message) : base(message)
    {
    }
}

internal sealed class FlowTypeJson
{
    public string Kind { get; set; } = "";
    public string? Name { get; set; }
    public Dictionary<string, FlowTypeJson>? Fields { get; set; }
    public FlowTypeJson? ElementType { get; set; }
    public FlowTypeJson? InnerType { get; set; }
    public FlowTypeJson? Underlying { get; set; }
    public List<string>? Values { get; set; }
}

internal sealed class ToolJson
{
    public string Name { get; set; } = "";
    public string Effect { get; set; } = "";
    public string Retry { get; set; } = "";
    public FlowTypeJson? InputType { get; set; }
    public FlowTypeJson? OutputType { get; set; }
}

internal sealed class ScenarioFileJson
{
    public string? Prompt { get; set; }
    public FlowTypeJson? InputType { get; set; }
    public FlowTypeJson? OutputType { get; set; }
    public List<ToolJson> Tools { get; set; } = new();
}

public static class ScenarioJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static (WorkflowSource Source, ToolCatalog Tools) Parse(string json)
    {
        ScenarioFileJson? scenario;
        try
        {
            scenario = JsonSerializer.Deserialize<ScenarioFileJson>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ScenarioParseException($"Scenario file is not valid JSON: {ex.Message}");
        }

        if (scenario is null)
            throw new ScenarioParseException("Scenario file is empty.");
        if (string.IsNullOrWhiteSpace(scenario.Prompt))
            throw new ScenarioParseException("Scenario file is missing a non-empty 'prompt'.");

        var inputType = ToFlowType(scenario.InputType, "inputType");
        var outputType = ToFlowType(scenario.OutputType, "outputType");
        var source = new WorkflowSource(scenario.Prompt, inputType, outputType);

        var tools = scenario.Tools.Select(ToToolDefinition).ToList();
        return (source, new ToolCatalog(tools));
    }

    private static ToolDefinition ToToolDefinition(ToolJson tool)
    {
        if (string.IsNullOrWhiteSpace(tool.Name))
            throw new ScenarioParseException("A tool entry is missing a non-empty 'name'.");

        var inputType = ToFlowType(tool.InputType, $"tools[{tool.Name}].inputType");
        if (inputType is not ObjectType inputObjectType)
            throw new ScenarioParseException(
                $"tools[{tool.Name}].inputType must have kind 'object', got '{tool.InputType?.Kind}'.");

        var outputType = ToFlowType(tool.OutputType, $"tools[{tool.Name}].outputType");
        var effect = ParseEnum<ToolEffect>(tool.Effect, $"tools[{tool.Name}].effect");
        var retry = ParseEnum<ToolRetryPolicy>(tool.Retry, $"tools[{tool.Name}].retry");

        return new ToolDefinition(tool.Name, inputObjectType, outputType, effect, retry);
    }

    private static FlowType ToFlowType(FlowTypeJson? json, string path)
    {
        if (json is null)
            throw new ScenarioParseException($"{path} is missing.");

        return json.Kind switch
        {
            "primitive" => ParsePrimitive(json, path),
            "object" => ParseObject(json, path),
            "list" => new ListType(ToFlowType(json.ElementType, $"{path}.elementType")),
            "optional" => new OptionalType(ToFlowType(json.InnerType, $"{path}.innerType")),
            "semantic" => ParseSemantic(json, path),
            "enum" => ParseEnumType(json, path),
            _ => throw new ScenarioParseException(
                $"{path} has unknown kind '{json.Kind}'. Expected one of: primitive, object, list, optional, semantic, enum.")
        };
    }

    private static FlowType ParsePrimitive(FlowTypeJson json, string path)
    {
        if (string.IsNullOrWhiteSpace(json.Name))
            throw new ScenarioParseException($"{path} (kind 'primitive') is missing 'name'.");
        if (!Enum.TryParse<PrimitiveKind>(json.Name, ignoreCase: false, out var kind))
            throw new ScenarioParseException(
                $"{path}.name '{json.Name}' is not a valid primitive kind. Expected one of: {string.Join(", ", Enum.GetNames<PrimitiveKind>())}.");
        return new PrimitiveType(kind);
    }

    private static FlowType ParseObject(FlowTypeJson json, string path)
    {
        if (string.IsNullOrWhiteSpace(json.Name))
            throw new ScenarioParseException($"{path} (kind 'object') is missing 'name'.");
        if (json.Fields is null)
            throw new ScenarioParseException($"{path} (kind 'object') is missing 'fields'.");

        var fields = new Dictionary<string, FlowType>();
        foreach (var (fieldName, fieldJson) in json.Fields)
            fields[fieldName] = ToFlowType(fieldJson, $"{path}.fields.{fieldName}");
        return new ObjectType(json.Name, fields);
    }

    private static FlowType ParseSemantic(FlowTypeJson json, string path)
    {
        if (string.IsNullOrWhiteSpace(json.Name))
            throw new ScenarioParseException($"{path} (kind 'semantic') is missing 'name'.");
        return new SemanticType(json.Name, ToFlowType(json.Underlying, $"{path}.underlying"));
    }

    private static FlowType ParseEnumType(FlowTypeJson json, string path)
    {
        if (string.IsNullOrWhiteSpace(json.Name))
            throw new ScenarioParseException($"{path} (kind 'enum') is missing 'name'.");
        if (json.Values is null || json.Values.Count == 0)
            throw new ScenarioParseException($"{path} (kind 'enum') is missing non-empty 'values'.");
        return new EnumType(json.Name, json.Values);
    }

    private static TEnum ParseEnum<TEnum>(string value, string path) where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: false, out var result))
            throw new ScenarioParseException(
                $"{path} '{value}' is not valid. Expected one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        return result;
    }
}
