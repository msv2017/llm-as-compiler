using Flow.Compiler.Candidate;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Xunit;

namespace Flow.Compiler.Tests;

public class IrToCandidateConverterTests
{
    [Fact]
    public void ConvertsCallNodeAndReturn()
    {
        var call = new CallNode("customer", "crm.getCustomerById",
            new Dictionary<string, FlowExpression> { ["id"] = new PathExpression("input.customerId") });
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["customerName"] = new PathExpression("customer.name") });
        var workflow = new WorkflowDefinition("FindCustomerName",
            new Flow.TypeSystem.ObjectType("Request", new Dictionary<string, Flow.TypeSystem.FlowType>()),
            new Flow.TypeSystem.ObjectType("Result", new Dictionary<string, Flow.TypeSystem.FlowType>()),
            new WorkflowNode[] { call }, returnNode);

        var body = IrToCandidateConverter.Convert(workflow);

        Assert.Equal("FindCustomerName", body.Name);
        var candidateCall = Assert.IsType<CandidateCallNode>(Assert.Single(body.Nodes));
        Assert.Equal("customer", candidateCall.Id);
        Assert.Equal("crm.getCustomerById", candidateCall.Tool);
        var arg = Assert.IsType<CandidatePathExpression>(candidateCall.Arguments["id"]);
        Assert.Equal("input.customerId", arg.Path);
        var returnExpr = Assert.IsType<CandidatePathExpression>(body.Return["customerName"]);
        Assert.Equal("customer.name", returnExpr.Path);
    }

    [Fact]
    public void ConvertsIfNode_BinaryCondition_ObjectAndConstantExpressions()
    {
        var condition = new BinaryExpression(
            new PathExpression("input.age"), BinaryOperator.GreaterThanOrEqual,
            new ConstantExpression(18L, new ConstantProvenance(ConstantProvenanceKind.Prompt, "\"18\" in prompt")));
        var ifNode = new IfNode("eligible",
            condition,
            new IfBranch(Array.Empty<WorkflowNode>(), new ObjectExpression(
                new Dictionary<string, FlowExpression> { ["allowed"] = new ConstantExpression(true, new ConstantProvenance(ConstantProvenanceKind.SystemPolicy)) })),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression(null, new ConstantProvenance(ConstantProvenanceKind.Unproven))));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["result"] = new PathExpression("eligible") });
        var workflow = new WorkflowDefinition("CheckEligibility",
            new Flow.TypeSystem.ObjectType("Request", new Dictionary<string, Flow.TypeSystem.FlowType>()),
            new Flow.TypeSystem.ObjectType("Result", new Dictionary<string, Flow.TypeSystem.FlowType>()),
            new WorkflowNode[] { ifNode }, returnNode);

        var body = IrToCandidateConverter.Convert(workflow);

        var candidateIf = Assert.IsType<CandidateIfNode>(Assert.Single(body.Nodes));
        var candidateCondition = Assert.IsType<CandidateBinaryExpression>(candidateIf.Condition);
        Assert.Equal("GreaterThanOrEqual", candidateCondition.Operator);
        var right = Assert.IsType<CandidateConstantExpression>(candidateCondition.Right);
        Assert.Equal(18L, right.Value);
        Assert.Equal("PROMPT", right.Source);
        Assert.Equal("\"18\" in prompt", right.Detail);
        var trueValue = Assert.IsType<CandidateObjectExpression>(candidateIf.TrueBranch.Value);
        var allowed = Assert.IsType<CandidateConstantExpression>(trueValue.Fields["allowed"]);
        Assert.Equal(true, allowed.Value);
        Assert.Equal("SYSTEM_POLICY", allowed.Source);
    }

    [Fact]
    public void ConvertsFilterSortAggregateForeachAndAssertNodes()
    {
        var filter = new FilterNode("openInvoices", new PathExpression("input.invoices"), "invoice",
            new BinaryExpression(new PathExpression("invoice.status"), BinaryOperator.Equal, new ConstantExpression("OPEN", new ConstantProvenance(ConstantProvenanceKind.Prompt))));
        var sort = new SortNode("sorted", new PathExpression("openInvoices"), "invoice", new PathExpression("invoice.dueDate"), SortDirection.Ascending);
        var aggregate = new AggregateNode("oldest", new PathExpression("sorted"), AggregateOperation.First, null, null);
        var assert = new AssertNode("guard", new BinaryExpression(new PathExpression("oldest"), BinaryOperator.NotEqual, new ConstantExpression(null, new ConstantProvenance(ConstantProvenanceKind.Unproven))), "NO_OPEN_INVOICES");
        var foreachNode = new ForeachNode("ids", new PathExpression("input.invoices"), "invoice", 100,
            new WorkflowNode[] { }, new PathExpression("invoice.id"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["ids"] = new PathExpression("ids") });
        var workflow = new WorkflowDefinition("RefundOldestInvoice",
            new Flow.TypeSystem.ObjectType("Request", new Dictionary<string, Flow.TypeSystem.FlowType>()),
            new Flow.TypeSystem.ObjectType("Result", new Dictionary<string, Flow.TypeSystem.FlowType>()),
            new WorkflowNode[] { filter, sort, aggregate, assert, foreachNode }, returnNode);

        var body = IrToCandidateConverter.Convert(workflow);

        Assert.Equal(5, body.Nodes.Count);
        var candidateFilter = Assert.IsType<CandidateFilterNode>(body.Nodes[0]);
        Assert.Equal("invoice", candidateFilter.ParameterName);
        var candidateSort = Assert.IsType<CandidateSortNode>(body.Nodes[1]);
        Assert.Equal("ASCENDING", candidateSort.Direction);
        var candidateAggregate = Assert.IsType<CandidateAggregateNode>(body.Nodes[2]);
        Assert.Equal("First", candidateAggregate.Operation);
        Assert.Null(candidateAggregate.Selector);
        var candidateAssert = Assert.IsType<CandidateAssertNode>(body.Nodes[3]);
        Assert.Equal("NO_OPEN_INVOICES", candidateAssert.FailureCode);
        var candidateForeach = Assert.IsType<CandidateForeachNode>(body.Nodes[4]);
        Assert.Equal(100, candidateForeach.Limit);
    }
}
