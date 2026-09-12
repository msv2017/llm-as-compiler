using System.Text.Json;
using Flow.Compiler.Candidate;
using Flow.Compiler.Providers;
using Xunit;

namespace Flow.Compiler.Tests;

public class CandidateSchemaRoundTripTests
{
    [Fact]
    public void AllNodeAndExpressionKinds_SurviveRewriteAndDeserialize()
    {
        var raw = """
        {
          "workflow": {
            "name": "AllKinds",
            "nodes": [
              { "kind": "call", "id": "customer", "tool": "crm.getCustomerById",
                "arguments": [ { "name": "id", "value": { "kind": "path", "path": "input.customerId" } } ] },
              { "kind": "if", "id": "decision",
                "condition": { "kind": "path", "path": "customer" },
                "trueBranch": { "nodes": [], "value": { "kind": "constant", "value": true, "source": "PROMPT", "detail": null } },
                "falseBranch": { "nodes": [], "value": { "kind": "constant", "value": false, "source": "PROMPT", "detail": null } } },
              { "kind": "filter", "id": "filtered", "source": { "kind": "path", "path": "input.items" },
                "parameterName": "x", "predicate": { "kind": "path", "path": "x.active" } },
              { "kind": "sort", "id": "sorted", "source": { "kind": "path", "path": "filtered" },
                "parameterName": "x", "key": { "kind": "path", "path": "x.createdAt" }, "direction": "Ascending" },
              { "kind": "aggregate", "id": "total", "source": { "kind": "path", "path": "sorted" },
                "operation": "Sum", "parameterName": "x", "selector": { "kind": "path", "path": "x.amount" } },
              { "kind": "foreach", "id": "ids", "source": { "kind": "path", "path": "sorted" },
                "parameterName": "x", "limit": 10, "body": [],
                "bodyValue": {
                  "kind": "object",
                  "fields": [ { "name": "id", "value": { "kind": "path", "path": "x.id" } } ]
                } },
              { "kind": "assert", "id": "guard",
                "condition": { "kind": "binary", "operator": "NotEqual",
                  "left": { "kind": "path", "path": "customer" },
                  "right": { "kind": "constant", "value": null, "source": "PROMPT", "detail": null } },
                "failureCode": "NOT_FOUND" }
            ],
            "return": [
              { "name": "customerName", "value": { "kind": "path", "path": "customer.name" } },
              { "name": "total", "value": { "kind": "path", "path": "total" } }
            ]
          },
          "interpretations": [], "assumptions": [], "unresolved": []
        }
        """;

        var rewritten = ResponseJsonRewriter.RewriteDictionaryShapedFields(raw);
        var candidate = JsonSerializer.Deserialize<CandidateWorkflowAst>(rewritten, CandidateJson.Options);

        Assert.NotNull(candidate);
        Assert.Equal(7, candidate!.Workflow.Nodes.Count);
        Assert.IsType<CandidateCallNode>(candidate.Workflow.Nodes[0]);
        var ifNode = Assert.IsType<CandidateIfNode>(candidate.Workflow.Nodes[1]);
        Assert.IsType<CandidateConstantExpression>(ifNode.TrueBranch.Value);
        Assert.IsType<CandidateFilterNode>(candidate.Workflow.Nodes[2]);
        Assert.IsType<CandidateSortNode>(candidate.Workflow.Nodes[3]);
        Assert.IsType<CandidateAggregateNode>(candidate.Workflow.Nodes[4]);
        var foreachNode = Assert.IsType<CandidateForeachNode>(candidate.Workflow.Nodes[5]);
        var bodyValue = Assert.IsType<CandidateObjectExpression>(foreachNode.BodyValue);
        Assert.IsType<CandidatePathExpression>(bodyValue.Fields["id"]);
        var assertNode = Assert.IsType<CandidateAssertNode>(candidate.Workflow.Nodes[6]);
        var condition = Assert.IsType<CandidateBinaryExpression>(assertNode.Condition);
        Assert.IsType<CandidateConstantExpression>(condition.Right);

        Assert.Equal(2, candidate.Workflow.Return.Count);
    }
}
