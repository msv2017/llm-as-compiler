using System.Text.Json;
using Flow.Compiler.Candidate;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Compiler.Tests;

public class CandidateToIrConverterIfBinaryObjectTests
{
    [Fact]
    public void ConvertsIfNode_WithBinaryConditionAndObjectBranchValues()
    {
        var json = """
        {
          "workflow": {
            "name": "Guarded",
            "nodes": [
              {
                "kind": "if",
                "id": "decision",
                "condition": {
                  "kind": "binary", "operator": "GreaterThan",
                  "left": { "kind": "path", "path": "input.total" },
                  "right": { "kind": "constant", "value": 0, "source": "PROMPT" }
                },
                "trueBranch": {
                  "nodes": [],
                  "value": { "kind": "object", "fields": { "flag": { "kind": "constant", "value": true, "source": "PROMPT" } } }
                },
                "falseBranch": {
                  "nodes": [],
                  "value": { "kind": "object", "fields": { "flag": { "kind": "constant", "value": false, "source": "PROMPT" } } }
                }
              }
            ],
            "return": { "flag": { "kind": "path", "path": "decision.flag" } }
          },
          "interpretations": [], "assumptions": [], "unresolved": []
        }
        """;
        var ast = JsonSerializer.Deserialize<CandidateWorkflowAst>(json, CandidateJson.Options)!;
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["total"] = PrimitiveType.Decimal });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["flag"] = PrimitiveType.Bool });

        var workflow = CandidateToIrConverter.Convert(ast, inputType, outputType);

        var ifNode = Assert.IsType<IfNode>(Assert.Single(workflow.Nodes));
        var condition = Assert.IsType<BinaryExpression>(ifNode.Condition);
        Assert.Equal(BinaryOperator.GreaterThan, condition.Operator);
        Assert.IsType<PathExpression>(condition.Left);
        Assert.IsType<ConstantExpression>(condition.Right);

        var trueValue = Assert.IsType<ObjectExpression>(ifNode.TrueBranch.Value);
        Assert.True((bool)((ConstantExpression)trueValue.Fields["flag"]).Value!);
        var falseValue = Assert.IsType<ObjectExpression>(ifNode.FalseBranch.Value);
        Assert.False((bool)((ConstantExpression)falseValue.Fields["flag"]).Value!);
    }
}
