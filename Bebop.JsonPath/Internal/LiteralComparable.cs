using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record LiteralComparable(JsonElement? Value) : Comparable
{
    internal override JsonElement? GetValue(SelectorContext selectorContext) => Value;
}
