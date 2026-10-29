using System.Text.Json;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Function call for length().
/// </summary>
internal sealed record LengthFunctionCall(IReadOnlyList<IFunctionArgument> Arguments)
    : FunctionCall("length", Arguments)
{
    internal override JsonElement? EvaluateValue(SelectorContext selectorContext)
    {
        if (GetValue(Arguments[0], selectorContext) is not { } v)
            return null;

        return v.ValueKind switch
        {
            JsonValueKind.String => CreateNumber(v.GetString()!.EnumerateRunes().Count()),
            JsonValueKind.Array => CreateNumber(v.GetArrayLength()),
            JsonValueKind.Object => CreateNumber(v.EnumerateObject().Count()),
            _ => null
        };
    }
}
