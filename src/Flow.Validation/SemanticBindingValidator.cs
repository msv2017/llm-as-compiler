using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.Validation;

public sealed class SemanticBindingValidator : IWorkflowValidationPass
{
    public string Name => "SemanticBinding";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        var nodeOutputTypes = new Dictionary<string, FlowType>();

        if (workflow.InputType is ObjectType inputObjectType)
        {
            foreach (var (fieldName, fieldType) in inputObjectType.Fields)
                nodeOutputTypes[fieldName] = fieldType;
        }

        CheckNodes(workflow.Nodes, workflow.InputType, tools, nodeOutputTypes, diagnostics);
        return diagnostics;
    }

    private static void CheckNodes(
        IReadOnlyList<WorkflowNode> nodes, FlowType inputType, ToolCatalog tools,
        Dictionary<string, FlowType> nodeOutputTypes, List<ValidationDiagnostic> diagnostics)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CallNode call when tools.TryGet(call.ToolName, out var tool) && tool is not null:
                    foreach (var (argumentName, expression) in call.Arguments)
                    {
                        if (expression is PathExpression path &&
                            tool.InputType.Fields.TryGetValue(argumentName, out var expectedType))
                        {
                            CheckBinding(call.Id, argumentName, path, expectedType, inputType, nodeOutputTypes, diagnostics);
                        }
                    }
                    nodeOutputTypes[call.Id] = tool.OutputType;
                    break;

                case IfNode ifNode:
                    CheckNodes(ifNode.TrueBranch.Nodes, inputType, tools, new Dictionary<string, FlowType>(nodeOutputTypes), diagnostics);
                    CheckNodes(ifNode.FalseBranch.Nodes, inputType, tools, new Dictionary<string, FlowType>(nodeOutputTypes), diagnostics);
                    break;

                case ForeachNode foreachNode:
                    CheckNodes(foreachNode.Body, inputType, tools, new Dictionary<string, FlowType>(nodeOutputTypes), diagnostics);
                    break;
            }
        }
    }

    private static void CheckBinding(
        string ownerNodeId, string argumentName, PathExpression path, FlowType expectedType, FlowType inputType,
        IReadOnlyDictionary<string, FlowType> nodeOutputTypes, List<ValidationDiagnostic> diagnostics)
    {
        var actualType = PathTypeResolver.Resolve(path.Path, inputType, nodeOutputTypes, out _);
        if (actualType is null)
            return; // unresolved — reported by DataflowValidator/TypeValidator

        var expectedSemanticName = UnwrapSemantic(expectedType, out var expectedUnderlying);
        var actualSemanticName = UnwrapSemantic(actualType, out var actualUnderlying);

        if (expectedSemanticName is null || actualSemanticName is null || expectedSemanticName == actualSemanticName)
            return;
        if (!TypeCompatibility.IsAssignable(expectedUnderlying, actualUnderlying))
            return; // structurally incompatible — TypeValidator's T101 already covers this

        var suggestion = FindLikelySource(path.Path, expectedSemanticName, nodeOutputTypes);
        var message = $"Argument '{argumentName}' of '{ownerNodeId}' expects semantic type '{expectedSemanticName}' " +
                       $"but received '{actualSemanticName}' from '{path.Path}'." +
                       (suggestion is null ? "" : $" Likely compatible source: '{suggestion}'.");
        diagnostics.Add(new ValidationDiagnostic("S201", message, ownerNodeId));
    }

    private static string? UnwrapSemantic(FlowType type, out FlowType underlying)
    {
        var core = type is OptionalType optional ? optional.InnerType : type;
        if (core is SemanticType semantic)
        {
            underlying = semantic.Underlying;
            return semantic.Name;
        }
        underlying = core;
        return null;
    }

    private static string? FindLikelySource(
        string currentPath, string expectedSemanticName, IReadOnlyDictionary<string, FlowType> nodeOutputTypes)
    {
        var root = currentPath.Split('.')[0];
        if (!nodeOutputTypes.TryGetValue(root, out var rootType))
            return null;

        var core = rootType is OptionalType optional ? optional.InnerType : rootType;
        if (core is not ObjectType objectType)
            return null;

        foreach (var (fieldName, fieldType) in objectType.Fields)
        {
            var fieldCore = fieldType is OptionalType fieldOptional ? fieldOptional.InnerType : fieldType;
            if (fieldCore is SemanticType fieldSemantic && fieldSemantic.Name == expectedSemanticName)
                return $"{root}.{fieldName}";
        }
        return null;
    }
}
