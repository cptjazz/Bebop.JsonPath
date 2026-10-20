namespace Bebop.JsonPath.Internal;

internal sealed record SingularQueryComparable(SingularQuery Query) : Comparable
{
    internal override int Compare(SelectorContext selectorContext, Comparable right)
    {
        return -1; // TODO;
    }
}
