using System.Text.Json;
using Flow.Compiler.Candidate;
using Xunit;

namespace Flow.Compiler.Tests;

public class CandidateWorkflowAstJsonTests
{
    // C# record properties are PascalCase; the wire format is camelCase (matches source doc §9's example).
    // CandidateJson.Options carries that convention everywhere this plan serializes/deserializes a candidate.
    private static readonly JsonSerializerOptions Options = CandidateJson.Options;

    [Fact]
    public void DeserializesDesignDocShapedCandidate_IntoTypedAst()
    {
        var json = """
        {
          "workflow": {
            "name": "RefundOldestInvoice",
            "nodes": [
              {
                "kind": "call",
                "id": "customer",
                "tool": "crm.findCustomer",
                "arguments": {
                  "email": { "kind": "path", "path": "input.email" }
                }
              }
            ],
            "return": {
              "customerName": { "kind": "path", "path": "customer.name" }
            }
          },
          "interpretations": [],
          "assumptions": [],
          "unresolved": []
        }
        """;

        var ast = JsonSerializer.Deserialize<CandidateWorkflowAst>(json, Options)!;

        Assert.Equal("RefundOldestInvoice", ast.Workflow.Name);
        var call = Assert.IsType<CandidateCallNode>(Assert.Single(ast.Workflow.Nodes));
        Assert.Equal("customer", call.Id);
        Assert.Equal("crm.findCustomer", call.Tool);
        var emailArg = Assert.IsType<CandidatePathExpression>(call.Arguments["email"]);
        Assert.Equal("input.email", emailArg.Path);
        var returnExpr = Assert.IsType<CandidatePathExpression>(ast.Workflow.Return["customerName"]);
        Assert.Equal("customer.name", returnExpr.Path);
        Assert.Empty(ast.Interpretations);
        Assert.Empty(ast.Assumptions);
        Assert.Empty(ast.Unresolved);
    }

    [Fact]
    public void RoundTrips_ConstantWithProvenanceSource()
    {
        var constant = new CandidateConstantExpression(30, "PROMPT", "\"30 days\" in prompt");
        var json = JsonSerializer.Serialize<CandidateExpression>(constant, Options);
        var back = JsonSerializer.Deserialize<CandidateExpression>(json, Options);

        var typed = Assert.IsType<CandidateConstantExpression>(back);
        Assert.Equal("PROMPT", typed.Source);
        Assert.Equal("\"30 days\" in prompt", typed.Detail);
    }

    [Fact]
    public void RoundTrips_BinaryAndObjectExpressions()
    {
        CandidateExpression binary = new CandidateBinaryExpression(
            new CandidatePathExpression("total"), "LessThan", new CandidateConstantExpression(500, "PROMPT"));
        var json = JsonSerializer.Serialize(binary, Options);
        var back = Assert.IsType<CandidateBinaryExpression>(JsonSerializer.Deserialize<CandidateExpression>(json, Options));
        Assert.Equal("LessThan", back.Operator);

        CandidateExpression obj = new CandidateObjectExpression(new Dictionary<string, CandidateExpression>
        {
            ["outstanding"] = new CandidatePathExpression("total")
        });
        var objJson = JsonSerializer.Serialize(obj, Options);
        var objBack = Assert.IsType<CandidateObjectExpression>(JsonSerializer.Deserialize<CandidateExpression>(objJson, Options));
        Assert.IsType<CandidatePathExpression>(objBack.Fields["outstanding"]);
    }
}
