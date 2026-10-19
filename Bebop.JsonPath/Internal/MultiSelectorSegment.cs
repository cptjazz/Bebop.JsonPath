namespace Bebop.JsonPath.Internal;

/// <summary>
/// Segment with multiple selectors - requires iteration.
/// </summary>
internal sealed record MultiSelectorSegment(ISelector[] Selectors, bool IsDescendant) : Segment(IsDescendant)
{
    internal override void Evaluate(in SelectorContext context, ResultCollector results)
    {
        foreach (var selector in Selectors)
        {
            selector.Evaluate(context, results);
        }
    }
}
