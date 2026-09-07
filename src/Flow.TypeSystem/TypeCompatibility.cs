namespace Flow.TypeSystem;

public static class TypeCompatibility
{
    public static bool IsAssignable(FlowType target, FlowType source)
    {
        // Semantic types type-check against their underlying structural type;
        // SemanticBindingValidator (Flow.Validation) separately flags mismatched
        // semantic tags (source doc §20) — that is a warning-level concern, not
        // a hard type error, so it is intentionally not enforced here.
        if (target is SemanticType targetSemantic)
            return IsAssignable(targetSemantic.Underlying, source);
        if (source is SemanticType sourceSemantic)
            return IsAssignable(target, sourceSemantic.Underlying);

        if (target == source)
            return true;

        if (target is OptionalType optionalTarget)
        {
            if (source is PrimitiveType { Kind: PrimitiveKind.Null })
                return true;
            if (source is OptionalType optionalSource)
                return IsAssignable(optionalTarget.InnerType, optionalSource.InnerType);
            return IsAssignable(optionalTarget.InnerType, source);
        }

        if (target is ListType listTarget && source is ListType listSource)
            return IsAssignable(listTarget.ElementType, listSource.ElementType);

        if (target is ObjectType objectTarget && source is ObjectType objectSource)
            return objectTarget.Name == objectSource.Name;

        if (target is EnumType enumTarget && source is EnumType enumSource)
            return enumTarget.Name == enumSource.Name;

        return false;
    }
}
