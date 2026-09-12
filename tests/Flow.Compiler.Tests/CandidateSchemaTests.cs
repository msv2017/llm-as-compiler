using System.Text.Json;
using Flow.Compiler.Providers;
using Xunit;

namespace Flow.Compiler.Tests;

public class CandidateSchemaTests
{
    [Fact]
    public void Json_IsValidJson_WithExpectedTopLevelShape()
    {
        using var document = JsonDocument.Parse(CandidateSchema.Json);
        var root = document.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.True(root.GetProperty("properties").TryGetProperty("workflow", out _));
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());

        var required = root.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("workflow", required);
        Assert.Contains("interpretations", required);
        Assert.Contains("assumptions", required);
        Assert.Contains("unresolved", required);
    }

    [Fact]
    public void Json_EveryNodeDefinition_DeclaresKindFirst()
    {
        using var document = JsonDocument.Parse(CandidateSchema.Json);
        var defs = document.RootElement.GetProperty("$defs");

        foreach (var defName in new[] { "callNode", "ifNode", "filterNode", "sortNode", "aggregateNode", "foreachNode", "assertNode" })
        {
            var properties = defs.GetProperty(defName).GetProperty("properties");
            var firstPropertyName = properties.EnumerateObject().First().Name;
            Assert.Equal("kind", firstPropertyName);
        }
    }

    [Fact]
    public void Json_EveryExpressionDefinition_DeclaresKindFirst()
    {
        using var document = JsonDocument.Parse(CandidateSchema.Json);
        var defs = document.RootElement.GetProperty("$defs");

        foreach (var defName in new[] { "pathExpression", "constantExpression", "binaryExpression", "objectExpression" })
        {
            var properties = defs.GetProperty(defName).GetProperty("properties");
            var firstPropertyName = properties.EnumerateObject().First().Name;
            Assert.Equal("kind", firstPropertyName);
        }
    }

    [Fact]
    public void Json_DictionaryShapedFields_AreArraysOfNamedExpressionPairs()
    {
        using var document = JsonDocument.Parse(CandidateSchema.Json);
        var defs = document.RootElement.GetProperty("$defs");

        var callArguments = defs.GetProperty("callNode").GetProperty("properties").GetProperty("arguments");
        Assert.Equal("array", callArguments.GetProperty("type").GetString());
        Assert.Equal("#/$defs/namedExpression", callArguments.GetProperty("items").GetProperty("$ref").GetString());

        var workflowReturn = defs.GetProperty("workflowBody").GetProperty("properties").GetProperty("return");
        Assert.Equal("array", workflowReturn.GetProperty("type").GetString());

        var objectFields = defs.GetProperty("objectExpression").GetProperty("properties").GetProperty("fields");
        Assert.Equal("array", objectFields.GetProperty("type").GetString());
    }
}
