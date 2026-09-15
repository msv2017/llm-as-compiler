using Flow.Cli;
using Flow.Contracts;
using Flow.TypeSystem;

namespace Flow.Cli.Tests;

public class ScenarioJsonTests
{
    // FlowType records (ObjectType/ListType/EnumType) hold Dictionary/List-typed fields, which don't
    // get structural equality from the record-generated Equals -- same reason TypeValidator.cs avoids
    // `==` for FlowType. Compare recursively by hand instead of Assert.Equal(FlowType, FlowType).
    private static bool FlowTypesEqual(FlowType expected, FlowType actual) => (expected, actual) switch
    {
        (PrimitiveType e, PrimitiveType a) => e.Kind == a.Kind,
        (ObjectType e, ObjectType a) => e.Name == a.Name && e.Fields.Count == a.Fields.Count &&
            e.Fields.All(kv => a.Fields.TryGetValue(kv.Key, out var av) && FlowTypesEqual(kv.Value, av)),
        (ListType e, ListType a) => FlowTypesEqual(e.ElementType, a.ElementType),
        (OptionalType e, OptionalType a) => FlowTypesEqual(e.InnerType, a.InnerType),
        (SemanticType e, SemanticType a) => e.Name == a.Name && FlowTypesEqual(e.Underlying, a.Underlying),
        (EnumType e, EnumType a) => e.Name == a.Name && e.Values.SequenceEqual(a.Values),
        _ => false
    };

    private const string ValidScenario = """
    {
      "prompt": "Given a customer id, look up and return the customer's name.",
      "inputType": { "kind": "object", "name": "Request", "fields": { "customerId": { "kind": "primitive", "name": "String" } } },
      "outputType": { "kind": "object", "name": "Result", "fields": { "customerName": { "kind": "primitive", "name": "String" } } },
      "tools": [
        {
          "name": "crm.getCustomerById",
          "effect": "Read",
          "retry": "Safe",
          "inputType": { "kind": "object", "name": "In", "fields": { "id": { "kind": "primitive", "name": "String" } } },
          "outputType": { "kind": "object", "name": "Customer", "fields": { "name": { "kind": "primitive", "name": "String" } } }
        }
      ]
    }
    """;

    [Fact]
    public void ValidScenario_ParsesPromptInputOutputAndTool()
    {
        var (source, tools) = ScenarioJson.Parse(ValidScenario);

        Assert.Equal("Given a customer id, look up and return the customer's name.", source.Prompt);
        Assert.True(FlowTypesEqual(new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String }), source.InputType));
        Assert.True(FlowTypesEqual(new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String }), source.OutputType));

        Assert.True(tools.TryGet("crm.getCustomerById", out var tool));
        Assert.Equal(ToolEffect.Read, tool!.Effect);
        Assert.Equal(ToolRetryPolicy.Safe, tool.Retry);
        Assert.True(FlowTypesEqual(new ObjectType("In", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }), tool.InputType));
        Assert.True(FlowTypesEqual(new ObjectType("Customer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String }), tool.OutputType));
    }

    [Fact]
    public void ListOptionalAndSemanticKinds_ParseCorrectly()
    {
        const string json = """
        {
          "prompt": "p",
          "inputType": {
            "kind": "object", "name": "Request",
            "fields": {
              "invoiceIds": { "kind": "list", "elementType": { "kind": "semantic", "name": "InvoiceId", "underlying": { "kind": "primitive", "name": "String" } } },
              "note": { "kind": "optional", "innerType": { "kind": "primitive", "name": "String" } }
            }
          },
          "outputType": { "kind": "primitive", "name": "Bool" },
          "tools": []
        }
        """;

        var (source, _) = ScenarioJson.Parse(json);

        var expectedInput = new ObjectType("Request", new Dictionary<string, FlowType>
        {
            ["invoiceIds"] = new ListType(new SemanticType("InvoiceId", PrimitiveType.String)),
            ["note"] = new OptionalType(PrimitiveType.String)
        });
        Assert.True(FlowTypesEqual(expectedInput, source.InputType));
        Assert.True(FlowTypesEqual(PrimitiveType.Bool, source.OutputType));
    }

    [Fact]
    public void EnumKind_ParsesNameAndValues()
    {
        const string json = """
        {
          "prompt": "p",
          "inputType": { "kind": "enum", "name": "Status", "values": ["Open", "Closed"] },
          "outputType": { "kind": "primitive", "name": "Bool" },
          "tools": []
        }
        """;

        var (source, _) = ScenarioJson.Parse(json);

        Assert.True(FlowTypesEqual(new EnumType("Status", new[] { "Open", "Closed" }), source.InputType));
    }

    [Fact]
    public void MalformedJson_ThrowsScenarioParseException_NotRawJsonException()
    {
        var ex = Assert.Throws<ScenarioParseException>(() => ScenarioJson.Parse("{ not valid json"));
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public void MissingPrompt_ThrowsScenarioParseException()
    {
        const string json = """
        { "inputType": { "kind": "primitive", "name": "String" }, "outputType": { "kind": "primitive", "name": "String" }, "tools": [] }
        """;

        var ex = Assert.Throws<ScenarioParseException>(() => ScenarioJson.Parse(json));
        Assert.Contains("prompt", ex.Message);
    }

    [Fact]
    public void UnknownFlowTypeKind_ThrowsScenarioParseException_NamingTheBadPath()
    {
        const string json = """
        { "prompt": "p", "inputType": { "kind": "bogus" }, "outputType": { "kind": "primitive", "name": "String" }, "tools": [] }
        """;

        var ex = Assert.Throws<ScenarioParseException>(() => ScenarioJson.Parse(json));
        Assert.Contains("inputType", ex.Message);
        Assert.Contains("bogus", ex.Message);
    }

    [Fact]
    public void UnknownToolEffect_ThrowsScenarioParseException()
    {
        const string json = """
        {
          "prompt": "p",
          "inputType": { "kind": "primitive", "name": "String" },
          "outputType": { "kind": "primitive", "name": "String" },
          "tools": [
            {
              "name": "t",
              "effect": "Sideways",
              "retry": "Safe",
              "inputType": { "kind": "object", "name": "In", "fields": {} },
              "outputType": { "kind": "primitive", "name": "String" }
            }
          ]
        }
        """;

        var ex = Assert.Throws<ScenarioParseException>(() => ScenarioJson.Parse(json));
        Assert.Contains("effect", ex.Message);
        Assert.Contains("Sideways", ex.Message);
    }

    [Fact]
    public void ToolInputTypeNotObject_ThrowsScenarioParseException()
    {
        const string json = """
        {
          "prompt": "p",
          "inputType": { "kind": "primitive", "name": "String" },
          "outputType": { "kind": "primitive", "name": "String" },
          "tools": [
            {
              "name": "t",
              "effect": "Read",
              "retry": "Safe",
              "inputType": { "kind": "primitive", "name": "String" },
              "outputType": { "kind": "primitive", "name": "String" }
            }
          ]
        }
        """;

        var ex = Assert.Throws<ScenarioParseException>(() => ScenarioJson.Parse(json));
        Assert.Contains("inputType", ex.Message);
        Assert.Contains("object", ex.Message);
    }

    [Fact]
    public void FromFlowType_ThenToFlowType_RoundTrips_EveryKind()
    {
        var type = new ObjectType("Request", new Dictionary<string, FlowType>
        {
            ["id"] = new SemanticType("CustomerId", PrimitiveType.String),
            ["tags"] = new ListType(PrimitiveType.String),
            ["note"] = new OptionalType(PrimitiveType.String),
            ["status"] = new EnumType("Status", new[] { "Open", "Closed" })
        });

        var json = ScenarioJson.FromFlowType(type);
        var roundTripped = ScenarioJson.ToFlowType(json, "test");

        Assert.True(FlowTypesEqual(type, roundTripped));
    }
}
