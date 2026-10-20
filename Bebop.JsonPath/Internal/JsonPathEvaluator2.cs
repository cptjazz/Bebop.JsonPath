using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal static class JsonPathEvaluator2
{
    internal static JsonElement[] Evaluate(ReadOnlySpan<Segment> segments, ref readonly JsonElement root)
    {
        ReadOnlySpan<JsonElement> nodeSet = [root];

        foreach (var segment in segments)
        {
            var c = new ResultCollector();

            for (int i = 0; i < nodeSet.Length; i++)
            {
                segment.Evaluate(new SelectorContext(nodeSet[i], root), c);
            }

            nodeSet = c.Results;
        }

        return nodeSet.ToArray();
    }
}
