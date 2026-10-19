namespace Bebop.JsonPath.Internal;

internal sealed class OrExpr(LogicalExpr[] Operands) : LogicalExpr
{
    internal override bool Evaluate(in SelectorContext selectorContext)
    {
        foreach (var operand in Operands)
        {
            if (operand.Evaluate(selectorContext))
                return true;
        }

        return false;
    }
}
