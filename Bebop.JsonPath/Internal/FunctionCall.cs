using System.Text.Json;
using System.Globalization;

namespace Bebop.JsonPath.Internal;

internal abstract record FunctionCall(string Name, IReadOnlyList<IFunctionArgument> Arguments)
{
    /// <summary>
    /// Evaluates a function with LogicalType result (match, search).
    /// Functions with other result types return false.
    /// </summary>
    internal virtual bool Evaluate(SelectorContext selectorContext) => false;

    /// <summary>
    /// Evaluates a function with ValueType result (length, count, value).
    /// Returns null for "Nothing".
    /// </summary>
    internal virtual JsonElement? EvaluateValue(SelectorContext selectorContext) => null;

    protected static JsonElement? GetValue(IFunctionArgument argument, SelectorContext selectorContext)
    {
        return argument.GetValue(selectorContext);
    }

    protected static JsonElement CreateNumber(int value)
    {
        using var document = JsonDocument.Parse(value.ToString(CultureInfo.InvariantCulture));
        return document.RootElement.Clone();
    }
}
