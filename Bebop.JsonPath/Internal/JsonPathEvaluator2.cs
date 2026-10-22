using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal static class JsonPathEvaluator2
{
    internal static IEnumerable<JsonElement> Evaluate(ReadOnlySpan<Segment> segments, in JsonElement root)
    {
        IEnumerable<JsonElement> nodeSet = [root];

        foreach (var segment in segments)
        {
            var c = new ResultCollector();

            foreach (var n in nodeSet)
            {
                segment.Evaluate(new SelectorContext(n, root), c);
            }

            nodeSet = c.Results;
        }

        return [.. nodeSet]; // materialize IEnumerable!
    }
}
