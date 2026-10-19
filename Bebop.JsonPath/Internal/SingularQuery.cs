namespace Bebop.JsonPath.Internal;

/// <summary>
/// A singular query (guaranteed to produce at most one node).
/// Used in comparisons.
/// </summary>
internal sealed record SingularQuery(bool IsRelative, SingularSegment[] Segments);
