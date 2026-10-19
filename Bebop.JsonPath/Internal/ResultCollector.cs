using System.Runtime.InteropServices;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal readonly struct ResultCollector
{
    private readonly List<JsonElement> _results;

    public ResultCollector()
    {
        _results = new();
    }

    public ReadOnlySpan<JsonElement> Results => CollectionsMarshal.AsSpan(_results);

    internal void Add(in JsonElement value)
    {
        _results.Add(value);
    }
}
