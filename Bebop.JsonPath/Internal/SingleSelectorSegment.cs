namespace Bebop.JsonPath.Internal;

/// <summary>
/// Segment with a single selector - optimized path without array iteration.
/// </summary>
internal sealed record SingleSelectorSegment(ISelector Selector, bool IsDescendant) : Segment(IsDescendant);
