using Flow.Cli;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.Cli.Tests;

public class WorkflowPrinterTests
{
    private static WorkflowDefinition Wrap(IReadOnlyList<WorkflowNode> nodes, ReturnNode returnNode) =>
        new("W",
            new ObjectType("Request", new Dictionary<string, FlowType>()),
            new ObjectType("Result", new Dictionary<string, FlowType>()),
            nodes, returnNode);

    [Fact]
    public void SingleCallNode_PrintsCallLineAndReturn()
    {
        var call = new CallNode("customer", "crm.getCustomerById",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["customerName"] = new PathExpression("customer.name") });
        var workflow = Wrap(new WorkflowNode[] { call }, returnNode);

        var printed = WorkflowPrinter.Print(workflow);

        Assert.Contains("call customer = crm.getCustomerById(id: input.customerId)", printed);
        Assert.Contains("return { customerName: customer.name }", printed);
    }

    [Fact]
    public void AggregateWithSelector_PrintsOperationAndSelector()
    {
        var aggregate = new AggregateNode("total", new PathExpression("invoices"), AggregateOperation.Sum, "x", new PathExpression("x.amount"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["total"] = new PathExpression("total") });
        var workflow = Wrap(new WorkflowNode[] { aggregate }, returnNode);

        var printed = WorkflowPrinter.Print(workflow);

        Assert.Contains("aggregate total = invoices.Sum(x => x.amount)", printed);
    }

    [Fact]
    public void AggregateWithoutSelector_PrintsOperationWithNoParens()
    {
        var aggregate = new AggregateNode("count", new PathExpression("invoices"), AggregateOperation.Count, null, null);
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["count"] = new PathExpression("count") });
        var workflow = Wrap(new WorkflowNode[] { aggregate }, returnNode);

        var printed = WorkflowPrinter.Print(workflow);

        Assert.Contains("aggregate count = invoices.Count", printed);
    }

    [Fact]
    public void IfNode_PrintsConditionAndBothBranchesIndented()
    {
        var trueBranch = new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(true, new ConstantProvenance(ConstantProvenanceKind.HandWritten)));
        var falseBranch = new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(false, new ConstantProvenance(ConstantProvenanceKind.HandWritten)));
        var ifNode = new IfNode("check", new PathExpression("input.flag"), trueBranch, falseBranch);
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new PathExpression("check") });
        var workflow = Wrap(new WorkflowNode[] { ifNode }, returnNode);

        var printed = WorkflowPrinter.Print(workflow);

        Assert.Contains("if input.flag:", printed);
        Assert.Contains("=> true", printed);
        Assert.Contains("else:", printed);
        Assert.Contains("=> false", printed);
    }

    [Fact]
    public void FilterNode_PrintsSourceAndPredicate()
    {
        var filter = new FilterNode("filtered", new PathExpression("customers"), "c",
            new BinaryExpression(new PathExpression("c.active"), BinaryOperator.Equal,
                new ConstantExpression(true, new ConstantProvenance(ConstantProvenanceKind.HandWritten))));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["filtered"] = new PathExpression("filtered") });
        var workflow = Wrap(new WorkflowNode[] { filter }, returnNode);

        var printed = WorkflowPrinter.Print(workflow);

        Assert.Contains("filter filtered = customers.where(c => (c.active == true))", printed);
    }

    [Fact]
    public void ConstantString_PrintsQuoted()
    {
        var call = new CallNode("customer", "crm.getCustomerById",
            new Dictionary<string, FlowExpression> { ["id"] = new ConstantExpression("42", new ConstantProvenance(ConstantProvenanceKind.HandWritten)) });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ok"] = new PathExpression("customer.name") });
        var workflow = Wrap(new WorkflowNode[] { call }, returnNode);

        var printed = WorkflowPrinter.Print(workflow);

        Assert.Contains("id: \"42\"", printed);
    }
}
