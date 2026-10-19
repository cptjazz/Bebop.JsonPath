namespace Bebop.JsonPath.Internal;

/// <summary>
/// Segment with multiple selectors - requires iteration.
/// </summary>
internal sealed record MultiSelectorSegment(ISelector[] Selectors, bool IsDescendant) : Segment(IsDescendant);
