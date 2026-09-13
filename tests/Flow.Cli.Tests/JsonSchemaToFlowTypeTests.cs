using System.Text.Json;
using Flow.Cli;
using Flow.Cli.Scaffold;

namespace Flow.Cli.Tests;

public class JsonSchemaToFlowTypeTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void StringType_ConvertsToPrimitiveString()
    {
        var schema = Parse("""{ "type": "string" }""");
        var warnings = new List<string>();

        var result = JsonSchemaToFlowType.Convert(schema, "Field", warnings, "field");

        Assert.Equal("primitive", result.Kind);
        Assert.Equal("String", result.Name);
        Assert.Empty(warnings);
    }

    [Fact]
    public void IntegerType_ConvertsToPrimitiveInt()
    {
        var schema = Parse("""{ "type": "integer" }""");

        var result = JsonSchemaToFlowType.Convert(schema, "Field", new List<string>(), "field");

        Assert.Equal("primitive", result.Kind);
        Assert.Equal("Int", result.Name);
    }

    [Fact]
    public void NumberType_ConvertsToPrimitiveDecimal()
    {
        var schema = Parse("""{ "type": "number" }""");

        var result = JsonSchemaToFlowType.Convert(schema, "Field", new List<string>(), "field");

        Assert.Equal("primitive", result.Kind);
        Assert.Equal("Decimal", result.Name);
    }

    [Fact]
    public void BooleanType_ConvertsToPrimitiveBool()
    {
        var schema = Parse("""{ "type": "boolean" }""");

        var result = JsonSchemaToFlowType.Convert(schema, "Field", new List<string>(), "field");

        Assert.Equal("primitive", result.Kind);
        Assert.Equal("Bool", result.Name);
    }

    [Fact]
    public void ObjectType_ConvertsFieldsAndWrapsNonRequiredInOptional()
    {
        var schema = Parse("""
        {
          "type": "object",
          "properties": {
            "id": { "type": "string" },
            "nickname": { "type": "string" }
          },
          "required": ["id"]
        }
        """);

        var result = JsonSchemaToFlowType.Convert(schema, "Customer", new List<string>(), "customer");

        Assert.Equal("object", result.Kind);
        Assert.Equal("Customer", result.Name);
        Assert.Equal("primitive", result.Fields!["id"].Kind);
        Assert.Equal("optional", result.Fields["nickname"].Kind);
        Assert.Equal("primitive", result.Fields["nickname"].InnerType!.Kind);
        Assert.Equal("String", result.Fields["nickname"].InnerType!.Name);
    }

    [Fact]
    public void ArrayType_ConvertsToListWithElementType()
    {
        var schema = Parse("""{ "type": "array", "items": { "type": "string" } }""");

        var result = JsonSchemaToFlowType.Convert(schema, "Tags", new List<string>(), "tags");

        Assert.Equal("list", result.Kind);
        Assert.Equal("primitive", result.ElementType!.Kind);
        Assert.Equal("String", result.ElementType!.Name);
    }

    [Fact]
    public void NestedObjectInArray_ConvertsRecursively()
    {
        var schema = Parse("""
        {
          "type": "array",
          "items": {
            "type": "object",
            "properties": { "id": { "type": "string" } },
            "required": ["id"]
          }
        }
        """);

        var result = JsonSchemaToFlowType.Convert(schema, "Items", new List<string>(), "items");

        Assert.Equal("list", result.Kind);
        Assert.Equal("object", result.ElementType!.Kind);
        Assert.Equal("primitive", result.ElementType!.Fields!["id"].Kind);
    }

    [Fact]
    public void StringEnum_ConvertsToEnumTypeWithValues()
    {
        var schema = Parse("""{ "type": "string", "enum": ["Open", "Closed"] }""");

        var result = JsonSchemaToFlowType.Convert(schema, "Status", new List<string>(), "status");

        Assert.Equal("enum", result.Kind);
        Assert.Equal("Status", result.Name);
        Assert.Equal(new[] { "Open", "Closed" }, result.Values);
    }

    [Fact]
    public void MissingType_FallsBackToStringPlaceholder_AndRecordsWarningWithPath()
    {
        var schema = Parse("""{ "oneOf": [ { "type": "string" }, { "type": "integer" } ] }""");
        var warnings = new List<string>();

        var result = JsonSchemaToFlowType.Convert(schema, "Field", warnings, "weird.field");

        Assert.Equal("primitive", result.Kind);
        Assert.Equal("String", result.Name);
        var warning = Assert.Single(warnings);
        Assert.Contains("weird.field", warning);
    }

    [Fact]
    public void RefSchema_FallsBackToStringPlaceholder_AndRecordsWarning()
    {
        var schema = Parse("""{ "$ref": "#/$defs/something" }""");
        var warnings = new List<string>();

        JsonSchemaToFlowType.Convert(schema, "Field", warnings, "refField");

        Assert.Single(warnings);
    }

    [Fact]
    public void UnionTypeArray_FallsBackToStringPlaceholder_AndRecordsWarning()
    {
        var schema = Parse("""{ "type": ["string", "null"] }""");
        var warnings = new List<string>();

        var result = JsonSchemaToFlowType.Convert(schema, "Field", warnings, "nullableField");

        Assert.Equal("primitive", result.Kind);
        Assert.Single(warnings);
    }

    [Fact]
    public void ArrayMissingItems_FallsBackToStringElementPlaceholder_AndRecordsWarning()
    {
        var schema = Parse("""{ "type": "array" }""");
        var warnings = new List<string>();

        var result = JsonSchemaToFlowType.Convert(schema, "Field", warnings, "list");

        Assert.Equal("list", result.Kind);
        Assert.Equal("String", result.ElementType!.Name);
        Assert.Single(warnings);
    }
}
