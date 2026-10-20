using System.Runtime.InteropServices;
using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal readonly ref struct ResultCollector
{
    private readonly List<JsonElement> _results;
 
    public ResultCollector()
    {
        _results = new List<JsonElement>(5);
    }

    public ReadOnlySpan<JsonElement> Results => CollectionsMarshal.AsSpan(_results);

    public void Add(in JsonElement value)
    {
        _results.Add(value);
    }
}
