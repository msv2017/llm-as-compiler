using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.Validation;

public sealed class TypeValidator : IWorkflowValidationPass
{
    public string Name => "Type";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        var nodeOutputTypes = new Dictionary<string, FlowType>();

        foreach (var node in workflow.Nodes)
        {
            if (node is CallNode call && tools.TryGet(call.ToolName, out var tool) && tool is not null)
            {
                foreach (var (argumentName, expression) in call.Arguments)
                {
                    if (!tool.InputType.Fields.TryGetValue(argumentName, out var expectedType))
                        continue; // unknown argument — reported by ToolResolutionValidator (F002)

                    CheckExpression(expression, expectedType, call.Id, workflow.InputType, nodeOutputTypes, diagnostics);
                }

                nodeOutputTypes[call.Id] = tool.OutputType;
            }
        }

        if (workflow.OutputType is ObjectType outputType)
        {
            foreach (var (fieldName, expression) in workflow.Return.Fields)
            {
                if (!outputType.Fields.TryGetValue(fieldName, out var expectedType))
                    continue; // unknown output field — reported by OutputValidator (O602)

                CheckExpression(expression, expectedType, workflow.Return.Id, workflow.InputType, nodeOutputTypes, diagnostics);
            }
        }

        return diagnostics;
    }

    private static void CheckExpression(
        FlowExpression expression,
        FlowType expectedType,
        string ownerNodeId,
        FlowType inputType,
        IReadOnlyDictionary<string, FlowType> nodeOutputTypes,
        List<ValidationDiagnostic> diagnostics)
    {
        FlowType? actualType = expression switch
        {
            ConstantExpression constant => InferConstantType(constant.Value),
            PathExpression path => PathTypeResolver.Resolve(path.Path, inputType, nodeOutputTypes, out _),
            _ => null
        };

        if (actualType is null)
            return; // unresolved path — reported by DataflowValidator, or a node kind this Phase 1 resolver doesn't cover yet

        if (!TypeCompatibility.IsAssignable(expectedType, actualType))
        {
            diagnostics.Add(new ValidationDiagnostic(
                "T101",
                $"Expected {expectedType.DisplayName} but expression produces {actualType.DisplayName}.",
                ownerNodeId));
        }
    }

    private static FlowType? InferConstantType(object? value) => value switch
    {
        null => PrimitiveType.Null,
        bool => PrimitiveType.Bool,
        int or long => PrimitiveType.Int,
        double or decimal => PrimitiveType.Decimal,
        string => PrimitiveType.String,
        _ => null
    };
}
