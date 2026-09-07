using Flow.TypeSystem;
using Xunit;

namespace Flow.IR.Tests;

public class TypeCompatibilityTests
{
    [Fact]
    public void SameType_IsAssignable()
    {
        Assert.True(TypeCompatibility.IsAssignable(PrimitiveType.String, PrimitiveType.String));
    }

    [Fact]
    public void DifferentPrimitives_AreNotAssignable()
    {
        Assert.False(TypeCompatibility.IsAssignable(PrimitiveType.String, PrimitiveType.Int));
    }

    [Fact]
    public void Optional_AcceptsInnerType()
    {
        var target = new OptionalType(PrimitiveType.String);
        Assert.True(TypeCompatibility.IsAssignable(target, PrimitiveType.String));
    }

    [Fact]
    public void Optional_AcceptsNull()
    {
        var target = new OptionalType(PrimitiveType.String);
        Assert.True(TypeCompatibility.IsAssignable(target, PrimitiveType.Null));
    }

    [Fact]
    public void NonOptional_DoesNotAcceptOptionalSource()
    {
        var source = new OptionalType(PrimitiveType.String);
        Assert.False(TypeCompatibility.IsAssignable(PrimitiveType.String, source));
    }

    [Fact]
    public void List_IsAssignable_WhenElementTypesAssignable()
    {
        // target accepts Optional<string>, source is List<string>.
        // These ListType records are NOT equal (different ElementTypes),
        // so they don't hit the == shortcut. But IsAssignable should still
        // return true by recursively checking element types:
        // IsAssignable(Optional<string>, string) -> true
        var target = new ListType(new OptionalType(PrimitiveType.String));
        var source = new ListType(PrimitiveType.String);
        Assert.True(TypeCompatibility.IsAssignable(target, source));
    }

    [Fact]
    public void Object_IsAssignable_WhenNamesMatch()
    {
        // target and source have the same Name ("Customer") but different Fields.
        // These ObjectType records are NOT equal (different Fields),
        // so they don't hit the == shortcut. But IsAssignable should still
        // return true by checking Name equality only.
        var target = new ObjectType("Customer", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String });
        var source = new ObjectType("Customer", new Dictionary<string, FlowType> { ["id"] = PrimitiveType.String, ["name"] = PrimitiveType.String });
        Assert.True(TypeCompatibility.IsAssignable(target, source));
    }

    [Fact]
    public void SemanticType_IsAssignable_ToItsUnderlyingType()
    {
        var source = new SemanticType("CustomerId", PrimitiveType.String);
        Assert.True(TypeCompatibility.IsAssignable(PrimitiveType.String, source));
    }
}
