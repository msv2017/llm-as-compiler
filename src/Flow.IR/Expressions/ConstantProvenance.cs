namespace Flow.IR.Expressions;

public enum ConstantProvenanceKind { Prompt, ToolSchema, SystemPolicy, ExplicitCompilerPolicy, HandWritten, Unproven }

public sealed record ConstantProvenance(ConstantProvenanceKind Kind, string? Detail = null);
