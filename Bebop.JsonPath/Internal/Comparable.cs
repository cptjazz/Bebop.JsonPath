using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal abstract record Comparable
{
    /// <summary>
    /// Gets the value to compare, or null for "Nothing" (empty nodelist / no result).
    /// </summary>
    internal abstract JsonElement? GetValue(SelectorContext selectorContext);
}
