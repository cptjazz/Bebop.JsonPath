using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record FilterQueryArgument(FilterQuery Query) : IFunctionArgument
{
    public JsonElement? GetValue(in SelectorContext selectorContext)
    {
        var nodes = Query.GetNodes(selectorContext);
        return nodes.Count == 1 ? nodes[0] : null;
    }
}
