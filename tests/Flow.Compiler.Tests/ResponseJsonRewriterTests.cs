using System.Text.Json;
using Flow.Compiler.Providers;
using Xunit;

namespace Flow.Compiler.Tests;

public class ResponseJsonRewriterTests
{
    [Fact]
    public void RewritesCallNodeArguments_FromArrayOfPairs_ToObject()
    {
        var raw = """
        {
          "workflow": {
            "name": "W",
            "nodes": [
              {
                "kind": "call", "id": "customer", "tool": "crm.getCustomerById",
                "arguments": [ { "name": "id", "value": { "kind": "path", "path": "input.customerId" } } ]
              }
            ],
            "return": [ { "name": "customerName", "value": { "kind": "path", "path": "customer.name" } } ]
          },
          "interpretations": [], "assumptions": [], "unresolved": []
        }
        """;

        var rewritten = ResponseJsonRewriter.RewriteDictionaryShapedFields(raw);
        using var document = JsonDocument.Parse(rewritten);
        var root = document.RootElement;

        var callNode = root.GetProperty("workflow").GetProperty("nodes")[0];
        var argumentsObject = callNode.GetProperty("arguments");
        Assert.Equal(JsonValueKind.Object, argumentsObject.ValueKind);
        Assert.Equal("input.customerId", argumentsObject.GetProperty("id").GetProperty("path").GetString());

        var returnObject = root.GetProperty("workflow").GetProperty("return");
        Assert.Equal(JsonValueKind.Object, returnObject.ValueKind);
        Assert.Equal("customer.name", returnObject.GetProperty("customerName").GetProperty("path").GetString());
    }

    [Fact]
    public void RewritesNestedObjectExpressionFields_Recursively()
    {
        var raw = """
        {
          "workflow": {
            "name": "W",
            "nodes": [
              {
                "kind": "if", "id": "decision",
                "condition": { "kind": "path", "path": "input.ok" },
                "trueBranch": {
                  "nodes": [],
                  "value": {
                    "kind": "object",
                    "fields": [ { "name": "flag", "value": { "kind": "constant", "value": true, "source": "PROMPT", "detail": null } } ]
                  }
                },
                "falseBranch": { "nodes": [], "value": { "kind": "path", "path": "input.ok" } }
              }
            ],
            "return": [ { "name": "flag", "value": { "kind": "path", "path": "decision.flag" } } ]
          },
          "interpretations": [], "assumptions": [], "unresolved": []
        }
        """;

        var rewritten = ResponseJsonRewriter.RewriteDictionaryShapedFields(raw);
        using var document = JsonDocument.Parse(rewritten);
        var ifNode = document.RootElement.GetProperty("workflow").GetProperty("nodes")[0];
        var fieldsObject = ifNode.GetProperty("trueBranch").GetProperty("value").GetProperty("fields");

        Assert.Equal(JsonValueKind.Object, fieldsObject.ValueKind);
        Assert.True(fieldsObject.GetProperty("flag").GetProperty("value").GetBoolean());
    }

    [Fact]
    public void EmptyArrayOfPairs_RewritesToEmptyObject()
    {
        var raw = """
        {
          "workflow": { "name": "W", "nodes": [], "return": [] },
          "interpretations": [], "assumptions": [], "unresolved": []
        }
        """;

        var rewritten = ResponseJsonRewriter.RewriteDictionaryShapedFields(raw);
        using var document = JsonDocument.Parse(rewritten);
        var returnObject = document.RootElement.GetProperty("workflow").GetProperty("return");

        Assert.Equal(JsonValueKind.Object, returnObject.ValueKind);
        Assert.Empty(returnObject.EnumerateObject());
    }

    [Fact]
    public void DuplicateKeyInPairsArray_ThrowsJsonException()
    {
        var raw = """
        {
          "workflow": {
            "name": "W",
            "nodes": [
              {
                "kind": "call", "id": "customer", "tool": "crm.getCustomerById",
                "arguments": [
                  { "name": "id", "value": { "kind": "path", "path": "input.customerId" } },
                  { "name": "id", "value": { "kind": "path", "path": "input.otherId" } }
                ]
              }
            ],
            "return": []
          },
          "interpretations": [], "assumptions": [], "unresolved": []
        }
        """;

        Assert.Throws<JsonException>(() => ResponseJsonRewriter.RewriteDictionaryShapedFields(raw));
    }
}
