namespace Flow.Validation;

public sealed record ValidationDiagnostic(string Code, string Message, string? NodeId = null);
