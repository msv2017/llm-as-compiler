namespace Flow.TypeSystem;

public abstract record FlowType
{
    public abstract string DisplayName { get; }
}

public enum PrimitiveKind { Null, Bool, Int, Decimal, String, Date, DateTime, Duration }

public sealed record PrimitiveType(PrimitiveKind Kind) : FlowType
{
    public override string DisplayName => Kind.ToString();

    public static readonly PrimitiveType Null = new(PrimitiveKind.Null);
    public static readonly PrimitiveType Bool = new(PrimitiveKind.Bool);
    public static readonly PrimitiveType Int = new(PrimitiveKind.Int);
    public static readonly PrimitiveType Decimal = new(PrimitiveKind.Decimal);
    public static readonly PrimitiveType String = new(PrimitiveKind.String);
    public static readonly PrimitiveType Date = new(PrimitiveKind.Date);
    public static readonly PrimitiveType DateTime = new(PrimitiveKind.DateTime);
    public static readonly PrimitiveType Duration = new(PrimitiveKind.Duration);
}

public sealed record ObjectType(string Name, IReadOnlyDictionary<string, FlowType> Fields) : FlowType
{
    public override string DisplayName => Name;
}

public sealed record ListType(FlowType ElementType) : FlowType
{
    public override string DisplayName => $"List<{ElementType.DisplayName}>";
}

public sealed record OptionalType(FlowType InnerType) : FlowType
{
    public override string DisplayName => $"{InnerType.DisplayName}?";
}

public sealed record EnumType(string Name, IReadOnlyList<string> Values) : FlowType
{
    public override string DisplayName => Name;
}

public sealed record SemanticType(string Name, FlowType Underlying) : FlowType
{
    public override string DisplayName => $"{Underlying.DisplayName}@semantic({Name})";
}
