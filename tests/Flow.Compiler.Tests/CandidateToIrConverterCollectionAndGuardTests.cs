using System.Text.Json;
using Flow.Compiler.Candidate;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Compiler.Tests;

public class CandidateToIrConverterCollectionAndGuardTests
{
    [Fact]
    public void ConvertsFilterSortAggregate()
    {
        var json = """
        {
          "workflow": {
            "name": "Aggregation",
            "nodes": [
              { "kind": "filter", "id": "unpaid", "source": { "kind": "path", "path": "input.invoices" },
                "parameterName": "x",
                "predicate": { "kind": "path", "path": "x.isUnpaid" } },
              { "kind": "sort", "id": "ordered", "source": { "kind": "path", "path": "unpaid" },
                "parameterName": "x", "key": { "kind": "path", "path": "x.createdAt" }, "direction": "Ascending" },
              { "kind": "aggregate", "id": "total", "source": { "kind": "path", "path": "ordered" },
                "operation": "Sum", "parameterName": "x", "selector": { "kind": "path", "path": "x.amount" } }
            ],
            "return": { "outstanding": { "kind": "path", "path": "total" } }
          },
          "interpretations": [], "assumptions": [], "unresolved": []
        }
        """;
        var ast = JsonSerializer.Deserialize<CandidateWorkflowAst>(json, CandidateJson.Options)!;
        var invoiceType = new ObjectType("Invoice", new Dictionary<string, FlowType>
        {
            ["isUnpaid"] = PrimitiveType.Bool, ["createdAt"] = PrimitiveType.DateTime, ["amount"] = PrimitiveType.Decimal
        });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["invoices"] = new ListType(invoiceType) });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["outstanding"] = PrimitiveType.Decimal });

        var workflow = CandidateToIrConverter.Convert(ast, inputType, outputType);

        Assert.IsType<FilterNode>(workflow.Nodes[0]);
        var sort = Assert.IsType<SortNode>(workflow.Nodes[1]);
        Assert.Equal(SortDirection.Ascending, sort.Direction);
        var aggregate = Assert.IsType<AggregateNode>(workflow.Nodes[2]);
        Assert.Equal(AggregateOperation.Sum, aggregate.Operation);
    }

    [Fact]
    public void ConvertsForeachAndAssert()
    {
        var json = """
        {
          "workflow": {
            "name": "Bounded",
            "nodes": [
              { "kind": "assert", "id": "guard",
                "condition": { "kind": "path", "path": "input.ok" }, "failureCode": "NOT_OK" },
              { "kind": "foreach", "id": "ids", "source": { "kind": "path", "path": "input.items" },
                "parameterName": "x", "limit": 10, "body": [],
                "bodyValue": { "kind": "path", "path": "x" } }
            ],
            "return": { "ids": { "kind": "path", "path": "ids" } }
          },
          "interpretations": [], "assumptions": [], "unresolved": []
        }
        """;
        var ast = JsonSerializer.Deserialize<CandidateWorkflowAst>(json, CandidateJson.Options)!;
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>
        {
            ["ok"] = PrimitiveType.Bool, ["items"] = new ListType(PrimitiveType.String)
        });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["ids"] = new ListType(PrimitiveType.String) });

        var workflow = CandidateToIrConverter.Convert(ast, inputType, outputType);

        Assert.IsType<AssertNode>(workflow.Nodes[0]);
        var foreachNode = Assert.IsType<ForeachNode>(workflow.Nodes[1]);
        Assert.Equal(10, foreachNode.Limit);
        Assert.Equal("x", foreachNode.ParameterName);
    }
}
