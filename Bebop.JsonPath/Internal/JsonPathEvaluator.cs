using System.Runtime.InteropServices;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal static class JsonPathEvaluator
{
    internal static IReadOnlyList<JsonElement> Evaluate(IReadOnlyList<Segment> segments, in JsonElement root)
    {
        var countHelper = new CountHelper();

        ReadOnlySpan<JsonElement> nodeSet = [root];
        
        foreach (var segment in segments)
        {
            var c = new ResultCollector(countHelper);

            if (segment.IsDescendant)
                nodeSet = CollectionsMarshal.AsSpan(_ExpandNodeSet(nodeSet));

            foreach (var n in nodeSet)
            {
                segment.Evaluate(new SelectorContext(n, root), c);
            }

            nodeSet = c.Results;
        }

        return [.. nodeSet]; // materialize IEnumerable!
    }

    private static List<JsonElement> _ExpandNodeSet(ReadOnlySpan<JsonElement> nodeSet)
    {
        var newNodes = new List<JsonElement>(nodeSet.Length);

        static void _Expand(JsonElement current, List<JsonElement> nodes)
        {
            nodes.Add(current);
            if (current.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in current.EnumerateObject())
                {
                    _Expand(property.Value, nodes);
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in current.EnumerateArray())
                {
                    _Expand(element, nodes);
                }
            }
        }

        foreach (var n in nodeSet)
        {
            _Expand(n, newNodes);
        }

        return [.. newNodes];
    }
}
