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
                if (segment.IsDescendant)
                    EvaluateDescendantSegment(segment, in node, in root, next);
                else
                    EvaluateChildSegment(segment, in node, in root, next);
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
            if (segment.IsDescendant || segment.Selectors.Length != 1)
            {
                result = default!;
                return false;
            }

            var selector = segment.Selectors[0];
            if (selector is not NameSelector and not IndexSelector)
            {
                result = default!;
                return false;
            }
        }

        var node = root;
        foreach (var segment in segments)
        {
            switch (segment.Selectors[0])
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

    private static void EvaluateChildSegment(Segment segment, ref readonly JsonElement node, ref readonly JsonElement root, List<JsonElement> results)
    {
        foreach (var selector in segment.Selectors)
        {
            ApplySelector(selector, in node, in root, results);
        }
    }

    private static void EvaluateDescendantSegment(Segment segment, ref readonly JsonElement node, ref readonly JsonElement root, List<JsonElement> results)
    {
        // Visit the node and all its descendants in pre-order.
        // For each visited node, apply the child selectors.
        var descendants = new List<JsonElement>();
        CollectDescendants(in node, descendants);

        foreach (var desc in descendants)
        {
            foreach (var selector in segment.Selectors)
            {
                ApplySelector(selector, in desc, in root, results);
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
                    foreach (var p in node.EnumerateObject())
                        results.Add(p.Value);
                }
                else if (node.ValueKind == JsonValueKind.Array)
                {
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

        if (step > 0)
        {
            for (long i = lower; i < upper; i += step)
                results.Add(array[(int)i]);
        }
        else
        {
            for (long i = upper; lower < i; i += step)
                results.Add(array[(int)i]);
        }
    }

    private static long Normalize(long i, int len) => i >= 0 ? i : len + i;

    // ── Filter ────────────────────────────────────────────────────────────

    private static void ApplyFilter(FilterSelector fs, ref readonly JsonElement node, ref readonly JsonElement root, List<JsonElement> results)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in node.EnumerateArray())
            {
                if (EvalLogical(fs.Expression, elem, root))
                    results.Add(elem);
            }
        }
        else if (node.ValueKind == JsonValueKind.Object)
        {
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
            OrExpr or => or.Operands.Any(op => EvalLogical(op, current, root)),
            AndExpr and => and.Operands.All(op => EvalLogical(op, current, root)),
            NotExpr not => !EvalLogical(not.Operand, current, root),
            ComparisonExpr cmp => EvalComparison(cmp, current, root),
            ExistenceExpr ex => EvalExistence(ex.Query, current, root),
            FunctionTestExpr ft => EvalFunctionTest(ft.Function, current, root),
            _ => false
        };
    }

    private static bool EvalExistence(FilterQuery query, JsonElement current, JsonElement root)
    {
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
            ComparisonOp.Le => CmpLessThan(leftHasValue, leftValue, rightHasValue, rightValue) || CmpEquals(leftHasValue, leftValue, rightHasValue, rightValue),
            ComparisonOp.Gt => CmpLessThan(rightHasValue, rightValue, leftHasValue, leftValue),
            ComparisonOp.Ge => CmpLessThan(rightHasValue, rightValue, leftHasValue, leftValue) || CmpEquals(leftHasValue, leftValue, rightHasValue, rightValue),
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
                var nodes = EvalSingularQuery(sq.Query, current, root);
                return nodes.Count == 1 ? (true, nodes[0]) : (false, default);

            case FunctionComparable fc:
                var (_, funcResult) = EvalFunction(fc.Function, current, root);
                if (funcResult is JsonElement el)
                    return (true, el);
                return (false, default);

            default:
                return (false, default);
        }
    }

    private static bool CmpEquals(bool leftHas, JsonElement left, bool rightHas, JsonElement right)
    {
        if (!leftHas && !rightHas) return true;
        if (!leftHas || !rightHas) return false;
        return DeepEquals(left, right);
    }

    private static bool CmpLessThan(bool leftHas, JsonElement left, bool rightHas, JsonElement right)
    {
        if (!leftHas || !rightHas) return false;

        // Only numbers vs numbers, or strings vs strings
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
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
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Array => ArrayDeepEquals(a, b),
            JsonValueKind.Object => ObjectDeepEquals(a, b),
            _ => false
        };
    }

    private static bool CompareNumbers(JsonElement a, JsonElement b)
    {
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
        var aDict = new Dictionary<string, JsonElement>();
        foreach (var p in a.EnumerateObject())
            aDict[p.Name] = p.Value;

        var bDict = new Dictionary<string, JsonElement>();
        foreach (var p in b.EnumerateObject())
            bDict[p.Name] = p.Value;

        if (aDict.Count != bDict.Count) return false;

        foreach (var (key, aVal) in aDict)
        {
            if (!bDict.TryGetValue(key, out var bVal))
                return false;
            if (!DeepEquals(aVal, bVal))
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
            var next = new List<JsonElement>();
            foreach (var node in nodes)
            {
                if (segment.IsDescendant)
                    EvaluateDescendantSegment(segment, in node, in root, next);
                else
                    EvaluateChildSegment(segment, in node, in root, next);
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
        return func.Name switch
        {
            "length" => EvalLength(func, current, root),
            "count" => EvalCount(func, current, root),
            "match" => EvalMatch(func, current, root),
            "search" => EvalSearch(func, current, root),
            "value" => EvalValueFunc(func, current, root),
            _ => (FunctionResultType.ValueType, null)
        };
    }

    private static (FunctionResultType, object?) EvalLength(FunctionCall func, JsonElement current, JsonElement root)
    {
        var val = ResolveValueTypeArgument(func.Arguments[0], current, root);
        if (val is not JsonElement el)
            return (FunctionResultType.ValueType, null); // Nothing

        JsonElement? result = el.ValueKind switch
        {
            JsonValueKind.String => MakeJsonNumber(el.GetString()!.EnumerateRunes().Count()),
            JsonValueKind.Array => MakeJsonNumber(el.GetArrayLength()),
            JsonValueKind.Object => MakeJsonNumber(CountObjectMembers(el)),
            _ => null
        };

        return (FunctionResultType.ValueType, result);
    }

    private static (FunctionResultType, object?) EvalCount(FunctionCall func, JsonElement current, JsonElement root)
    {
        var nodes = ResolveNodesTypeArgument(func.Arguments[0], current, root);
        return (FunctionResultType.ValueType, MakeJsonNumber(nodes.Count));
    }

    private static (FunctionResultType, object?) EvalMatch(FunctionCall func, JsonElement current, JsonElement root)
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
            string converted = ConvertIRegexp(pattern);
            string anchored = $"^(?:{converted})$";
            bool matches = Regex.IsMatch(input, anchored, RegexOptions.None, TimeSpan.FromSeconds(1));
            return (FunctionResultType.LogicalType, matches);
        }
        catch
        {
            return (FunctionResultType.LogicalType, false);
        }
    }

    private static (FunctionResultType, object?) EvalSearch(FunctionCall func, JsonElement current, JsonElement root)
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
            string converted = ConvertIRegexp(pattern);
            bool found = Regex.IsMatch(input, converted, RegexOptions.None, TimeSpan.FromSeconds(1));
            return (FunctionResultType.LogicalType, found);
        }
        catch
        {
            return (FunctionResultType.LogicalType, false);
        }
    }

    /// <summary>
    /// Converts an I-Regexp (RFC 9485) pattern to a .NET Regex pattern.
    /// In I-Regexp, <c>.</c> matches any code point except <c>\n</c> and <c>\r</c>,
    /// including supplementary plane characters (surrogate pairs in UTF-16).
    /// </summary>
    private static string ConvertIRegexp(string pattern)
    {
        var sb = new StringBuilder(pattern.Length * 2);
        bool inCharClass = false;

        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];

            if (c == '\\' && i + 1 < pattern.Length)
            {
                // Escaped character — pass through as-is
                sb.Append(c);
                sb.Append(pattern[i + 1]);
                i++;
                continue;
            }

            if (c == '[' && !inCharClass)
            {
                inCharClass = true;
                sb.Append(c);
                continue;
            }

            if (c == ']' && inCharClass)
            {
                inCharClass = false;
                sb.Append(c);
                continue;
            }

            if (c == '.' && !inCharClass)
            {
                // I-Regexp dot: any code point except \n and \r, including surrogates
                sb.Append("(?:[^\\n\\r\\uD800-\\uDFFF]|[\\uD800-\\uDBFF][\\uDC00-\\uDFFF])");
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
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
                // Used as singular query for ValueType
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

    private static JsonElement MakeJsonNumber(int value)
    {
        return JsonDocument.Parse(value.ToString()).RootElement.Clone();
    }

    private static int CountObjectMembers(JsonElement obj)
    {
        int count = 0;
        foreach (var _ in obj.EnumerateObject())
            count++;
        return count;
    }
}
