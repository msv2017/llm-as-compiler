using Flow.Contracts;
using Flow.IR;
using Flow.IR.Expressions;
using Flow.IR.Nodes;
using Flow.TypeSystem;

namespace Flow.Validation;

public sealed class NullabilityValidator : IWorkflowValidationPass
{
    public string Name => "Nullability";

    public IReadOnlyList<ValidationDiagnostic> Validate(WorkflowDefinition workflow, ToolCatalog tools)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        var nodeOutputTypes = new Dictionary<string, FlowType>();
        var safe = new HashSet<string>();

        ProcessNodes(workflow.Nodes, workflow.InputType, tools, nodeOutputTypes, safe, diagnostics);
        CheckExpressions(workflow.Return.Id, workflow.Return.Fields.Values, workflow.InputType, nodeOutputTypes, safe, diagnostics);

        return diagnostics;
    }

    private static void ProcessNodes(
        IReadOnlyList<WorkflowNode> nodes,
        FlowType inputType,
        ToolCatalog tools,
        Dictionary<string, FlowType> nodeOutputTypes,
        HashSet<string> safe,
        List<ValidationDiagnostic> diagnostics)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CallNode call when tools.TryGet(call.ToolName, out var tool) && tool is not null:
                    CheckExpressions(call.Id, call.Arguments.Values, inputType, nodeOutputTypes, safe, diagnostics);
                    nodeOutputTypes[call.Id] = tool.OutputType;
                    break;

                case IfNode ifNode:
                    CheckExpressions(ifNode.Id, new[] { ifNode.Condition }, inputType, nodeOutputTypes, safe, diagnostics);

                    var (trueSafeAddition, falseSafeAddition) = GuardedPath(ifNode.Condition);

                    var trueSafe = new HashSet<string>(safe);
                    if (trueSafeAddition is not null) trueSafe.Add(trueSafeAddition);
                    ProcessNodes(ifNode.TrueBranch.Nodes, inputType, tools, nodeOutputTypes, trueSafe, diagnostics);
                    CheckExpressions(ifNode.Id, new[] { ifNode.TrueBranch.Value }, inputType, nodeOutputTypes, trueSafe, diagnostics);

                    var falseSafe = new HashSet<string>(safe);
                    if (falseSafeAddition is not null) falseSafe.Add(falseSafeAddition);
                    ProcessNodes(ifNode.FalseBranch.Nodes, inputType, tools, nodeOutputTypes, falseSafe, diagnostics);
                    CheckExpressions(ifNode.Id, new[] { ifNode.FalseBranch.Value }, inputType, nodeOutputTypes, falseSafe, diagnostics);
                    break;

                case FilterNode filterNode:
                {
                    CheckExpressions(filterNode.Id, new[] { filterNode.Source }, inputType, nodeOutputTypes, safe, diagnostics);
                    var sourceType = ResolveType(filterNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                    {
                        var scopedTypes = new Dictionary<string, FlowType>(nodeOutputTypes) { [filterNode.ParameterName] = listType.ElementType };
                        // The element binding is deliberately NOT added to `safe`: `safe` holds paths whose
                        // dereference is proven null-safe, and IsPathSafe covers everything nested under an
                        // entry — seeding the loop/predicate parameter would whitelist `x.anyOptionalField`
                        // and re-hide exactly the unguarded dereferences this pass exists to find. The
                        // parameter's own non-nullness is already carried by its element type in scopedTypes.
                        CheckExpressions(filterNode.Id, new[] { filterNode.Predicate }, inputType, scopedTypes, safe, diagnostics);
                        nodeOutputTypes[filterNode.Id] = listType;
                    }
                    break;
                }

                case SortNode sortNode:
                {
                    CheckExpressions(sortNode.Id, new[] { sortNode.Source }, inputType, nodeOutputTypes, safe, diagnostics);
                    var sourceType = ResolveType(sortNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                    {
                        var scopedTypes = new Dictionary<string, FlowType>(nodeOutputTypes) { [sortNode.ParameterName] = listType.ElementType };
                        CheckExpressions(sortNode.Id, new[] { sortNode.Key }, inputType, scopedTypes, safe, diagnostics);
                        nodeOutputTypes[sortNode.Id] = listType;
                    }
                    break;
                }

                case AggregateNode aggregateNode:
                {
                    CheckExpressions(aggregateNode.Id, new[] { aggregateNode.Source }, inputType, nodeOutputTypes, safe, diagnostics);
                    var sourceType = ResolveType(aggregateNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                    {
                        if (aggregateNode.Selector is not null && aggregateNode.ParameterName is not null)
                        {
                            var scopedTypes = new Dictionary<string, FlowType>(nodeOutputTypes) { [aggregateNode.ParameterName] = listType.ElementType };
                            CheckExpressions(aggregateNode.Id, new[] { aggregateNode.Selector }, inputType, scopedTypes, safe, diagnostics);
                        }
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
                    CheckExpressions(foreachNode.Id, new[] { foreachNode.Source }, inputType, nodeOutputTypes, safe, diagnostics);
                    var sourceType = ResolveType(foreachNode.Source, inputType, nodeOutputTypes);
                    if (sourceType is ListType listType)
                    {
                        var scopedTypes = new Dictionary<string, FlowType>(nodeOutputTypes) { [foreachNode.ParameterName] = listType.ElementType };
                        ProcessNodes(foreachNode.Body, inputType, tools, scopedTypes, safe, diagnostics);
                        CheckExpressions(foreachNode.Id, new[] { foreachNode.BodyValue }, inputType, scopedTypes, safe, diagnostics);
                        var bodyValueType = ResolveType(foreachNode.BodyValue, inputType, scopedTypes);
                        if (bodyValueType is not null)
                            nodeOutputTypes[foreachNode.Id] = new ListType(bodyValueType);
                    }
                    break;
                }

                case AssertNode assertNode:
                    CheckExpressions(assertNode.Id, new[] { assertNode.Condition }, inputType, nodeOutputTypes, safe, diagnostics);
                    nodeOutputTypes[assertNode.Id] = PrimitiveType.Bool;
                    break;
            }
        }
    }

    // Recognizes `path == null` / `null == path` / `path != null` / `null != path` and returns
    // (pathMadeSafeInTrueBranch, pathMadeSafeInFalseBranch) — the FULL guarded path (not just its
    // root), so a guard on `customer.address` only proves `customer.address` (and anything nested
    // under it) safe, not unrelated sibling fields like `customer.billing`.
    private static (string? TrueSafe, string? FalseSafe) GuardedPath(FlowExpression condition)
    {
        if (condition is not BinaryExpression { Operator: BinaryOperator.Equal or BinaryOperator.NotEqual } binary)
            return (null, null);

        string? guardedPath = (binary.Left, binary.Right) switch
        {
            (PathExpression p, ConstantExpression { Value: null }) => p.Path,
            (ConstantExpression { Value: null }, PathExpression p) => p.Path,
            _ => null
        };

        if (guardedPath is null)
            return (null, null);

        return binary.Operator == BinaryOperator.Equal ? (null, guardedPath) : (guardedPath, null);
    }

    private static void CheckExpressions(
        string ownerNodeId,
        IEnumerable<FlowExpression> expressions,
        FlowType inputType,
        IReadOnlyDictionary<string, FlowType> nodeOutputTypes,
        HashSet<string> safe,
        List<ValidationDiagnostic> diagnostics)
    {
        foreach (var expression in expressions)
            CheckExpression(ownerNodeId, expression, inputType, nodeOutputTypes, safe, diagnostics);
    }

    private static void CheckExpression(
        string ownerNodeId,
        FlowExpression expression,
        FlowType inputType,
        IReadOnlyDictionary<string, FlowType> nodeOutputTypes,
        HashSet<string> safe,
        List<ValidationDiagnostic> diagnostics)
    {
        switch (expression)
        {
            case PathExpression path:
                var segments = path.Path.Split('.');
                if (segments.Length > 1 && !IsPathSafe(path.Path, safe))
                {
                    PathTypeResolver.Resolve(path.Path, inputType, nodeOutputTypes, out var passedThroughUnwrappedOptional);
                    if (passedThroughUnwrappedOptional)
                    {
                        diagnostics.Add(new ValidationDiagnostic(
                            "T103",
                            $"'{path.Path}' may be null; it cannot be accessed until the workflow proves it is not null.",
                            ownerNodeId));
                    }
                }
                break;
            case BinaryExpression binary:
                CheckExpression(ownerNodeId, binary.Left, inputType, nodeOutputTypes, safe, diagnostics);
                CheckExpression(ownerNodeId, binary.Right, inputType, nodeOutputTypes, safe, diagnostics);
                break;
            case ObjectExpression obj:
                foreach (var fieldExpression in obj.Fields.Values)
                    CheckExpression(ownerNodeId, fieldExpression, inputType, nodeOutputTypes, safe, diagnostics);
                break;
        }
    }

    // A dereferenced path is proven safe if some guarded path in `safe` equals it exactly, or is a
    // strict prefix of it (e.g. guarding "customer" also covers "customer.name"; guarding
    // "customer.address" covers "customer.address.city" but NOT the unrelated "customer.billing").
    private static bool IsPathSafe(string path, HashSet<string> safe)
    {
        foreach (var guardedPath in safe)
        {
            if (path == guardedPath || path.StartsWith(guardedPath + ".", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static FlowType? ResolveType(
        FlowExpression expression, FlowType inputType, IReadOnlyDictionary<string, FlowType> nodeOutputTypes) => expression switch
    {
        ConstantExpression constant => InferConstantType(constant.Value),
        PathExpression path => PathTypeResolver.Resolve(path.Path, inputType, nodeOutputTypes, out _),
        BinaryExpression => PrimitiveType.Bool,
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
