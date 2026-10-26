using System.Text.Json;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// Function call for count().
/// </summary>
internal sealed record CountFunctionCall(IReadOnlyList<IFunctionArgument> Arguments)
    : FunctionCall("count", Arguments)
{
    internal override JsonElement? EvaluateValue(SelectorContext selectorContext)
    {
        return Arguments[0] is FilterQueryArgument arg
            ? JsonSerializer.SerializeToElement(arg.Query.GetNodes(selectorContext).Count)
            : null;
    }
}
