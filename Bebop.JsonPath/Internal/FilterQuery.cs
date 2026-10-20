namespace Bebop.JsonPath.Internal;

/// <summary>
/// A query used in existence tests. Can be relative (@-rooted) or absolute ($-rooted).
/// </summary>
internal sealed record FilterQuery
{
    private readonly bool isRelative;
    private readonly JsonPath path;

    public FilterQuery(bool isRelative, Segment[] segments, string original)
    {
        this.isRelative = isRelative;
        path = new JsonPath(segments, original);
    }

    internal bool Evaluate(SelectorContext selectorContext)
    {
        var node = isRelative
            ? selectorContext.Current
            : selectorContext.Root;

        return path.Evaluate(node).Length > 0;
    }
}
