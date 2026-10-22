namespace Bebop.JsonPath.Internal;

internal abstract class Segment(bool IsDescendant)
{
    internal abstract void Evaluate(in SelectorContext context, ResultCollector results);
}
