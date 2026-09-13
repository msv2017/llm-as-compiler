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

                case FilterNode filterNode:
                {
                    var sourceType = ResolveType(filterNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                        nodeOutputTypes[filterNode.Id] = listType;
                    break;
                }

                case SortNode sortNode:
                {
                    var sourceType = ResolveType(sortNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                        nodeOutputTypes[sortNode.Id] = listType;
                    break;
                }

                case AggregateNode aggregateNode:
                {
                    var sourceType = ResolveType(aggregateNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                    {
                        nodeOutputTypes[aggregateNode.Id] = aggregateNode.Operation switch
                        {
                            AggregateOperation.Count => PrimitiveType.Int,
                            AggregateOperation.First => new OptionalType(listType.ElementType),
                            AggregateOperation.Sum => PrimitiveType.Decimal,
                            _ => listType.ElementType
                        };
                    }
                    break;
                }

                case ForeachNode foreachNode:
                {
                    var scoped = new Dictionary<string, FlowType>(nodeOutputTypes);
                    var sourceType = ResolveType(foreachNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType sourceListType)
                        scoped[foreachNode.ParameterName] = sourceListType.ElementType;

                    CheckNodes(foreachNode.Body, inputType, tools, scoped, diagnostics);

                    if (sourceType is ListType)
                    {
                        var bodyValueType = ResolveType(foreachNode.BodyValue, inputType, scoped);
                        if (bodyValueType is not null)
                            nodeOutputTypes[foreachNode.Id] = new ListType(bodyValueType);
                    }
                    break;
                }
            }
        }
    }

    private static FlowType? ResolveType(
        FlowExpression expression, FlowType inputType, IReadOnlyDictionary<string, FlowType> nodeOutputTypes) => expression switch
    {
        ConstantExpression constant => InferConstantType(constant.Value),
        PathExpression path => PathTypeResolver.Resolve(path.Path, inputType, nodeOutputTypes, out _),
        BinaryExpression binary => IsComparisonOrBoolean(binary.Operator) ? PrimitiveType.Bool : null,
        ObjectExpression obj => ResolveObjectType(obj, inputType, nodeOutputTypes),
        _ => null
    };

    private static FlowType? ResolveObjectType(
        ObjectExpression obj, FlowType inputType, IReadOnlyDictionary<string, FlowType> nodeOutputTypes)
    {
        var fields = new Dictionary<string, FlowType>();
        foreach (var (fieldName, fieldExpression) in obj.Fields)
        {
            var fieldType = ResolveType(fieldExpression, inputType, nodeOutputTypes);
            if (fieldType is null)
                return null;
            fields[fieldName] = fieldType;
        }
        return new ObjectType("Anonymous", fields);
    }

    private static bool IsComparisonOrBoolean(BinaryOperator op) => op is
        BinaryOperator.Equal or BinaryOperator.NotEqual or
        BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual or
        BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual or
        BinaryOperator.And or BinaryOperator.Or;

    private static FlowType? InferConstantType(object? value) => value switch
    {
        null => PrimitiveType.Null,
        bool => PrimitiveType.Bool,
        int or long => PrimitiveType.Int,
        double or decimal => PrimitiveType.Decimal,
        string => PrimitiveType.String,
        _ => null
    };

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
