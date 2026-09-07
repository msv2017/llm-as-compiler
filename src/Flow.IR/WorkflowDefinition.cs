using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.IR;

public sealed record WorkflowDefinition(
    string Name,
    FlowType InputType,
    FlowType OutputType,
    IReadOnlyList<WorkflowNode> Nodes,
    ReturnNode Return);
