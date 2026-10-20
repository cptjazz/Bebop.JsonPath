namespace Bebop.JsonPath.Internal;

internal sealed record FunctionComparable(FunctionCall Function) : Comparable
{
    internal override int Compare(SelectorContext selectorContext, Comparable right)
    {
        return -1; // TODO;
    }
}
