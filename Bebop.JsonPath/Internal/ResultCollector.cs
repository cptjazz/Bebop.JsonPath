using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal readonly ref struct ResultCollector
{
    private readonly List<IEnumerable<JsonElement>> _results;
 
    public ResultCollector()
    {
        _results = new List<IEnumerable<JsonElement>>(5);
    }

    public IEnumerable<JsonElement> Results => _results.SelectMany(x => x).ToArray();

    public void Add(in JsonElement value)
    {
        _results.Add([value]);
    }

    public void Add(IEnumerable<JsonElement> values)
    {
        _results.Add(values);
    }
}
