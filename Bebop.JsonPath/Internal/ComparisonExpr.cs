using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed class ComparisonExpr(Comparable Left, ComparisonOp Op, Comparable Right) : LogicalExpr
{
    internal override bool Evaluate(in SelectorContext selectorContext)
    {
        var left = Left.GetValue(selectorContext);
        var right = Right.GetValue(selectorContext);

        return Op switch
        {
            ComparisonOp.Eq => _Equal(left, right),
            ComparisonOp.Ne => !_Equal(left, right),
            ComparisonOp.Lt => _Less(left, right),
            ComparisonOp.Le => _Less(left, right) || _Equal(left, right),
            ComparisonOp.Gt => _Less(right, left),
            ComparisonOp.Ge => _Less(right, left) || _Equal(left, right),

            _ => throw new InvalidOperationException($"Unknown comparison operator: {Op}")
        };
    }

    private static bool _Equal(JsonElement? left, JsonElement? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return _DeepEquals(left.Value, right.Value);
    }

    private static bool _Less(JsonElement? left, JsonElement? right)
    {
        if (left is not { } l || right is not { } r)
            return false;

        if (l.ValueKind == JsonValueKind.Number && r.ValueKind == JsonValueKind.Number)
            return l.GetDouble() < r.GetDouble();

        if (l.ValueKind == JsonValueKind.String && r.ValueKind == JsonValueKind.String)
            return _CompareCodePoints(l.GetString()!, r.GetString()!) < 0;

        return false;
    }

    private static int _CompareCodePoints(string a, string b)
    {
        var ra = a.EnumerateRunes().GetEnumerator();
        var rb = b.EnumerateRunes().GetEnumerator();
        while (true)
        {
            var hasA = ra.MoveNext();
            var hasB = rb.MoveNext();
            if (!hasA || !hasB)
                return hasA == hasB ? 0 : hasA ? 1 : -1;

            var cmp = ra.Current.Value.CompareTo(rb.Current.Value);
            if (cmp != 0)
                return cmp;
        }
    }

    private static bool _DeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        switch (a.ValueKind)
        {
            case JsonValueKind.Number:
                return a.GetDouble() == b.GetDouble();
            case JsonValueKind.String:
                return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Array:
                if (a.GetArrayLength() != b.GetArrayLength())
                    return false;
                using (var ea = a.EnumerateArray().GetEnumerator())
                using (var eb = b.EnumerateArray().GetEnumerator())
                {
                    while (ea.MoveNext() && eb.MoveNext())
                    {
                        if (!_DeepEquals(ea.Current, eb.Current))
                            return false;
                    }
                }
                return true;
            case JsonValueKind.Object:
                var count = 0;
                foreach (var property in a.EnumerateObject())
                {
                    count++;
                    if (!b.TryGetProperty(property.Name, out var other) || !_DeepEquals(property.Value, other))
                        return false;
                }
                return count == b.EnumerateObject().Count();
            default:
                return true; // true, false, null: kinds already equal
        }
    }
}
