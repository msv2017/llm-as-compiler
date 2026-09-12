using System.Text.Json;
using Flow.Compiler.Candidate;
using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.Runtime;
using Flow.TypeSystem;
using Flow.Validation;
using Xunit;

namespace Flow.Compiler.Tests;

public class CandidateToIrConverterCallReturnTests
{
    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition(
            "crm.getCustomerById",
            new ObjectType("In", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            new ObjectType("Customer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String }),
            ToolEffect.Read, ToolRetryPolicy.Safe)
    });

    [Fact]
    public void ConvertsJsonCandidate_ToExecutableWorkflow_MatchingSlice1Shape()
    {
        var json = """
        {
          "workflow": {
            "name": "FindCustomerName",
            "nodes": [
              {
                "kind": "call",
                "id": "customer",
                "tool": "crm.getCustomerById",
                "arguments": { "id": { "kind": "path", "path": "input.customerId" } }
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
        var ast = JsonSerializer.Deserialize<CandidateWorkflowAst>(json, CandidateJson.Options)!;

        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["customerId"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["customerName"] = PrimitiveType.String });

        var workflow = CandidateToIrConverter.Convert(ast, inputType, outputType);

        Assert.Equal("FindCustomerName", workflow.Name);
        var call = Assert.IsType<CallNode>(Assert.Single(workflow.Nodes));
        Assert.Equal("crm.getCustomerById", call.ToolName);
        var arg = Assert.IsType<PathExpression>(call.Arguments["id"]);
        Assert.Equal("input.customerId", arg.Path);

        var diagnostics = WorkflowValidator.CreateDefault().Validate(workflow, Catalog());
        Assert.Empty(diagnostics);

        var invoker = new FakeInvoker();
        var input = new FlowRecord(new Dictionary<string, object?> { ["customerId"] = "42" });
        var result = new Flow.Runtime.WorkflowExecutor().ExecuteAsync(workflow, input, invoker).GetAwaiter().GetResult();

        Assert.True(result.Success);
        Assert.Equal("Ada Lovelace", result.Output!.Get("customerName"));
    }

    private sealed class FakeInvoker : IMcpInvoker
    {
        public Task<object?> InvokeAsync(string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(new FlowRecord(new Dictionary<string, object?> { ["name"] = "Ada Lovelace" }));
    }
}
