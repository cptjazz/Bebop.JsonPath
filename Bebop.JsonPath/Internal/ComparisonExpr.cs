namespace Bebop.JsonPath.Internal;

internal sealed class ComparisonExpr(Comparable Left, ComparisonOp Op, Comparable Right) : LogicalExpr
{
    internal override bool Evaluate(in SelectorContext selectorContext)
    {
        return Op switch
        {
            ComparisonOp.Eq=> Left.Compare(selectorContext, Right) == 0,
            ComparisonOp.Ne=> Left.Compare(selectorContext, Right) != 0,
            ComparisonOp.Lt => Left.Compare(selectorContext, Right) < 0,
            ComparisonOp.Le => Left.Compare(selectorContext, Right) <= 0,
            ComparisonOp.Gt => Left.Compare(selectorContext, Right) > 0,
            ComparisonOp.Ge => Left.Compare(selectorContext, Right) >= 0,

            _ => throw new InvalidOperationException($"Unknown comparison operator: {Op}")
        };
    }
}
