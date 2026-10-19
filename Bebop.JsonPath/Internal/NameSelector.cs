using System.Text.Json;

namespace Bebop.JsonPath.Internal;

internal sealed class NameSelector(string Name) : ISelector
{
    public void Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.ValueKind is not JsonValueKind.Object)
            return;
        
        _Evaluate(context, results);
    }

    private void _Evaluate(in SelectorContext context, in ResultCollector results)
    {
        if (context.Current.TryGetProperty(Name, out var value))
        {
            results.Add(value);
        }
    }
}
