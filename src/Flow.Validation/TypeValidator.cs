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

        if (workflow.InputType is ObjectType inputType)
        {
            foreach (var (fieldName, fieldType) in inputType.Fields)
            {
                nodeOutputTypes[fieldName] = fieldType;
            }
        }

        ProcessNodes(workflow.Nodes, workflow.InputType, tools, nodeOutputTypes, diagnostics);

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

    private static void ProcessNodes(
        IReadOnlyList<WorkflowNode> nodes,
        FlowType inputType,
        ToolCatalog tools,
        Dictionary<string, FlowType> nodeOutputTypes,
        List<ValidationDiagnostic> diagnostics)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CallNode call when tools.TryGet(call.ToolName, out var tool) && tool is not null:
                    foreach (var (argumentName, expression) in call.Arguments)
                    {
                        if (!tool.InputType.Fields.TryGetValue(argumentName, out var expectedType))
                            continue; // unknown argument — reported by ToolResolutionValidator (F002)
                        CheckExpression(expression, expectedType, call.Id, inputType, nodeOutputTypes, diagnostics);
                    }
                    nodeOutputTypes[call.Id] = tool.OutputType;
                    break;

                case IfNode ifNode:
                    var conditionType = ResolveType(ifNode.Condition, inputType, nodeOutputTypes);
                    if (conditionType is not null && !TypeCompatibility.IsAssignable(PrimitiveType.Bool, conditionType))
                    {
                        diagnostics.Add(new ValidationDiagnostic(
                            "T101", $"Condition must be Bool but produces {conditionType.DisplayName}.", ifNode.Id));
                    }

                    ProcessNodes(ifNode.TrueBranch.Nodes, inputType, tools, nodeOutputTypes, diagnostics);
                    ProcessNodes(ifNode.FalseBranch.Nodes, inputType, tools, nodeOutputTypes, diagnostics);

                    var trueType = ResolveType(ifNode.TrueBranch.Value, inputType, nodeOutputTypes);
                    var falseType = ResolveType(ifNode.FalseBranch.Value, inputType, nodeOutputTypes);

                    if (trueType is not null && falseType is not null)
                    {
                        // Structural compatibility both ways, NOT `trueType == falseType`: FlowType records
                        // (ObjectType/ListType) hold Dictionary-typed fields, and record-generated equality
                        // falls back to reference equality for those — two independently-built ObjectTypes
                        // with identical Name+Fields would otherwise compare unequal.
                        if (TypeCompatibility.IsAssignable(trueType, falseType) && TypeCompatibility.IsAssignable(falseType, trueType))
                        {
                            nodeOutputTypes[ifNode.Id] = trueType;
                        }
                        else
                        {
                            diagnostics.Add(new ValidationDiagnostic(
                                "T101",
                                $"if/else branches must produce the same type: true branch is {trueType.DisplayName}, false branch is {falseType.DisplayName}.",
                                ifNode.Id));
                        }
                    }
                    break;

                case FilterNode filterNode:
                {
                    var sourceType = ResolveType(filterNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                    {
                        var scoped = new Dictionary<string, FlowType>(nodeOutputTypes) { [filterNode.ParameterName] = listType.ElementType };
                        var predicateType = ResolveType(filterNode.Predicate, inputType, scoped);
                        if (predicateType is not null && !TypeCompatibility.IsAssignable(PrimitiveType.Bool, predicateType))
                        {
                            diagnostics.Add(new ValidationDiagnostic(
                                "T101", $"filter predicate must be Bool but produces {predicateType.DisplayName}.", filterNode.Id));
                        }
                        nodeOutputTypes[filterNode.Id] = listType;
                    }
                    break;
                }

                case SortNode sortNode:
                {
                    var sourceType = ResolveType(sortNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                    {
                        // Key comparability is not validated in Phase 1 — trusted from hand-authored IR.
                        nodeOutputTypes[sortNode.Id] = listType;
                    }
                    break;
                }

                case AggregateNode aggregateNode:
                {
                    var sourceType = ResolveType(aggregateNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                    {
                        // Selector's numeric-ness for Sum is not validated in Phase 1 — trusted from hand-authored IR.
                        nodeOutputTypes[aggregateNode.Id] = aggregateNode.Operation switch
                        {
                            AggregateOperation.Count => PrimitiveType.Int,
                            AggregateOperation.First => listType.ElementType,
                            AggregateOperation.Sum => PrimitiveType.Decimal,
                            _ => listType.ElementType
                        };
                    }
                    break;
                }

                case ForeachNode foreachNode:
                {
                    var sourceType = ResolveType(foreachNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                    {
                        var scoped = new Dictionary<string, FlowType>(nodeOutputTypes) { [foreachNode.ParameterName] = listType.ElementType };
                        ProcessNodes(foreachNode.Body, inputType, tools, scoped, diagnostics);
                        var bodyValueType = ResolveType(foreachNode.BodyValue, inputType, scoped);
                        if (bodyValueType is not null)
                            nodeOutputTypes[foreachNode.Id] = new ListType(bodyValueType);
                    }
                    break;
                }

                case AssertNode assertNode:
                {
                    var assertConditionType = ResolveType(assertNode.Condition, inputType, nodeOutputTypes);
                    if (assertConditionType is not null && !TypeCompatibility.IsAssignable(PrimitiveType.Bool, assertConditionType))
                    {
                        diagnostics.Add(new ValidationDiagnostic(
                            "T101", $"assert condition must be Bool but produces {assertConditionType.DisplayName}.", assertNode.Id));
                    }
                    nodeOutputTypes[assertNode.Id] = PrimitiveType.Bool;
                    break;
                }
            }
        }
    }

    private static void CheckExpression(
        FlowExpression expression,
        FlowType expectedType,
        string ownerNodeId,
        FlowType inputType,
        IReadOnlyDictionary<string, FlowType> nodeOutputTypes,
        List<ValidationDiagnostic> diagnostics)
    {
        var actualType = ResolveType(expression, inputType, nodeOutputTypes);
        if (actualType is null)
            return; // unresolved — reported by DataflowValidator, or a node kind this Phase 1 resolver doesn't cover yet

        if (!TypeCompatibility.IsAssignable(expectedType, actualType))
        {
            diagnostics.Add(new ValidationDiagnostic(
                "T101",
                $"Expected {expectedType.DisplayName} but expression produces {actualType.DisplayName}.",
                ownerNodeId));
        }
    }

    private static FlowType? ResolveType(
        FlowExpression expression, FlowType inputType, IReadOnlyDictionary<string, FlowType> nodeOutputTypes) => expression switch
    {
        ConstantExpression constant => InferConstantType(constant.Value),
        PathExpression path => PathTypeResolver.Resolve(path.Path, inputType, nodeOutputTypes, out _),
        BinaryExpression binary => IsComparisonOrBoolean(binary.Operator) ? PrimitiveType.Bool : null,
        _ => null
    };

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
}
