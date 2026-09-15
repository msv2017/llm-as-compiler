using Flow.Cli;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Cli.Tests;

public class CompiledWorkflowJsonTests
{
    private static WorkflowDefinition Fixture()
    {
        var call = new CallNode("customer", "crm.getCustomerById",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["customerName"] = new PathExpression("customer.name") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });
        return new WorkflowDefinition("FindCustomerName", inputType, outputType, new WorkflowNode[] { call }, returnNode);
    }

    [Fact]
    public void Write_ThenParse_RoundTripsNodesAndTypes()
    {
        var workflow = Fixture();

        var json = CompiledWorkflowJson.Write(workflow);
        var parsed = CompiledWorkflowJson.Parse(json);

        Assert.Equal("FindCustomerName", parsed.Name);
        var call = Assert.IsType<CallNode>(Assert.Single(parsed.Nodes));
        Assert.Equal("crm.getCustomerById", call.ToolName);
        var arg = Assert.IsType<PathExpression>(call.Arguments["id"]);
        Assert.Equal("input.customerId", arg.Path);
        var returnExpr = Assert.IsType<PathExpression>(parsed.Return.Fields["customerName"]);
        Assert.Equal("customer.name", returnExpr.Path);
        Assert.IsType<ObjectType>(parsed.InputType);
        Assert.Equal("Request", ((ObjectType)parsed.InputType).Name);
        Assert.IsType<ObjectType>(parsed.OutputType);
        Assert.Equal("Result", ((ObjectType)parsed.OutputType).Name);
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsScenarioParseException()
    {
        Assert.Throws<ScenarioParseException>(() => CompiledWorkflowJson.Parse("{ not valid json"));
    }

    [Fact]
    public void Parse_MissingWorkflow_ThrowsScenarioParseException()
    {
        var json = """{ "inputType": { "kind": "primitive", "name": "String" }, "outputType": { "kind": "primitive", "name": "String" } }""";

        var ex = Assert.Throws<ScenarioParseException>(() => CompiledWorkflowJson.Parse(json));
        Assert.Contains("workflow", ex.Message);
    }

    [Fact]
    public void Parse_InvalidSortDirection_ThrowsScenarioParseException()
    {
        var json = """
        {
          "inputType": { "kind": "primitive", "name": "String" },
          "outputType": { "kind": "primitive", "name": "String" },
          "workflow": {
            "name": "BadSort",
            "nodes": [
              {
                "kind": "sort",
                "id": "sorted",
                "source": { "kind": "path", "path": "input" },
                "parameterName": "item",
                "key": { "kind": "path", "path": "item" },
                "direction": "ASC"
              }
            ],
            "return": { "value": { "kind": "path", "path": "sorted" } }
          }
        }
        """;

        var ex = Assert.Throws<ScenarioParseException>(() => CompiledWorkflowJson.Parse(json));
        Assert.Contains("invalid workflow", ex.Message);
    }

    [Fact]
    public void Parse_SortNodeMissingDirection_ThrowsScenarioParseException()
    {
        var json = """
        {
          "inputType": { "kind": "primitive", "name": "String" },
          "outputType": { "kind": "primitive", "name": "String" },
          "workflow": {
            "name": "MissingDirection",
            "nodes": [
              {
                "kind": "sort",
                "id": "sorted",
                "source": { "kind": "path", "path": "input" },
                "parameterName": "item",
                "key": { "kind": "path", "path": "item" }
              }
            ],
            "return": { "value": { "kind": "path", "path": "sorted" } }
          }
        }
        """;

        var ex = Assert.Throws<ScenarioParseException>(() => CompiledWorkflowJson.Parse(json));
        Assert.Contains("invalid workflow", ex.Message);
    }

    [Fact]
    public void Parse_FilterNodeMissingPredicate_ThrowsScenarioParseException()
    {
        var json = """
        {
          "inputType": { "kind": "primitive", "name": "String" },
          "outputType": { "kind": "primitive", "name": "String" },
          "workflow": {
            "name": "MissingPredicate",
            "nodes": [
              {
                "kind": "filter",
                "id": "filtered",
                "source": { "kind": "path", "path": "input" },
                "parameterName": "item"
              }
            ],
            "return": { "value": { "kind": "path", "path": "filtered" } }
          }
        }
        """;

        var ex = Assert.Throws<ScenarioParseException>(() => CompiledWorkflowJson.Parse(json));
        Assert.Contains("invalid workflow", ex.Message);
    }

    [Fact]
    public void Write_ThenParse_RoundTripsFilterSortAggregateAssertAndForeachNodes()
    {
        var filter = new FilterNode("openInvoices", new PathExpression("input.invoices"), "invoice",
            new BinaryExpression(new PathExpression("invoice.status"), BinaryOperator.Equal, new ConstantExpression("OPEN", new ConstantProvenance(ConstantProvenanceKind.Prompt))));
        var sort = new SortNode("sorted", new PathExpression("openInvoices"), "invoice", new PathExpression("invoice.dueDate"), SortDirection.Ascending);
        var aggregate = new AggregateNode("oldest", new PathExpression("sorted"), AggregateOperation.First, null, null);
        var assert = new AssertNode("guard", new BinaryExpression(new PathExpression("oldest"), BinaryOperator.NotEqual, new ConstantExpression(null, new ConstantProvenance(ConstantProvenanceKind.Unproven))), "NO_OPEN_INVOICES");
        var foreachNode = new ForeachNode("ids", new PathExpression("input.invoices"), "invoice", 100,
            Array.Empty<WorkflowNode>(), new PathExpression("invoice.id"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ids"] = new PathExpression("ids") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType>());
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType>());
        var workflow = new WorkflowDefinition("RefundOldestInvoice", inputType, outputType,
            new WorkflowNode[] { filter, sort, aggregate, assert, foreachNode }, returnNode);

        var json = CompiledWorkflowJson.Write(workflow);
        var parsed = CompiledWorkflowJson.Parse(json);

        Assert.Equal(5, parsed.Nodes.Count);
        var parsedFilter = Assert.IsType<FilterNode>(parsed.Nodes[0]);
        Assert.Equal("invoice", parsedFilter.ParameterName);
        var parsedSort = Assert.IsType<SortNode>(parsed.Nodes[1]);
        Assert.Equal(SortDirection.Ascending, parsedSort.Direction);
        var parsedAggregate = Assert.IsType<AggregateNode>(parsed.Nodes[2]);
        Assert.Equal(AggregateOperation.First, parsedAggregate.Operation);
        Assert.Null(parsedAggregate.Selector);
        var parsedAssert = Assert.IsType<AssertNode>(parsed.Nodes[3]);
        Assert.Equal("NO_OPEN_INVOICES", parsedAssert.FailureCode);
        var parsedForeach = Assert.IsType<ForeachNode>(parsed.Nodes[4]);
        Assert.Equal(100, parsedForeach.Limit);
    }
}
