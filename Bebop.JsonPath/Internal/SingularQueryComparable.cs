using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record SingularQueryComparable(SingularQuery Query) : Comparable
{
    internal override JsonElement? GetValue(SelectorContext selectorContext)
    {
        var current = Query.IsRelative ? selectorContext.Current : selectorContext.Root;

        foreach (var segment in Query.Segments)
        {
            if (!segment.Evaluate(current, out current))
                return null;
        }

        return current;
    }
}
