using System.Text.Json;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Function call for value().
/// </summary>
internal sealed record ValueFunctionCall(IReadOnlyList<IFunctionArgument> Arguments)
    : FunctionCall("value", Arguments)
{
    internal override JsonElement? EvaluateValue(SelectorContext selectorContext)
    {
        return Arguments[0] is FilterQueryArgument arg
            ? arg.GetValue(selectorContext)
            : null;
    }
}
