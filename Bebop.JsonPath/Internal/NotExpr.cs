namespace Bebop.JsonPath.Internal;

internal sealed class NotExpr(LogicalExpr Operand) : LogicalExpr
{
    internal override bool Evaluate(in SelectorContext selectorContext)
    {
        return !Operand.Evaluate(selectorContext);
    }
}
