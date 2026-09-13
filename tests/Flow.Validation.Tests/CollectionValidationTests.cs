using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

public class CollectionValidationTests
{
    private static readonly ToolCatalog EmptyCatalog = new(Array.Empty<ToolDefinition>());
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);
    private static readonly ObjectType InvoiceType = new("Invoice", new Dictionary<string, FlowType>
    {
        ["amount"] = PrimitiveType.Decimal,
        ["createdAt"] = PrimitiveType.Date
    });

    [Fact]
    public void Dataflow_PredicateReferencingUndefinedRoot_ProducesD301()
    {
        var filter = new FilterNode("unpaid", new PathExpression("input.invoices"), "x", new PathExpression("doesNotExist.amount"));
        var diagnostics = new DataflowValidator().Validate(WorkflowWith(filter), EmptyCatalog);
        Assert.Contains(diagnostics, d => d.Code == "D301");
    }

    [Fact]
    public void Dataflow_PredicateUsingParameter_ProducesNoDiagnostics()
    {
        var filter = new FilterNode("unpaid", new PathExpression("input.invoices"), "x",
            new BinaryExpression(new PathExpression("x.amount"), BinaryOperator.GreaterThan, new ConstantExpression(0, Provenance)));
        var diagnostics = new DataflowValidator().Validate(WorkflowWith(filter), EmptyCatalog);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Type_FilterPredicateNotBool_ProducesT101()
    {
        var filter = new FilterNode("unpaid", new PathExpression("input.invoices"), "x", new PathExpression("x.amount")); // Decimal, not Bool
        var diagnostics = new TypeValidator().Validate(WorkflowWith(filter), EmptyCatalog);
        Assert.Contains(diagnostics, d => d.Code == "T101");
    }

    [Fact]
    public void Type_AggregateSum_ResolvesToDecimal()
    {
        var aggregate = new AggregateNode("total", new PathExpression("input.invoices"), AggregateOperation.Sum, "x", new PathExpression("x.amount"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["outstanding"] = new PathExpression("total") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["invoices"] = new ListType(InvoiceType) });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["outstanding"] = PrimitiveType.Decimal });
        var workflow = new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { aggregate }, returnNode);

        var diagnostics = new TypeValidator().Validate(workflow, EmptyCatalog);

        Assert.Empty(diagnostics);
    }

    private static WorkflowDefinition WorkflowWith(WorkflowNode node)
    {
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["count"] = new PathExpression($"{node.Id}") });
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["invoices"] = new ListType(InvoiceType) });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["count"] = new ListType(InvoiceType) });
        return new WorkflowDefinition("Test", inputType, outputType, new WorkflowNode[] { node }, returnNode);
    }
}
