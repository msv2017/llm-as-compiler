using Flow.TypeSystem;

namespace Flow.Validation;

internal static class PathTypeResolver
{
    public static FlowType? Resolve(
        string path,
        FlowType inputType,
        IReadOnlyDictionary<string, FlowType> nodeOutputTypes,
        out bool passedThroughUnwrappedOptional)
    {
        passedThroughUnwrappedOptional = false;
        var segments = path.Split('.');

        FlowType? current = segments[0] == "input" ? inputType : nodeOutputTypes.GetValueOrDefault(segments[0]);
        if (current is null)
            return null;

        for (var i = 1; i < segments.Length; i++)
        {
            if (current is OptionalType optional)
            {
                passedThroughUnwrappedOptional = true;
                current = optional.InnerType;
            }

            if (current is not ObjectType objectType || !objectType.Fields.TryGetValue(segments[i], out var fieldType))
                return null;

            current = fieldType;
        }

        return current;
    }
}
