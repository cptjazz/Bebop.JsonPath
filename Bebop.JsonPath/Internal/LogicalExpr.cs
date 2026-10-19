namespace Bebop.JsonPath.Internal;

internal abstract class LogicalExpr
{
    internal abstract bool Evaluate(in SelectorContext selectorContext);
}
