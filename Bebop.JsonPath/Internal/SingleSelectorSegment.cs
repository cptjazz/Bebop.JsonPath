namespace Bebop.JsonPath.Internal;

/// <summary>
/// Segment with a single selector - optimized path without array iteration.
/// </summary>
internal sealed class SingleSelectorSegment(ISelector Selector, bool IsDescendant) : Segment(IsDescendant)
{
    internal override void Evaluate(in SelectorContext context, ResultCollector results)
    {
        Selector.Evaluate(context, results);
    }
}
