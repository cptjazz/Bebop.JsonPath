using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record LiteralArgument(JsonElement? Value) : IFunctionArgument
{
    public JsonElement? GetValue(in SelectorContext selectorContext)
    {
        return Value;
    }
}
