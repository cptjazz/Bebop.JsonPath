namespace Bebop.JsonPath.Internal;

internal abstract record Comparable
{
    internal abstract int Compare(SelectorContext selectorContext, Comparable right);
}