namespace Bebop.JsonPath.Internal;

internal sealed class FunctionTestExpr(FunctionCall Function) : LogicalExpr
{
    internal override bool Evaluate(in SelectorContext selectorContext)
    {
        return Function.Evaluate(selectorContext);
    }
}
