namespace Bebop.JsonPath.Internal;

/// <summary>
/// A query used in existence tests. Can be relative (@-rooted) or absolute ($-rooted).
/// </summary>
internal sealed record FilterQuery(bool IsRelative, Segment[] Segments)
{
    internal bool Evaluate(SelectorContext selectorContext)
    {
        return IsRelative
            ? selectorContext.Current.Evaluate(Segments)
            : selectorContext.Root?.Value.Evaluate(Segments) ?? false;
    }
}
