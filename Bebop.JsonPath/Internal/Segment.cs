namespace Bebop.JsonPath.Internal;

internal abstract record Segment(bool IsDescendant)
{
    internal abstract void Evaluate(in SelectorContext context, ResultCollector results);
}
