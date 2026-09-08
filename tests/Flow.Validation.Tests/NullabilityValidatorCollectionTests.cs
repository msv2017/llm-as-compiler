using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;
using Xunit;

namespace Flow.Validation.Tests;

// Covers the collection/nested-node recursion (a T103 inside a foreach body used to be invisible)
// and the guarded-path narrowing (a guard on `customer.address` used to whitelist the whole
// `customer` root, hiding unguarded dereferences of sibling fields).
public class NullabilityValidatorCollectionTests
{
    private static readonly ConstantProvenance Provenance = new(ConstantProvenanceKind.HandWritten);

    // Contact.profile is Optional — `c.profile.email` inside a foreach body is an unguarded deref.
    private static readonly ObjectType ContactType = new("Contact", new Dictionary<string, FlowType>
    {
        ["id"] = PrimitiveType.String,
        ["profile"] = new OptionalType(new ObjectType("Profile", new Dictionary<string, FlowType>
        {
            ["email"] = PrimitiveType.String
        }))
    });

    // Customer has TWO independently-optional object fields, so a guard on one must not prove the other safe.
    private static readonly ObjectType CustomerType = new("Customer", new Dictionary<string, FlowType>
    {
        ["address"] = new OptionalType(new ObjectType("Address", new Dictionary<string, FlowType>
        {
            ["city"] = PrimitiveType.String
        })),
        ["billing"] = new OptionalType(new ObjectType("Billing", new Dictionary<string, FlowType>
        {
            ["zip"] = PrimitiveType.String
        }))
    });

    private static ToolCatalog Catalog() => new(new[]
    {
        new ToolDefinition(
            "crm.listContacts",
            new ObjectType("ListContactsInput", new Dictionary<string, FlowType> { ["accountId"] = PrimitiveType.String }),
            new ListType(ContactType),
            ToolEffect.Read,
            ToolRetryPolicy.Safe),
        new ToolDefinition(
            "crm.getCustomer",
            new ObjectType("GetCustomerInput", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            CustomerType,
            ToolEffect.Read,
            ToolRetryPolicy.Safe),
        new ToolDefinition(
            "crm.findCustomer",
            new ObjectType("FindCustomerInput", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String }),
            new OptionalType(new ObjectType("SimpleCustomer", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String })),
            ToolEffect.Read,
            ToolRetryPolicy.Safe),
        new ToolDefinition(
            "notify.send",
            new ObjectType("SendInput", new Dictionary<string, FlowType> { ["to"] = PrimitiveType.String }),
            new ObjectType("SendResult", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String }),
            ToolEffect.Write,
            ToolRetryPolicy.Never)
    });

    [Fact]
    public void UnguardedDereferenceInsideForeachBody_ProducesT103()
    {
        var listContacts = new CallNode("contacts", "crm.listContacts",
            new Dictionary<string, FlowExpression> { ["accountId"] = new PathExpression("input.email") });
        var notify = new CallNode("sent", "notify.send",
            new Dictionary<string, FlowExpression> { ["to"] = new PathExpression("c.profile.email") });
        var loop = new ForeachNode("notifications", new PathExpression("contacts"), "c", 100,
            new WorkflowNode[] { notify }, new PathExpression("sent.id"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["name"] = new ConstantExpression("done", Provenance)
        });
        var workflow = WorkflowWith(new WorkflowNode[] { listContacts, loop }, returnNode);

        var diagnostics = new NullabilityValidator().Validate(workflow, Catalog());

        Assert.Contains(diagnostics, d => d.Code == "T103" && d.NodeId == "sent");
    }

    [Fact]
    public void UnguardedDereferenceOfBodyLocalOptionalCallOutput_ProducesT103()
    {
        var listContacts = new CallNode("contacts", "crm.listContacts",
            new Dictionary<string, FlowExpression> { ["accountId"] = new PathExpression("input.email") });
        var lookup = new CallNode("found", "crm.findCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("c.id") });
        var notify = new CallNode("sent", "notify.send",
            new Dictionary<string, FlowExpression> { ["to"] = new PathExpression("found.name") });
        var loop = new ForeachNode("notifications", new PathExpression("contacts"), "c", 100,
            new WorkflowNode[] { lookup, notify }, new PathExpression("sent.id"));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["name"] = new ConstantExpression("done", Provenance)
        });
        var workflow = WorkflowWith(new WorkflowNode[] { listContacts, loop }, returnNode);

        var diagnostics = new NullabilityValidator().Validate(workflow, Catalog());

        Assert.Contains(diagnostics, d => d.Code == "T103" && d.NodeId == "sent");
    }

    [Fact]
    public void UnguardedDereferenceInFilterPredicate_ProducesT103()
    {
        var listContacts = new CallNode("contacts", "crm.listContacts",
            new Dictionary<string, FlowExpression> { ["accountId"] = new PathExpression("input.email") });
        var filtered = new FilterNode("withEmail", new PathExpression("contacts"), "c",
            new BinaryExpression(new PathExpression("c.profile.email"), BinaryOperator.NotEqual, new ConstantExpression("", Provenance)));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression>
        {
            ["name"] = new ConstantExpression("done", Provenance)
        });
        var workflow = WorkflowWith(new WorkflowNode[] { listContacts, filtered }, returnNode);

        var diagnostics = new NullabilityValidator().Validate(workflow, Catalog());

        Assert.Contains(diagnostics, d => d.Code == "T103" && d.NodeId == "withEmail");
    }

    [Fact]
    public void GuardOnOneOptionalField_DoesNotProveSiblingFieldSafe()
    {
        var getCustomer = new CallNode("customer", "crm.getCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        // Guards `customer.address`, then dereferences the UNRELATED `customer.billing.zip` in the same branch.
        var guarded = new IfNode(
            "zip",
            new BinaryExpression(new PathExpression("customer.address"), BinaryOperator.NotEqual, new ConstantExpression(null, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new PathExpression("customer.billing.zip")),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("", Provenance)));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression("zip") });
        var workflow = WorkflowWith(new WorkflowNode[] { getCustomer, guarded }, returnNode);

        var diagnostics = new NullabilityValidator().Validate(workflow, Catalog());

        Assert.Contains(diagnostics, d => d.Code == "T103" && d.Message.Contains("customer.billing.zip"));
    }

    [Fact]
    public void GuardOnOptionalField_ProvesThatFieldAndItsNestedPathsSafe()
    {
        var getCustomer = new CallNode("customer", "crm.getCustomer",
            new Dictionary<string, FlowExpression> { ["email"] = new PathExpression("input.email") });
        var guarded = new IfNode(
            "city",
            new BinaryExpression(new PathExpression("customer.address"), BinaryOperator.NotEqual, new ConstantExpression(null, Provenance)),
            new IfBranch(Array.Empty<WorkflowNode>(), new PathExpression("customer.address.city")),
            new IfBranch(Array.Empty<WorkflowNode>(), new ConstantExpression("", Provenance)));
        var returnNode = new ReturnNode(new Dictionary<string, FlowExpression> { ["name"] = new PathExpression("city") });
        var workflow = WorkflowWith(new WorkflowNode[] { getCustomer, guarded }, returnNode);

        var diagnostics = new NullabilityValidator().Validate(workflow, Catalog());

        Assert.Empty(diagnostics);
    }

    private static WorkflowDefinition WorkflowWith(IReadOnlyList<WorkflowNode> nodes, ReturnNode returnNode)
    {
        var inputType = new ObjectType("Request", new Dictionary<string, FlowType> { ["email"] = PrimitiveType.String });
        var outputType = new ObjectType("Result", new Dictionary<string, FlowType> { ["name"] = PrimitiveType.String });
        return new WorkflowDefinition("Test", inputType, outputType, nodes, returnNode);
    }
}
