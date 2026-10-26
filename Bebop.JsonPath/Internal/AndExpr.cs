namespace Bebop.JsonPath.Internal;

internal sealed class AndExpr(IReadOnlyList<LogicalExpr> Operands) : LogicalExpr
{
    internal override bool Evaluate(in SelectorContext selectorContext)
    {
        foreach (var operand in Operands)
        {
            if (!operand.Evaluate(selectorContext))
                return false;
        }

        return true;
    }
}
