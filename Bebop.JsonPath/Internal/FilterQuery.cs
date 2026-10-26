using System.Text.Json;

namespace Bebop.JsonPath.Internal;

/// <summary>
/// A query used in existence tests. Either relative (@-rooted) or absolute ($-rooted);
/// the variant is chosen at parse time via <see cref="Create"/>.
/// </summary>
internal abstract record FilterQuery
{
    private protected readonly JsonPath Path;

    private protected FilterQuery(IReadOnlyList<Segment> segments, ReadOnlyMemory<char> original)
    {
        Path = new JsonPath(segments, original);
    }

    internal static FilterQuery Create(bool isRelative, IReadOnlyList<Segment> segments, ReadOnlyMemory<char> original)
    {
        return isRelative
            ? new RelativeFilterQuery(segments, original)
            : new RootFilterQuery(segments, original);
    }

    internal bool Evaluate(SelectorContext selectorContext)
    {
        return GetNodes(selectorContext).Count > 0;
    }

    internal abstract IReadOnlyList<JsonElement> GetNodes(SelectorContext selectorContext);
}

/// <summary>
/// A query starting at the current node (@).
/// </summary>
internal sealed record RelativeFilterQuery(IReadOnlyList<Segment> Segments, ReadOnlyMemory<char> Original) : FilterQuery(Segments, Original)
{
    internal override IReadOnlyList<JsonElement> GetNodes(SelectorContext selectorContext)
    {
        return Path.Evaluate(selectorContext.Current);
    }
}

/// <summary>
/// A query starting at the document root ($).
/// </summary>
internal sealed record RootFilterQuery(IReadOnlyList<Segment> Segments, ReadOnlyMemory<char> Original) : FilterQuery(Segments, Original)
{
    internal override IReadOnlyList<JsonElement> GetNodes(SelectorContext selectorContext)
    {
        return Path.Evaluate(selectorContext.Root);
    }
}
