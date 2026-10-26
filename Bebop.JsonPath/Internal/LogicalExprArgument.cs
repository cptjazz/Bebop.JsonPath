using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed record LogicalExprArgument(LogicalExpr Expr) : IFunctionArgument
{
    public JsonElement? GetValue(in SelectorContext selectorContext)
    {
        throw new NotImplementedException();
    }
}
