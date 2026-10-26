using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record FunctionComparable(FunctionCall Function) : Comparable
{
    internal override JsonElement? GetValue(SelectorContext selectorContext) => Function.EvaluateValue(selectorContext);
}
