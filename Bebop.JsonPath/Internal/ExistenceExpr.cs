namespace Bebop.JsonPath.Internal;

internal sealed class ExistenceExpr(FilterQuery Query) : LogicalExpr
{
    internal override bool Evaluate(in SelectorContext selectorContext)
    {
        return Query.Evaluate(selectorContext);
    }
}
