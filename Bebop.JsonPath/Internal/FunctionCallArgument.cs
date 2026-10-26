using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record FunctionCallArgument(FunctionCall Call) : IFunctionArgument
{
    public JsonElement? GetValue(in SelectorContext selectorContext)
    {
        return Call.EvaluateValue(selectorContext);
    }
}
