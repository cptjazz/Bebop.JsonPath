using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record LiteralComparable(JsonElement? Value) : Comparable
{
    internal override int Compare(SelectorContext selectorContext, Comparable right)
    {
        return -1; // TODO;
    }
}
