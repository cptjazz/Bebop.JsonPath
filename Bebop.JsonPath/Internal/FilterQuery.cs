namespace Bebop.JsonPath.Internal;

// ── Queries ───────────────────────────────────────────────────────────────────

/// <summary>
/// A query used in existence tests. Can be relative (@-rooted) or absolute ($-rooted).
/// </summary>
internal sealed record FilterQuery(bool IsRelative, Segment[] Segments);
