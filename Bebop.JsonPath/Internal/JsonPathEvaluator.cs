using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Evaluates a parsed JSONPath query against a <see cref="JsonElement"/> tree.
/// </summary>
internal static class JsonPathEvaluator
{
    /// <summary>
    /// Evaluates the query segments against the root element and returns the resulting nodelist.
    /// </summary>
    internal static JsonElement[] Evaluate(ReadOnlySpan<Segment> segments, ref readonly JsonElement root)
    {
        // Fast path: when every segment is a non-descendant, single name/index selector
        // we can walk the tree directly without any intermediate list allocations.
        if (TryEvaluateSingularPath(segments, in root, out var singularResult))
            return singularResult;

        var current = new List<JsonElement> { root };

        foreach (var segment in segments)
        {
            var next = new List<JsonElement>(current.Count);
            foreach (var node in current)
            {
                // Dispatch based on segment type and descendant flag
                switch (segment)
                {
                    case SingleSelectorSegment sing:
                        if (sing.IsDescendant)
                            EvaluateDescendantSingularSegment(sing.Selector, in node, in root, next);
                        else
                            ApplySelector(sing.Selector, in node, in root, next);
                        break;
                        
                    case MultiSelectorSegment multi:
                        if (multi.IsDescendant)
                            EvaluateDescendantMultiSegment(multi.Selectors, in node, in root, next);
                        else
                            EvaluateChildMultiSegment(multi.Selectors, in node, in root, next);
                        break;
                }
            }
            current = next;
        }

        return current.ToArray();
    }

    /// <summary>
    /// Attempts a zero-allocation walk for paths composed entirely of
    /// non-descendant single name/index selectors (e.g. $.store.book[0].title).
    /// </summary>
    private static bool TryEvaluateSingularPath(ReadOnlySpan<Segment> segments, ref readonly JsonElement root, out JsonElement[] result)
    {
        foreach (var segment in segments)
        {
            if (segment.IsDescendant)
            {
                result = default!;
                return false;
            }

            if (segment is not SingleSelectorSegment sing)
            {
                result = default!;
                return false;
            }
            
            if (sing.Selector is not NameSelector and not IndexSelector)
            {
                result = default!;
                return false;
            }
        }

        var node = root;
        foreach (var segment in segments)
        {
            var sing = (SingleSelectorSegment)segment;
            switch (sing.Selector)
            {
                case NameSelector ns:
                    if (node.ValueKind == JsonValueKind.Object && node.TryGetProperty(ns.Name, out var prop))
                        node = prop;
                    else
                    {
                        result = [];
                        return true;
                    }
                    break;

                case IndexSelector ix:
                    if (node.ValueKind == JsonValueKind.Array)
                    {
                        int len = node.GetArrayLength();
                        long effective = ix.Index >= 0 ? ix.Index : len + ix.Index;
                        if (effective >= 0 && effective < len)
                            node = node[(int)effective];
                        else
                        {
                            result = [];
                            return true;
                        }
                    }
                    else
                    {
                        result = [];
                        return true;
                    }
                    break;
            }
        }

        result = [node];
        return true;
    }

    private static void EvaluateChildMultiSegment(ISelector[] selectors, ref readonly JsonElement node, ref readonly JsonElement root, List<JsonElement> results)
    {
        foreach (var selector in selectors)
        {
            ApplySelector(selector, in node, in root, results);
        }
    }

    private static void EvaluateDescendantSingularSegment(ISelector selector, ref readonly JsonElement node, ref readonly JsonElement root, List<JsonElement> results)
    {
        // Visit the node and all its descendants in pre-order.
        // For each visited node, apply the selector.
        CollectDescendantsAndApplySelector(in node, selector, in root, results);
    }

    private static void EvaluateDescendantMultiSegment(ISelector[] selectors, ref readonly JsonElement node, ref readonly JsonElement root, List<JsonElement> results)
    {
        // Visit the node and all its descendants in pre-order.
        // For each visited node, apply all selectors.
        CollectDescendantsAndApplySelectors(in node, selectors, in root, results);
    }

    private static void CollectDescendantsAndApplySelector(ref readonly JsonElement node, ISelector selector, ref readonly JsonElement root, List<JsonElement> results)
    {
        // Iterative pre-order traversal using a stack to avoid recursion overhead.
        var stack = new Stack<JsonElement>();
        stack.Push(node);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            // Apply selector to current node
            ApplySelector(selector, in current, in root, results);

            // Push children in reverse order so they are processed left-to-right
            if (current.ValueKind == JsonValueKind.Array)
            {
                int len = current.GetArrayLength();
                for (int i = len - 1; i >= 0; i--)
                    stack.Push(current[i]);
            }
            else if (current.ValueKind == JsonValueKind.Object)
            {
                // We need to push in reverse order; collect to a local list first
                var props = new List<JsonElement>(current.GetPropertyCount());
                foreach (var prop in current.EnumerateObject())
                    props.Add(prop.Value);
                for (int i = props.Count - 1; i >= 0; i--)
                    stack.Push(props[i]);
            }
        }
    }

    private static void CollectDescendantsAndApplySelectors(ref readonly JsonElement node, ISelector[] selectors, ref readonly JsonElement root, List<JsonElement> results)
    {
        // Iterative pre-order traversal using a stack to avoid recursion overhead.
        var stack = new Stack<JsonElement>();
        stack.Push(node);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            // Apply all selectors to current node
            foreach (var selector in selectors)
                ApplySelector(selector, in current, in root, results);

            // Push children in reverse order so they are processed left-to-right
            if (current.ValueKind == JsonValueKind.Array)
            {
                int len = current.GetArrayLength();
                for (int i = len - 1; i >= 0; i--)
                    stack.Push(current[i]);
            }
            else if (current.ValueKind == JsonValueKind.Object)
            {
                var props = new List<JsonElement>(current.GetPropertyCount());
                foreach (var prop in current.EnumerateObject())
                    props.Add(prop.Value);
                for (int i = props.Count - 1; i >= 0; i--)
                    stack.Push(props[i]);
            }
        }
    }

    private static void CollectDescendants(ref readonly JsonElement node, List<JsonElement> list)
    {
        list.Add(node);

        if (node.ValueKind == JsonValueKind.Object)
        {
            list.EnsureCapacity(list.Count + node.GetPropertyCount());
            foreach (var prop in node.EnumerateObject())
            {
                JsonElement value = prop.Value;
                CollectDescendants(in value, list);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            list.EnsureCapacity(list.Count + node.GetArrayLength());
            foreach (var elem in node.EnumerateArray())
                CollectDescendants(in elem, list);
        }
    }

    // ── Selector Application ──────────────────────────────────────────────

    private static void ApplySelector(ISelector selector, ref readonly JsonElement node, ref readonly JsonElement root, List<JsonElement> results)
    {
        switch (selector)
        {
            case NameSelector ns:
                if (node.ValueKind == JsonValueKind.Object && node.TryGetProperty(ns.Name, out var prop))
                    results.Add(prop);
                break;

            case WildcardSelector:
                if (node.ValueKind == JsonValueKind.Object)
                {
                    int propCount = node.GetPropertyCount();
                    results.EnsureCapacity(results.Count + propCount);
                    foreach (var p in node.EnumerateObject())
                        results.Add(p.Value);
                }
                else if (node.ValueKind == JsonValueKind.Array)
                {
                    int arrayLen = node.GetArrayLength();
                    results.EnsureCapacity(results.Count + arrayLen);
                    foreach (var e in node.EnumerateArray())
                        results.Add(e);
                }
                break;

            case IndexSelector ix:
                if (node.ValueKind == JsonValueKind.Array)
                {
                    int len = node.GetArrayLength();
                    long effectiveIndex = ix.Index >= 0 ? ix.Index : len + ix.Index;
                    if (effectiveIndex >= 0 && effectiveIndex < len)
                        results.Add(node[(int)effectiveIndex]);
                }
                break;

            case SliceSelector sl:
                if (node.ValueKind == JsonValueKind.Array)
                    ApplySlice(sl, in node, results);
                break;

            case FilterSelector fs:
                ApplyFilter(fs, in node, in root, results);
                break;
        }
    }

    // ── Slice ─────────────────────────────────────────────────────────────

    private static void ApplySlice(SliceSelector sl, ref readonly JsonElement array, List<JsonElement> results)
    {
        int len = array.GetArrayLength();
        long step = sl.Step ?? 1;

        if (step == 0)
            return;

        long start = sl.Start ?? (step >= 0 ? 0 : len - 1);
        long end = sl.End ?? (step >= 0 ? len : -len - 1);

        long nStart = Normalize(start, len);
        long nEnd = Normalize(end, len);

        long lower, upper;
        if (step > 0)
        {
            lower = Math.Min(Math.Max(nStart, 0), len);
            upper = Math.Min(Math.Max(nEnd, 0), len);
        }
        else
        {
            upper = Math.Min(Math.Max(nStart, -1), len - 1);
            lower = Math.Min(Math.Max(nEnd, -1), len - 1);
        }

        // Pre-calculate count and pre-allocate capacity
        if (step > 0)
        {
            if (upper > lower)
            {
                long count = (upper - lower + step - 1) / step;
                results.EnsureCapacity(results.Count + (int)count);
                for (long i = lower; i < upper; i += step)
                    results.Add(array[(int)i]);
            }
        }
        else
        {
            if (upper > lower)
            {
                long count = (upper - lower + (-step) - 1) / (-step);
                results.EnsureCapacity(results.Count + (int)count);
                for (long i = upper; lower < i; i += step)
                    results.Add(array[(int)i]);
            }
        }
    }

    private static long Normalize(long i, int len) => i >= 0 ? i : len + i;

    // ── Filter ────────────────────────────────────────────────────────────

    private static void ApplyFilter(FilterSelector fs, ref readonly JsonElement node, ref readonly JsonElement root, List<JsonElement> results)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            int arrayLen = node.GetArrayLength();
            int startCount = results.Count;
            results.EnsureCapacity(startCount + arrayLen); // Reserve space for worst case

            foreach (var elem in node.EnumerateArray())
            {
                if (EvalLogical(fs.Expression, elem, root))
                    results.Add(elem);
            }
        }
        else if (node.ValueKind == JsonValueKind.Object)
        {
            int propCount = node.GetPropertyCount();
            int startCount = results.Count;
            results.EnsureCapacity(startCount + propCount); // Reserve space for worst case

            foreach (var prop in node.EnumerateObject())
            {
                if (EvalLogical(fs.Expression, prop.Value, root))
                    results.Add(prop.Value);
            }
        }
    }

    // ── Logical Expression Evaluation ─────────────────────────────────────

    private static bool EvalLogical(LogicalExpr expr, JsonElement current, JsonElement root)
    {
        return expr switch
        {
            OrExpr or => EvalOr(or, current, root),
            AndExpr and => EvalAnd(and, current, root),
            NotExpr not => !EvalLogical(not.Operand, current, root),
            ComparisonExpr cmp => EvalComparison(cmp, current, root),
            ExistenceExpr ex => EvalExistence(ex.Query, current, root),
            FunctionTestExpr ft => EvalFunctionTest(ft.Function, current, root),
            _ => false
        };
    }

    private static bool EvalOr(OrExpr or, JsonElement current, JsonElement root)
    {
        foreach (var operand in or.Operands)
        {
            if (EvalLogical(operand, current, root))
                return true;
        }
        return false;
    }

    private static bool EvalAnd(AndExpr and, JsonElement current, JsonElement root)
    {
        foreach (var operand in and.Operands)
        {
            if (!EvalLogical(operand, current, root))
                return false;
        }
        return true;
    }

    private static bool EvalExistence(FilterQuery query, JsonElement current, JsonElement root)
    {
        // Fast path: single non-descendant segment with a name or index selector.
        // This is the most common case: @.property or @[index].
        // Avoid allocating a List<JsonElement> just to check Count > 0.
        if (query.Segments is [SingleSelectorSegment { IsDescendant: false } seg])
        {
            var startNode = query.IsRelative ? current : root;
            switch (seg.Selector)
            {
                case NameSelector ns:
                    return startNode.ValueKind == JsonValueKind.Object
                        && startNode.TryGetProperty(ns.Name, out _);

                case IndexSelector ix:
                    if (startNode.ValueKind != JsonValueKind.Array) return false;
                    int len = startNode.GetArrayLength();
                    long effective = ix.Index >= 0 ? ix.Index : len + ix.Index;
                    return effective >= 0 && effective < len;

                case WildcardSelector:
                    return startNode.ValueKind switch
                    {
                        JsonValueKind.Object => startNode.GetPropertyCount() > 0,
                        JsonValueKind.Array => startNode.GetArrayLength() > 0,
                        _ => false
                    };
            }
        }

        var nodes = EvalFilterQuery(query, current, root);
        return nodes.Count > 0;
    }

    private static bool EvalFunctionTest(FunctionCall func, JsonElement current, JsonElement root)
    {
        var (resultType, value) = EvalFunction(func, current, root);

        return resultType switch
        {
            FunctionResultType.LogicalType => value is bool b && b,
            FunctionResultType.NodesType => value is List<JsonElement> list && list.Count > 0,
            _ => false
        };
    }

    // ── Comparison Evaluation ─────────────────────────────────────────────

    private static bool EvalComparison(ComparisonExpr cmp, JsonElement current, JsonElement root)
    {
        var (leftHasValue, leftValue) = ResolveComparable(cmp.Left, current, root);
        var (rightHasValue, rightValue) = ResolveComparable(cmp.Right, current, root);

        return cmp.Op switch
        {
            ComparisonOp.Eq => CmpEquals(leftHasValue, leftValue, rightHasValue, rightValue),
            ComparisonOp.Ne => !CmpEquals(leftHasValue, leftValue, rightHasValue, rightValue),
            ComparisonOp.Lt => CmpLessThan(leftHasValue, leftValue, rightHasValue, rightValue),
            ComparisonOp.Le => CmpLessThan(leftHasValue, leftValue, rightHasValue, rightValue)
                            || CmpEquals(leftHasValue, leftValue, rightHasValue, rightValue),
            ComparisonOp.Gt => CmpLessThan(rightHasValue, rightValue, leftHasValue, leftValue),
            ComparisonOp.Ge => CmpLessThan(rightHasValue, rightValue, leftHasValue, leftValue)
                            || CmpEquals(leftHasValue, leftValue, rightHasValue, rightValue),
            _ => false
        };
    }

    private static (bool HasValue, JsonElement Value) ResolveComparable(Comparable comp, JsonElement current, JsonElement root)
    {
        switch (comp)
        {
            case LiteralComparable lit:
                return lit.Value.HasValue ? (true, lit.Value.Value) : (false, default);

            case SingularQueryComparable sq:
                // Optimize: avoid allocating a list for singular queries
                return TryEvalSingularQuery(sq.Query, current, root);

            case FunctionComparable fc:
                var (_, funcResult) = EvalFunction(fc.Function, current, root);
                if (funcResult is JsonElement el)
                    return (true, el);
                return (false, default);

            default:
                return (false, default);
        }
    }

    // Optimized version that doesn't allocate a list
    private static (bool HasValue, JsonElement Value) TryEvalSingularQuery(SingularQuery query, JsonElement current, JsonElement root)
    {
        var node = query.IsRelative ? current : root;

        foreach (var seg in query.Segments)
        {
            switch (seg)
            {
                case SingularNameSegment ns:
                    if (node.ValueKind == JsonValueKind.Object && node.TryGetProperty(ns.Name, out var prop))
                        node = prop;
                    else
                        return (false, default);
                    break;

                case SingularIndexSegment ix:
                    if (node.ValueKind == JsonValueKind.Array)
                    {
                        int len = node.GetArrayLength();
                        long effective = ix.Index >= 0 ? ix.Index : len + ix.Index;
                        if (effective >= 0 && effective < len)
                            node = node[(int)effective];
                        else
                            return (false, default);
                    }
                    else
                    {
                        return (false, default);
                    }
                    break;
            }
        }

        return (true, node);
    }

    private static bool CmpEquals(bool leftHas, JsonElement left, bool rightHas, JsonElement right)
    {
        if (!leftHas && !rightHas) return true;
        if (!leftHas || !rightHas) return false;
        
        // Fast path for common primitive comparisons
        var leftKind = left.ValueKind;
        var rightKind = right.ValueKind;
        
        if (leftKind != rightKind) 
            return false;

        switch (leftKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return true;
                
            case JsonValueKind.Number:
                return CompareNumbers(left, right);
                
            case JsonValueKind.String:
                // ValueEquals avoids one string allocation compared to GetString() == GetString()
                return left.ValueEquals(right.GetString()!);
                
            case JsonValueKind.Array:
                return ArrayDeepEquals(left, right);
                
            case JsonValueKind.Object:
                return ObjectDeepEquals(left, right);
                
            default:
                return false;
        }
    }

    private static bool CmpLessThan(bool leftHas, JsonElement left, bool rightHas, JsonElement right)
    {
        if (!leftHas || !rightHas) return false;

        // Only numbers vs numbers, or strings vs strings
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
            // Fast path for integers
            if (left.TryGetInt32(out var leftInt) && right.TryGetInt32(out var rightInt))
                return leftInt < rightInt;
            
            return left.GetDouble() < right.GetDouble();
        }

        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.String)
        {
            return string.Compare(left.GetString(), right.GetString(), StringComparison.Ordinal) < 0;
        }

        return false;
    }

    private static bool DeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;

        return a.ValueKind switch
        {
            JsonValueKind.Null => true,
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            JsonValueKind.Number => CompareNumbers(a, b),
            JsonValueKind.String => a.ValueEquals(b.GetString()!),
            JsonValueKind.Array => ArrayDeepEquals(a, b),
            JsonValueKind.Object => ObjectDeepEquals(a, b),
            _ => false
        };
    }

    private static bool CompareNumbers(JsonElement a, JsonElement b)
    {
        // Fast path for integers
        if (a.TryGetInt32(out var aInt) && b.TryGetInt32(out var bInt))
            return aInt == bInt;

        // Try decimal for exact comparison first, fall back to double
        if (a.TryGetDecimal(out var ad) && b.TryGetDecimal(out var bd))
            return ad == bd;
        return a.GetDouble() == b.GetDouble();
    }

    private static bool ArrayDeepEquals(JsonElement a, JsonElement b)
    {
        int aLen = a.GetArrayLength();
        int bLen = b.GetArrayLength();
        if (aLen != bLen) return false;

        using var ae = a.EnumerateArray();
        using var be = b.EnumerateArray();
        while (ae.MoveNext() && be.MoveNext())
        {
            if (!DeepEquals(ae.Current, be.Current))
                return false;
        }
        return true;
    }

    private static bool ObjectDeepEquals(JsonElement a, JsonElement b)
    {
        int aCount = a.GetPropertyCount();
        int bCount = b.GetPropertyCount();
        
        if (aCount != bCount) 
            return false;

        // Build dictionary only for 'a', then look up in 'b'
        var aDict = new Dictionary<string, JsonElement>(aCount);
        foreach (var p in a.EnumerateObject())
            aDict[p.Name] = p.Value;

        foreach (var bProp in b.EnumerateObject())
        {
            if (!aDict.TryGetValue(bProp.Name, out var aVal))
                return false;
            if (!DeepEquals(aVal, bProp.Value))
                return false;
        }
        
        return true;
    }

    // ── Query Evaluation ──────────────────────────────────────────────────

    private static List<JsonElement> EvalFilterQuery(FilterQuery query, JsonElement current, JsonElement root)
    {
        var startNode = query.IsRelative ? current : root;
        var nodes = new List<JsonElement> { startNode };

        foreach (var segment in query.Segments)
        {
            var next = new List<JsonElement>(nodes.Count * 2); // Estimate capacity
            foreach (var node in nodes)
            {
                // Dispatch based on segment type
                switch (segment)
                {
                    case SingleSelectorSegment sing:
                        if (sing.IsDescendant)
                            EvaluateDescendantSingularSegment(sing.Selector, in node, in root, next);
                        else
                            ApplySelector(sing.Selector, in node, in root, next);
                        break;
                        
                    case MultiSelectorSegment multi:
                        if (multi.IsDescendant)
                            EvaluateDescendantMultiSegment(multi.Selectors, in node, in root, next);
                        else
                            EvaluateChildMultiSegment(multi.Selectors, in node, in root, next);
                        break;
                }
            }
            nodes = next;
        }

        return nodes;
    }

    private static List<JsonElement> EvalSingularQuery(SingularQuery query, JsonElement current, JsonElement root)
    {
        var node = query.IsRelative ? current : root;

        foreach (var seg in query.Segments)
        {
            switch (seg)
            {
                case SingularNameSegment ns:
                    if (node.ValueKind == JsonValueKind.Object && node.TryGetProperty(ns.Name, out var prop))
                        node = prop;
                    else
                        return [];
                    break;

                case SingularIndexSegment ix:
                    if (node.ValueKind == JsonValueKind.Array)
                    {
                        int len = node.GetArrayLength();
                        long effective = ix.Index >= 0 ? ix.Index : len + ix.Index;
                        if (effective >= 0 && effective < len)
                            node = node[(int)effective];
                        else
                            return [];
                    }
                    else
                    {
                        return [];
                    }
                    break;
            }
        }

        return [node];
    }

    // ── Function Evaluation ───────────────────────────────────────────────

    private static (FunctionResultType Type, object? Value) EvalFunction(FunctionCall func, JsonElement current, JsonElement root)
    {
        return func switch
        {
            MatchFunctionCall match => EvalMatch(match, current, root),
            SearchFunctionCall search => EvalSearch(search, current, root),
            _ => func.Name switch
            {
                "length" => EvalLength(func, current, root),
                "count" => EvalCount(func, current, root),
                "match" => EvalMatchDynamic(func, current, root),
                "search" => EvalSearchDynamic(func, current, root),
                "value" => EvalValueFunc(func, current, root),
                _ => (FunctionResultType.ValueType, null)
            }
        };
    }

    private static (FunctionResultType, object?) EvalLength(FunctionCall func, JsonElement current, JsonElement root)
    {
        var val = ResolveValueTypeArgument(func.Arguments[0], current, root);
        if (val is not JsonElement el)
            return (FunctionResultType.ValueType, null); // Nothing

        JsonElement? result = el.ValueKind switch
        {
            JsonValueKind.String => MakeJsonNumber(CountUnicodeScalarValues(el.GetString()!)),
            JsonValueKind.Array => MakeJsonNumber(el.GetArrayLength()),
            JsonValueKind.Object => MakeJsonNumber(el.GetPropertyCount()),
            _ => null
        };

        return (FunctionResultType.ValueType, result);
    }

    private static (FunctionResultType, object?) EvalCount(FunctionCall func, JsonElement current, JsonElement root)
    {
        var nodes = ResolveNodesTypeArgument(func.Arguments[0], current, root);
        return (FunctionResultType.ValueType, MakeJsonNumber(nodes.Count));
    }

    private static (FunctionResultType, object?) EvalMatch(MatchFunctionCall func, JsonElement current, JsonElement root)
    {
        var first = ResolveValueTypeArgument(func.Arguments[0], current, root);

        if (first is not JsonElement el1 || el1.ValueKind != JsonValueKind.String)
            return (FunctionResultType.LogicalType, false);

        string input = el1.GetString()!;

        // Use pre-compiled regex if available
        if (func.CompiledRegex != null)
        {
            try
            {
                bool matches = func.CompiledRegex.IsMatch(input);
                return (FunctionResultType.LogicalType, matches);
            }
            catch
            {
                return (FunctionResultType.LogicalType, false);
            }
        }

        // Fallback: pattern is dynamic (shouldn't happen if second arg is literal)
        var second = ResolveValueTypeArgument(func.Arguments[1], current, root);
        if (second is not JsonElement el2 || el2.ValueKind != JsonValueKind.String)
            return (FunctionResultType.LogicalType, false);

        string pattern = el2.GetString()!;
        try
        {
            string converted = IRegexpHelper.ConvertIRegexp(pattern);
            string anchored = $"^(?:{converted})$";
            var regex = IRegexpHelper.TryGetCachedRegex(anchored);
            if (regex == null)
                return (FunctionResultType.LogicalType, false);

            bool matches = regex.IsMatch(input);
            return (FunctionResultType.LogicalType, matches);
        }
        catch
        {
            return (FunctionResultType.LogicalType, false);
        }
    }

    private static (FunctionResultType, object?) EvalSearch(SearchFunctionCall func, JsonElement current, JsonElement root)
    {
        var first = ResolveValueTypeArgument(func.Arguments[0], current, root);

        if (first is not JsonElement el1 || el1.ValueKind != JsonValueKind.String)
            return (FunctionResultType.LogicalType, false);

        string input = el1.GetString()!;

        // Use pre-compiled regex if available
        if (func.CompiledRegex != null)
        {
            try
            {
                bool found = func.CompiledRegex.IsMatch(input);
                return (FunctionResultType.LogicalType, found);
            }
            catch
            {
                return (FunctionResultType.LogicalType, false);
            }
        }

        // Fallback: pattern is dynamic (shouldn't happen if second arg is literal)
        var second = ResolveValueTypeArgument(func.Arguments[1], current, root);
        if (second is not JsonElement el2 || el2.ValueKind != JsonValueKind.String)
            return (FunctionResultType.LogicalType, false);

        string pattern = el2.GetString()!;
        try
        {
            string converted = IRegexpHelper.ConvertIRegexp(pattern);
            var regex = IRegexpHelper.TryGetCachedRegex(converted);
            if (regex == null)
                return (FunctionResultType.LogicalType, false);

            bool found = regex.IsMatch(input);
            return (FunctionResultType.LogicalType, found);
        }
        catch
        {
            return (FunctionResultType.LogicalType, false);
        }
    }

    /// <summary>
    /// Evaluates match() when the pattern is not a compile-time literal.
    /// </summary>
    private static (FunctionResultType, object?) EvalMatchDynamic(FunctionCall func, JsonElement current, JsonElement root)
    {
        var first = ResolveValueTypeArgument(func.Arguments[0], current, root);
        var second = ResolveValueTypeArgument(func.Arguments[1], current, root);

        if (first is not JsonElement el1 || el1.ValueKind != JsonValueKind.String
            || second is not JsonElement el2 || el2.ValueKind != JsonValueKind.String)
            return (FunctionResultType.LogicalType, false);

        string input = el1.GetString()!;
        string pattern = el2.GetString()!;

        try
        {
            string converted = IRegexpHelper.ConvertIRegexp(pattern);
            string anchored = $"^(?:{converted})$";
            var regex = IRegexpHelper.TryGetCachedRegex(anchored);
            if (regex == null)
                return (FunctionResultType.LogicalType, false);

            bool matches = regex.IsMatch(input);
            return (FunctionResultType.LogicalType, matches);
        }
        catch
        {
            return (FunctionResultType.LogicalType, false);
        }
    }

    /// <summary>
    /// Evaluates search() when the pattern is not a compile-time literal.
    /// </summary>
    private static (FunctionResultType, object?) EvalSearchDynamic(FunctionCall func, JsonElement current, JsonElement root)
    {
        var first = ResolveValueTypeArgument(func.Arguments[0], current, root);
        var second = ResolveValueTypeArgument(func.Arguments[1], current, root);

        if (first is not JsonElement el1 || el1.ValueKind != JsonValueKind.String
            || second is not JsonElement el2 || el2.ValueKind != JsonValueKind.String)
            return (FunctionResultType.LogicalType, false);

        string input = el1.GetString()!;
        string pattern = el2.GetString()!;

        try
        {
            string converted = IRegexpHelper.ConvertIRegexp(pattern);
            var regex = IRegexpHelper.TryGetCachedRegex(converted);
            if (regex == null)
                return (FunctionResultType.LogicalType, false);

            bool found = regex.IsMatch(input);
            return (FunctionResultType.LogicalType, found);
        }
        catch
        {
            return (FunctionResultType.LogicalType, false);
        }
    }

    private static (FunctionResultType, object?) EvalValueFunc(FunctionCall func, JsonElement current, JsonElement root)
    {
        var nodes = ResolveNodesTypeArgument(func.Arguments[0], current, root);
        if (nodes.Count == 1)
            return (FunctionResultType.ValueType, nodes[0]);
        return (FunctionResultType.ValueType, null); // Nothing
    }

    // ── Argument Resolution ───────────────────────────────────────────────

    private static object? ResolveValueTypeArgument(IFunctionArgument arg, JsonElement current, JsonElement root)
    {
        switch (arg)
        {
            case LiteralArgument lit:
                return lit.Value.HasValue ? lit.Value.Value : (object?)null;

            case FilterQueryArgument fq:
                // Fast path: for singular paths (the common case for ValueType arguments
                // like length(@.title)), avoid allocating a List.
                var startNode = fq.Query.IsRelative ? current : root;
                if (TryEvaluateSingularPath(fq.Query.Segments, in startNode, out var singular))
                    return singular.Length == 1 ? singular[0] : (object?)null;
                // General path
                var nodes = EvalFilterQuery(fq.Query, current, root);
                return nodes.Count == 1 ? nodes[0] : (object?)null;

            case FunctionCallArgument fc:
                var (_, result) = EvalFunction(fc.Call, current, root);
                return result;

            default:
                return null;
        }
    }

    private static List<JsonElement> ResolveNodesTypeArgument(IFunctionArgument arg, JsonElement current, JsonElement root)
    {
        switch (arg)
        {
            case FilterQueryArgument fq:
                return EvalFilterQuery(fq.Query, current, root);

            case FunctionCallArgument fc:
                var (_, result) = EvalFunction(fc.Call, current, root);
                if (result is List<JsonElement> list) return list;
                return [];

            default:
                return [];
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // Cache for small integers to avoid repeated JsonDocument.Parse calls
    private static readonly JsonElement[] _cachedNumbers = CreateCachedNumbers();

    private static JsonElement[] CreateCachedNumbers()
    {
        var cache = new JsonElement[256];
        for (int i = 0; i < 256; i++)
        {
            cache[i] = JsonDocument.Parse(i.ToString()).RootElement.Clone();
        }
        return cache;
    }

    private static JsonElement MakeJsonNumber(int value)
    {
        if (value >= 0 && value < 256)
            return _cachedNumbers[value];
        return JsonDocument.Parse(value.ToString()).RootElement.Clone();
    }

    /// <summary>
    /// Counts the number of Unicode scalar values (code points) in a string.
    /// Surrogate pairs represent a single code point, so each pair is counted as 1.
    /// This is faster than <c>EnumerateRunes().Count()</c>.
    /// </summary>
    private static int CountUnicodeScalarValues(string s)
    {
        int count = s.Length;
        // Each high surrogate is the first char of a surrogate pair (one code point = 2 chars).
        // Subtract one for each high surrogate to get the code point count.
        foreach (char c in s)
        {
            if (char.IsHighSurrogate(c))
                count--;
        }
        return count;
    }
}
